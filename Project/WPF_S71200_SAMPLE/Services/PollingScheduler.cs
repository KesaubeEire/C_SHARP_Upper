using System.Collections.Concurrent;
using System.Diagnostics;
using System.Timers;
using Sharp7;
using TestWpf.Models;
using Timer = System.Timers.Timer;

namespace TestWpf.Services;

/// <summary>
/// 单连接轮询调度器 — 复用 S7Service 的连接，不额外占用 PLC 连接数
/// - I/Q/M 通过共享的 S7Service 读取（跟手动读取用同一连接）
/// - DB 列表分片轮转（独立连接，一个 DB 连接足够）
/// - DataUpdated 事件推送
/// </summary>
public sealed class PollingScheduler : IDisposable
{
    private S7Client? _dbConn;
    private Timer? _timer;
    private S7Service? _s7;

    private readonly PollingConfig _config;
    private readonly object _lock = new();

    private int _dbIndex;
    private int _tick;

    /// <summary>防重入标志 — 前一次还没跑完就跳过本次</summary>
    private volatile bool _busy;

    public ConcurrentDictionary<string, byte> LastValues { get; } = new();

    public bool IsRunning { get; private set; }
    public bool IsConnected { get; private set; }
    public string? LastError { get; private set; }
    /// <summary>最近一次轮询读操作的耗时（ms）</summary>
    public long LatencyMs { get; private set; }
    public PollingConfig Config => _config;

    public event Action<HashSet<string>>? DataUpdated;

    public PollingScheduler() => _config = new PollingConfig();
    public PollingScheduler(PollingConfig config) => _config = config;

    // ===== 启动/停止 =====

    /// <summary>使用共享的 S7Service（复用其连接，不额外占 PLC 连接数）</summary>
    public void Start(S7Service s7, int port)
    {
        lock (_lock)
        {
            Stop();
            _s7 = s7;

            if (_config.DbItems.Count > 0)
            {
                _dbConn = new S7Client();
                int portVal = port;
                _dbConn.SetParam(Sharp7.S7Consts.p_u16_LocalPort, ref portVal);
                int r = _dbConn.ConnectTo(_config.DbIp, _config.DbRack, _config.DbSlot);
                if (r != 0)
                {
                    LastError = $"DB 连接失败: {_dbConn.ErrorText(r)}";
                    _dbConn = null;
                }
            }

            IsConnected = s7.IsConnected;
            _dbIndex = 0;
            _tick = 0;

            _timer = new Timer(Math.Max(20, Math.Min(_config.FastInterval, 200)));
            _timer.Elapsed += OnTimerElapsed;
            _timer.AutoReset = false;   // 手动重启，防止重叠
            _timer.Start();
            IsRunning = true;
            LastError = null;
        }
    }

    public void Stop()
    {
        lock (_lock)
        {
            if (_timer != null) { _timer.Stop(); _timer.Dispose(); _timer = null; }
            _dbConn?.Disconnect(); _dbConn = null;
            _s7 = null;
            IsRunning = false;
            IsConnected = false;
        }
    }

    // ===== 定时触发 =====

    private void OnTimerElapsed(object? sender, ElapsedEventArgs e)
    {
        // 防重入：上次还没跑完就跳过本次 tick，不重启 timer（等下次触发）
        if (!IsRunning || _busy) return;
        if (_s7 == null || !_s7.IsConnected) { IsConnected = false; return; }

        _busy = true;
        var sw = Stopwatch.StartNew();
        try
        {
            _tick++;
            var updated = new HashSet<string>();

            ReadFastPath(updated);
            ReadDbSlice(updated);

            sw.Stop();
            LatencyMs = sw.ElapsedMilliseconds;

            DataUpdated?.Invoke(updated);
        }
        catch (Exception ex)
        {
            sw.Stop();
            LastError = ex.Message;
        }
        finally
        {
            _busy = false;
            // 工作完成后再启动下一次定时，绝不重叠
            if (IsRunning)
            {
                try
                {
                    _timer!.Interval = Math.Max(20, Math.Min(_config.FastInterval, 200));
                    _timer.Start();
                }
                catch { /* Dispose 竞争时忽略 */ }
            }
        }
    }

    // ===== Fast Path: I/Q/M（复用 S7Service 连接） =====

    private void ReadFastPath(HashSet<string> updated)
    {
        var s7 = _s7;
        if (s7 == null) return;

        var cfg = _config.Fast;

        if (cfg.EnableI && cfg.IAddresses.Length > 0)
        {
            var data = s7.ReadBytes(S7Service.AreaI, cfg.IAddresses);
            foreach (var (addr, val) in data)
            { LastValues[$"I{addr}"] = val; updated.Add($"I{addr}"); }
        }

        if (cfg.EnableQ && cfg.QAddresses.Length > 0)
        {
            var data = s7.ReadBytes(S7Service.AreaQ, cfg.QAddresses);
            foreach (var (addr, val) in data)
            { LastValues[$"Q{addr}"] = val; updated.Add($"Q{addr}"); }
        }

        if (cfg.EnableM && cfg.MAddresses.Length > 0)
        {
            var data = s7.ReadBytes(S7Service.AreaM, cfg.MAddresses);
            foreach (var (addr, val) in data)
            { LastValues[$"M{addr}"] = val; updated.Add($"M{addr}"); }
        }
    }

    // ===== DB Pool: 分片轮转 =====

    private void ReadDbSlice(HashSet<string> updated)
    {
        var conn = _dbConn;
        if (conn == null || !conn.Connected) return;

        var items = _config.DbItems.Where(d => d.Enabled).ToList();
        if (items.Count == 0) return;

        int maxThisTick = 2;
        for (int i = 0; i < maxThisTick && items.Count > 0; i++)
        {
            if (_dbIndex >= items.Count) _dbIndex = 0;
            var item = items[_dbIndex];
            _dbIndex++;

            int len = Math.Min(item.Length, 222);
            byte[] buf = new byte[len];
            int ret = conn.DBRead(item.DbNumber, item.Offset, len, buf);

            if (ret == 0)
            {
                for (int j = 0; j < len; j++)
                {
                    string key = $"DB{item.DbNumber}[{item.Offset + j}]";
                    LastValues[key] = buf[j];
                    updated.Add(key);
                }
                item.Status = $"OK 0x{buf[0]:X2}..";
            }
            else
            {
                item.Status = $"ERR {ret}";
            }
            if (i == 0 && len > 100) break;
        }
    }

    // ===== 写操作（通过 S7Service） =====

    public bool WriteByte(string areaType, int byteAddr, byte value)
    {
        if (_s7 == null) return false;
        int area = areaType.ToUpper() switch { "Q" => S7Service.AreaQ, "M" => S7Service.AreaM, _ => S7Service.AreaQ };
        bool ok = _s7.WriteByte(area, byteAddr, value);
        if (ok) LastValues[$"{areaType}{byteAddr}"] = value;
        return ok;
    }

    // ===== 查询最新值 =====

    public byte? GetValue(string key) =>
        LastValues.TryGetValue(key, out var val) ? val : null;

    public void Dispose() => Stop();
}
