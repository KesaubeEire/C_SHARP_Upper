using System.Collections.Concurrent;
using System.Diagnostics;
using System.Timers;
using Microsoft.Extensions.Logging;
using Timer = System.Timers.Timer;

namespace WpfScada.Services.Plc.Modbus;

public record ModbusPollingConfig
{
    public byte DeviceAddr { get; init; }
    public byte FuncCode { get; init; } = 0x03;
    public ushort StartAddr { get; init; }
    public ushort Count { get; init; } = 10;
    public int IntervalMs { get; init; } = 1000;
    public string Tag { get; init; } = "";
}

public class ModbusPollingResult
{
    public ModbusRequest Request { get; }
    public ModbusParseResult ParseResult { get; }
    public byte[] RawFrame { get; }
    public bool IsTcp { get; }
    public TimeSpan Elapsed { get; }
    public bool IsTimeout { get; set; }
    public string? ErrorMessage { get; set; }

    public ModbusPollingResult(ModbusRequest request, ModbusParseResult parseResult, byte[] rawFrame, bool isTcp, TimeSpan elapsed)
    {
        Request = request; ParseResult = parseResult; RawFrame = rawFrame; IsTcp = isTcp; Elapsed = elapsed;
    }

    public static ModbusPollingResult Timeout(ModbusRequest request, bool isTcp, TimeSpan elapsed)
        => new(request, new ModbusParseResult { IsError = true, ErrorMessage = "超时" }, [], isTcp, elapsed) { IsTimeout = true };

    public static ModbusPollingResult Error(ModbusRequest request, bool isTcp, string error)
        => new(request, new ModbusParseResult { IsError = true, ErrorMessage = error }, [], isTcp, TimeSpan.Zero) { ErrorMessage = error };
}

public class ModbusPollingService : IDisposable
{
    private readonly ILogger<ModbusPollingService> _logger;
    private readonly ModbusTransport _transport;
    private readonly Func<bool> _isTcpMode;

    private readonly ConcurrentQueue<ModbusRequest> _queue = new();
    private readonly AutoResetEvent _signal = new(false);
    private CancellationTokenSource? _cts;
    private Task? _consumerTask;

    private readonly List<ModbusPollingConfig> _pollingConfigs = [];
    private readonly Dictionary<byte, ModbusDeviceState> _deviceStates = [];
    private readonly object _configLock = new();
    private Timer? _pollTimer;
    private bool _pollingActive;
    private bool _disposed;

    private int _requestsSent;
    private int _requestsSucceeded;
    private int _requestsFailed;

    public event Action<ModbusPollingResult>? DataReceived;
    public event Action<byte, bool>? DeviceOnlineChanged;
    public event Action<bool>? ServiceStateChanged;

    public ModbusPollingService(ILogger<ModbusPollingService> logger, ModbusTransport transport, Func<bool> isTcpModeCallback)
    {
        _logger = logger;
        _transport = transport;
        _isTcpMode = isTcpModeCallback;
    }

    public bool IsRunning => _cts is { IsCancellationRequested: false };
    public int QueueLength => _queue.Count;
    public IReadOnlyList<ModbusPollingConfig> PollingConfigs { get { lock (_configLock) return [.. _pollingConfigs]; } }

