using System.Collections.Concurrent;

namespace TEST_101;

/// <summary>
/// 轮询配置项 — 描述一个设备的轮询参数。
/// </summary>
public record PollingConfig
{
    public byte DeviceAddr { get; init; }
    public byte FuncCode { get; init; } = 0x03; // 默认读保持寄存器
    public ushort StartAddr { get; init; }
    public ushort Count { get; init; } = 10;
    public int IntervalMs { get; init; } = 1000; // 轮询间隔（毫秒）
    public string Tag { get; init; } = "";
}

/// <summary>
/// 轮询结果 — 消费者线程产出的一帧解析结果。
/// </summary>
public class PollingResult
{
    public ModbusRequest Request { get; }
    public ModbusParseResult ParseResult { get; }
    public byte[] RawFrame { get; }
    public bool IsTcp { get; }
    public TimeSpan Elapsed { get; }
    public bool IsTimeout { get; set; }
    public string? ErrorMessage { get; set; }

    public PollingResult(ModbusRequest request, ModbusParseResult parseResult, byte[] rawFrame, bool isTcp, TimeSpan elapsed)
    {
        Request = request;
        ParseResult = parseResult;
        RawFrame = rawFrame;
        IsTcp = isTcp;
        Elapsed = elapsed;
    }

    public static PollingResult Timeout(ModbusRequest request, bool isTcp, TimeSpan elapsed)
        => new(request, new ModbusParseResult { IsError = true, ErrorMessage = "超时" }, Array.Empty<byte>(), isTcp, elapsed)
        { IsTimeout = true, ErrorMessage = "超时" };

    public static PollingResult Error(ModbusRequest request, bool isTcp, string error)
        => new(request, new ModbusParseResult { IsError = true, ErrorMessage = error }, Array.Empty<byte>(), isTcp, TimeSpan.Zero)
        { ErrorMessage = error };
}

/// <summary>
/// 生产者-消费者轮询服务 — 用 ConcurrentQueue + AutoResetEvent 实现请求队列。
///
/// 工作流：
///   Timer 定时触发 → 遍历 PollingConfigs → 跳过退避中设备 → Enqueue()
///   用户按钮点击 → Enqueue()
///   ─────────────────────────────────────
///   消费者线程逐一取出请求 → SendAndReadSync() → 解析 → DataReceived 事件
///
/// 特性：
///   - 线程安全，多生产者可同时入队
///   - 队列空时消费者线程完全阻塞（不占 CPU）
///   - 支持设备级退避重试（指数退避）
///   - 支持运行时动态增删轮询配置
/// </summary>
public class ModbusPollingService : IDisposable
{
    private readonly ModbusTransport _transport;
    private readonly Func<bool> _isTcpMode;

    // ─── 队列引擎 ───
    private readonly ConcurrentQueue<ModbusRequest> _queue = new();
    private readonly AutoResetEvent _signal = new(false);
    private CancellationTokenSource? _cts;
    private Task? _consumerTask;

    // ─── 定时轮询 ───
    private readonly List<PollingConfig> _pollingConfigs = new();
    private readonly Dictionary<byte, DeviceState> _deviceStates = new();
    private readonly object _configLock = new();
    private System.Threading.Timer? _pollTimer;
    private bool _pollingActive;

    // ─── 状态 ───
    private bool _disposed;
    private int _requestsSent;
    private int _requestsSucceeded;
    private int _requestsFailed;

    // ========== 事件（在消费者线程上触发，UI 层需 Invoke）==========

    /// <summary>一帧数据解析完成（不管是轮询还是手动）</summary>
    public event Action<PollingResult>? DataReceived;

    /// <summary>设备在线状态变化</summary>
    public event Action<byte, bool>? DeviceOnlineChanged;

    /// <summary>服务启停状态变化</summary>
    public event Action<bool>? ServiceStateChanged;

    /// <summary>轮询统计更新（每秒约一次）</summary>
    public event Action<PollingStats>? StatsUpdated;

    // ========== 构造函数 ==========

    public ModbusPollingService(ModbusTransport transport, Func<bool> isTcpModeCallback)
    {
        _transport = transport ?? throw new ArgumentNullException(nameof(transport));
        _isTcpMode = isTcpModeCallback ?? throw new ArgumentNullException(nameof(isTcpModeCallback));
    }

