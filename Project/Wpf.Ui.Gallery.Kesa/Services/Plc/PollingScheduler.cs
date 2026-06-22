using System.Collections.Concurrent;
using System.Timers;
using Sharp7;
using Timer = System.Timers.Timer;
using Wpf.Ui.Gallery.Models.Plc;

namespace Wpf.Ui.Gallery.Services.Plc;

public class PollingScheduler : IDisposable
{
    private Timer? _timer;
    private volatile bool _busy;
    private S7Service? _s7;
    private S7Client? _dbClient;
    private int _dbIndex;
    private int _maxThisTick = 2;
    private readonly ConcurrentDictionary<string, byte> _lastValues = new(StringComparer.OrdinalIgnoreCase);

    public PollingConfig Config { get; } = new();
    public bool IsRunning => _timer?.Enabled ?? false;
    public bool IsConnected { get; private set; }
    public string? LastError { get; private set; }
    public long LatencyMs { get; private set; }
    public ConcurrentDictionary<string, byte> LastValues => _lastValues;

    public event Action<HashSet<string>>? DataUpdated;

    public void Start(S7Service s7, int port = 102)
    {
        Stop();
        _s7 = s7;
        IsConnected = s7.IsConnected;

        if (Config.DbItems.Any(x => x.Enabled))
        {
            _dbClient = new S7Client();
            int ret = _dbClient.ConnectTo(Config.DbIp, Config.DbRack, Config.DbSlot);
            if (ret != 0)
            {
                LastError = "连接失败: " + ret;
                _dbClient = null;
            }
        }

        _timer = new Timer(Config.FastInterval);
        _timer.Elapsed += OnTimerElapsed;
        _timer.AutoReset = false;
        _timer.Start();
    }

    public void Stop()
    {
        _timer?.Stop();
        _timer?.Dispose();
        _timer = null;
        _dbClient?.Disconnect();
        _dbClient = null;
        _busy = false;
    }

    private void OnTimerElapsed(object? sender, ElapsedEventArgs e)
    {
        if (_busy || _s7 == null) return;
        _busy = true;
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var updated = new HashSet<string>();

        try
        {
            // Fast path: I/Q/M
            var fast = Config.Fast;
            ReadFastArea(S7Service.AreaI, fast.PollIAddr, "I", updated);
            ReadFastArea(S7Service.AreaQ, fast.PollQAddr, "Q", updated);
            ReadFastArea(S7Service.AreaM, fast.PollMAddr, "M", updated);

            // DB polling (round-robin)
            if (_dbClient != null && _dbClient.Connected)
            {
                var enabled = Config.DbItems.Where(x => x.Enabled).ToList();
                int tickCount = 0;
                while (tickCount < _maxThisTick && enabled.Count > 0)
                {
                    var item = enabled[_dbIndex % enabled.Count];
                    _dbIndex++;
                    if (item.Length > 100) { _maxThisTick = 1; }
                    else { _maxThisTick = 2; }

                    var buf = new byte[item.Length];
                    int ret = _dbClient.DBRead(item.DbNumber, item.Offset, item.Length, buf);
                    if (ret == 0)
                    {
                        for (int i = 0; i < item.Length; i++)
                        {
                            string key = $"DB{item.DbNumber}[{item.Offset + i}]";
                            _lastValues[key] = buf[i];
                            updated.Add(key);
                        }
                        item.Status = "OK";
                    }
                    else
                    {
                        item.Status = "错误: " + ret;
                    }
                    tickCount++;
                }
            }
        }
        catch { }
        finally
        {
            sw.Stop();
            LatencyMs = sw.ElapsedMilliseconds;
            _busy = false;
            if (_timer != null)
            {
                try { _timer.Start(); } catch { }
            }
        }

        if (updated.Count > 0)
            DataUpdated?.Invoke(updated);
    }

    private void ReadFastArea(int area, string addrStr, string prefix, HashSet<string> updated)
    {
        if (_s7 == null || string.IsNullOrWhiteSpace(addrStr)) return;
        var addrs = Config.Fast.ResolveAddr(addrStr);
        if (addrs.Length == 0) return;

        var bytes = _s7.ReadBytes(area, addrs);
        foreach (var kv in bytes)
        {
            string key = $"{prefix}{kv.Key}";
            _lastValues[key] = kv.Value;
            updated.Add(key);
        }
    }

    public bool WriteByte(int areaType, int byteAddr, byte value)
    {
        if (_s7 == null) return false;
        bool ok = _s7.WriteByte(areaType, byteAddr, value);
        if (ok)
        {
            string key = $"W{byteAddr}";
            _lastValues[key] = value;
        }
        return ok;
    }

    public byte? GetValue(string key)
    {
        return _lastValues.TryGetValue(key, out var v) ? v : null;
    }

    public void Dispose()
    {
        Stop();
    }
}