    public void Start()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (IsRunning) return;
        _cts = new CancellationTokenSource();
        _consumerTask = Task.Run(() => ConsumerLoop(_cts.Token));
        ServiceStateChanged?.Invoke(true);
    }

    public void Stop()
    {
        StopPollingTimer();
        _cts?.Cancel();
        _signal.Set();
        try { _consumerTask?.Wait(1000); } catch (Exception ex) { _logger.LogDebug(ex, "Modbus 消费者任务等待超时"); }
        _consumerTask = null;
        _cts?.Dispose();
        _cts = null;
        ServiceStateChanged?.Invoke(false);
    }

    public void StartPolling()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!IsRunning) Start();
        _pollingActive = true;
        int minInterval;
        lock (_configLock) { minInterval = _pollingConfigs.Count > 0 ? _pollingConfigs.Min(c => c.IntervalMs) : 1000; }
        _pollTimer = new Timer(Math.Max(50, minInterval));
        _pollTimer.Elapsed += PollTimerCallback;
        _pollTimer.AutoReset = true;
        _pollTimer.Start();
    }

    public void StopPolling()
    {
        _pollingActive = false;
        StopPollingTimer();
    }

    public void AddPollingConfig(ModbusPollingConfig config)
    {
        lock (_configLock)
        {
            _pollingConfigs.Add(config);
            if (!_deviceStates.ContainsKey(config.DeviceAddr))
                _deviceStates[config.DeviceAddr] = new ModbusDeviceState(config.DeviceAddr);
        }
    }

    public void RemovePollingConfig(ModbusPollingConfig config)
    {
        lock (_configLock) _pollingConfigs.Remove(config);
    }

    public void ClearPollingConfigs()
    {
        lock (_configLock) _pollingConfigs.Clear();
    }

    public ModbusDeviceState GetDeviceState(byte address)
    {
        lock (_configLock)
        {
            if (!_deviceStates.TryGetValue(address, out var state))
            {
                state = new ModbusDeviceState(address);
                _deviceStates[address] = state;
            }
            return state;
        }
    }

    public void Enqueue(ModbusRequest request)
    {
        if (_disposed) return;
        _queue.Enqueue(request);
        Interlocked.Increment(ref _requestsSent);
        _signal.Set();
    }

    public ModbusStats GetStats() => new()
    {
        RequestsSent = _requestsSent,
        RequestsSucceeded = _requestsSucceeded,
        RequestsFailed = _requestsFailed,
        QueueLength = _queue.Count,
        PendingConfigs = _pollingConfigs.Count,
        OnlineDevices = _deviceStates.Values.Count(s => s.IsOnline),
        OfflineDevices = _deviceStates.Values.Count(s => !s.IsOnline),
    };

    private async Task ConsumerLoop(CancellationToken ct)
    {
        var sw = new Stopwatch();
        while (!ct.IsCancellationRequested)
        {
            if (!_queue.TryDequeue(out var request))
            {
                _signal.WaitOne(200);
                continue;
            }

            sw.Restart();
            try
            {
                byte[] response = await _transport.SendAndReadAsync(
                    request.DeviceAddr, request.FuncCode,
                    request.StartAddr, request.Count, timeoutMs: 2000);

                var elapsed = sw.Elapsed;
                bool isTcp = _isTcpMode();
                byte[] pduBuf = isTcp && response.Length > ModbusProtocol.MBAP_HEADER_SIZE
                    ? response[ModbusProtocol.MBAP_HEADER_SIZE..]
                    : response;

                var parseResult = ModbusProtocol.ParseResponse(pduBuf);
                var devState = GetDeviceState(request.DeviceAddr);
                bool wasOnline = devState.IsOnline;

                if (parseResult.IsError) { devState.RecordFailure(); Interlocked.Increment(ref _requestsFailed); }
                else { devState.RecordSuccess(); Interlocked.Increment(ref _requestsSucceeded); }

                if (wasOnline != devState.IsOnline)
                    DeviceOnlineChanged?.Invoke(request.DeviceAddr, devState.IsOnline);

                DataReceived?.Invoke(new ModbusPollingResult(request, parseResult, response, isTcp, elapsed));
            }
            catch (TimeoutException)
            {
                Interlocked.Increment(ref _requestsFailed);
                var devState = GetDeviceState(request.DeviceAddr);
                bool wasOnline = devState.IsOnline;
                devState.RecordFailure();
                if (wasOnline != devState.IsOnline)
                    DeviceOnlineChanged?.Invoke(request.DeviceAddr, devState.IsOnline);
                DataReceived?.Invoke(ModbusPollingResult.Timeout(request, _isTcpMode(), sw.Elapsed));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Modbus 请求处理异常: Addr={Addr} Func={Func}", request.DeviceAddr, request.FuncCode);
                Interlocked.Increment(ref _requestsFailed);
                DataReceived?.Invoke(ModbusPollingResult.Error(request, _isTcpMode(), ex.Message));
            }
        }
    }

    private void PollTimerCallback(object? sender, ElapsedEventArgs e)
    {
        if (!_pollingActive || _disposed) return;

        List<ModbusPollingConfig> configs;
        lock (_configLock) configs = [.. _pollingConfigs];

        foreach (var cfg in configs)
        {
            if (GetDeviceState(cfg.DeviceAddr).ShouldSkip()) continue;
            Enqueue(new ModbusRequest { DeviceAddr = cfg.DeviceAddr, FuncCode = cfg.FuncCode, StartAddr = cfg.StartAddr, Count = cfg.Count, Tag = cfg.Tag });
        }
    }

    private void StopPollingTimer()
    {
        if (_pollTimer != null)
        {
            _pollTimer.Stop();
            _pollTimer.Elapsed -= PollTimerCallback;
            _pollTimer.Dispose();
            _pollTimer = null;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Stop();
        _signal.Dispose();
    }
}

public class ModbusStats
{
    public int RequestsSent { get; init; }
    public int RequestsSucceeded { get; init; }
    public int RequestsFailed { get; init; }
    public int QueueLength { get; init; }
    public int PendingConfigs { get; init; }
    public int OnlineDevices { get; init; }
    public int OfflineDevices { get; init; }
    public int TotalProcessed => RequestsSucceeded + RequestsFailed;
    public double SuccessRate => TotalProcessed > 0 ? Math.Round((double)RequestsSucceeded / TotalProcessed * 100, 1) : 0;
}