    // ========== 属性 ==========

    public bool IsRunning => _cts != null && !_cts.IsCancellationRequested;
    public int QueueLength => _queue.Count;
    public IReadOnlyList<PollingConfig> PollingConfigs
    {
        get { lock (_configLock) return _pollingConfigs.ToList(); }
    }

    // ========== 启动 / 停止 ==========

    /// <summary>启动消费者线程（队列引擎）。未开启定时器时可用 Enqueue 手动入队。</summary>
    public void Start()
    {
        if (_disposed) throw new ObjectDisposedException(nameof(ModbusPollingService));
        if (IsRunning) return;

        _cts = new CancellationTokenSource();
        _consumerTask = Task.Run(() => ConsumerLoop(_cts.Token));
        ServiceStateChanged?.Invoke(true);
    }

    /// <summary>停止消费者线程</summary>
    public void Stop()
    {
        StopPollingTimer();
        _cts?.Cancel();
        _signal.Set(); // 唤醒消费者线程让它退出

        try { _consumerTask?.Wait(1000); } catch { }
        _consumerTask = null;
        _cts?.Dispose();
        _cts = null;

        ServiceStateChanged?.Invoke(false);
    }

    // ========== 定时轮询控制 ==========

    /// <summary>
    /// 启动定时轮询。会同时确保消费者线程 running。
    /// 轮询周期 = 最短 IntervalMs 的配置项（通常是 100~1000ms）。
    /// 每次 tick 遍历所有配置，把可以轮询的请求入队。
    /// </summary>
    public void StartPolling()
    {
        if (_disposed) throw new ObjectDisposedException(nameof(ModbusPollingService));
        if (!IsRunning) Start();

        _pollingActive = true;
        // 轮询周期取最短间隔
        int minInterval;
        lock (_configLock)
        {
            minInterval = _pollingConfigs.Count > 0
                ? _pollingConfigs.Min(c => c.IntervalMs)
                : 1000;
        }

        _pollTimer = new System.Threading.Timer(PollTimerCallback, null, 0, Math.Max(50, minInterval));
    }

    /// <summary>停止自动轮询（但消费者线程继续运行，仍可手动 Enqueue）</summary>
    public void StopPolling()
    {
        _pollingActive = false;
        StopPollingTimer();
    }

    // ========== 轮询配置管理 ==========

    /// <summary>添加一个轮询配置项</summary>
    public void AddPollingConfig(PollingConfig config)
    {
        lock (_configLock)
        {
            _pollingConfigs.Add(config);
            if (!_deviceStates.ContainsKey(config.DeviceAddr))
                _deviceStates[config.DeviceAddr] = new DeviceState(config.DeviceAddr);
        }
    }

    /// <summary>移除一个轮询配置项</summary>
    public void RemovePollingConfig(PollingConfig config)
    {
        lock (_configLock) _pollingConfigs.Remove(config);
    }

    /// <summary>清空所有轮询配置</summary>
    public void ClearPollingConfigs()
    {
        lock (_configLock) _pollingConfigs.Clear();
    }

    /// <summary>获取设备状态（不存在时自动创建）</summary>
    public DeviceState GetDeviceState(byte address)
    {
        lock (_configLock)
        {
            if (!_deviceStates.ContainsKey(address))
                _deviceStates[address] = new DeviceState(address);
            return _deviceStates[address];
        }
    }

    // ========== 生产者入口 ==========

    /// <summary>
    /// 入队一个请求（线程安全）。
    /// 不管来自定时器还是手动按钮，都走这一个入口。
    /// </summary>
    public void Enqueue(ModbusRequest request)
    {
        if (_disposed) return;

        _queue.Enqueue(request);
        Interlocked.Increment(ref _requestsSent);
        _signal.Set(); // 唤醒消费者
    }

    // ========== 统计 ==========

    public PollingStats GetStats() => new()
    {
        RequestsSent = _requestsSent,
        RequestsSucceeded = _requestsSucceeded,
        RequestsFailed = _requestsFailed,
        QueueLength = _queue.Count,
        PendingConfigs = _pollingConfigs.Count,
        OnlineDevices = _deviceStates.Values.Count(s => s.IsOnline),
        OfflineDevices = _deviceStates.Values.Count(s => !s.IsOnline),
    };

    // ========== 消费者循环（后台线程）==========

    private void ConsumerLoop(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            if (!_queue.TryDequeue(out var request))
            {
                // 队列空了 → 阻塞等待，最多 200ms 唤醒一次检查退出信号
                _signal.WaitOne(200);
                continue;
            }

            // --- 处理一个请求 ---
            DateTime start = DateTime.Now;
            try
            {
                // 1. 同步发送 + 等待响应
                byte[] response = _transport.SendAndReadSync(
                    request.DeviceAddr, request.FuncCode,
                    request.StartAddr, request.Count, timeoutMs: 2000);

                TimeSpan elapsed = DateTime.Now - start;

                // 2. 解析
                // TCP 帧前面 7 字节是 MBAP 头
                bool isTcp = _isTcpMode();
                byte[] pduBuf = isTcp && response.Length > ModbusProtocol.MBAP_HEADER_SIZE
                    ? response.Skip(ModbusProtocol.MBAP_HEADER_SIZE).ToArray()
                    : response;

                var parseResult = ModbusProtocol.ParseResponse(pduBuf);

                // 3. 更新设备状态
                var devState = GetDeviceState(request.DeviceAddr);
                bool wasOnline = devState.IsOnline;
                if (parseResult.IsError)
                {
                    devState.RecordFailure();
                    Interlocked.Increment(ref _requestsFailed);
                }
                else
                {
                    devState.RecordSuccess();
                    Interlocked.Increment(ref _requestsSucceeded);
                }

                // 4. 设备状态变化通知
                if (wasOnline != devState.IsOnline)
                    DeviceOnlineChanged?.Invoke(request.DeviceAddr, devState.IsOnline);

                // 5. 通知 UI
                var result = new PollingResult(request, parseResult, response, isTcp, elapsed);
                DataReceived?.Invoke(result);
            }
            catch (TimeoutException tex)
            {
                Interlocked.Increment(ref _requestsFailed);
                var devState2 = GetDeviceState(request.DeviceAddr);
                bool wasOnline2 = devState2.IsOnline;
                devState2.RecordFailure();
                if (wasOnline2 != devState2.IsOnline)
                    DeviceOnlineChanged?.Invoke(request.DeviceAddr, devState2.IsOnline);

                DataReceived?.Invoke(PollingResult.Timeout(request, _isTcpMode(), DateTime.Now - start));
            }
            catch (Exception ex)
            {
                Interlocked.Increment(ref _requestsFailed);
                DataReceived?.Invoke(PollingResult.Error(request, _isTcpMode(), ex.Message));
            }
        }
    }

    // ========== 定时器回调 ==========

    private void PollTimerCallback(object? state)
    {
        if (!_pollingActive || _disposed) return;

        List<PollingConfig> configs;
        lock (_configLock) configs = _pollingConfigs.ToList();

        foreach (var cfg in configs)
        {
            // 跳过退避中的设备
            var devState = GetDeviceState(cfg.DeviceAddr);
            if (devState.ShouldSkip())
                continue;

            Enqueue(new ModbusRequest(
                cfg.DeviceAddr, cfg.FuncCode,
                cfg.StartAddr, cfg.Count, cfg.Tag));
        }
    }

    // ========== 辅助方法 ==========

    private void StopPollingTimer()
    {
        _pollTimer?.Dispose();
        _pollTimer = null;
    }

    // ========== 释放 ==========

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        Stop();
        _signal.Dispose();
    }
}

/// <summary>轮询统计快照</summary>
public class PollingStats
{
    public int RequestsSent { get; init; }
    public int RequestsSucceeded { get; init; }
    public int RequestsFailed { get; init; }
    public int QueueLength { get; init; }
    public int PendingConfigs { get; init; }
    public int OnlineDevices { get; init; }
    public int OfflineDevices { get; init; }

    public int TotalProcessed => RequestsSucceeded + RequestsFailed;
    public double SuccessRate => TotalProcessed > 0
        ? Math.Round((double)RequestsSucceeded / TotalProcessed * 100, 1)
        : 0;
}
