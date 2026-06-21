using System.Collections.Concurrent;
using System.Timers;
using Sharp7;
using TestWpf.Models;
using Timer = System.Timers.Timer;

namespace TestWpf.Services;

/// <summary>
/// 双连接轮询调度器
/// 连接1（Fast）: I/Q/M 每 tick 必读
/// 连接2（DB Pool）: DB 列表分片轮转
/// </summary>
public sealed class PollingScheduler : IDisposable
{
    private S7Client? _fastConn;
    private S7Client? _dbConn;
    private Timer? _timer;

    private readonly PollingConfig _config;
    private readonly object _lock = new();

    private int _dbIndex;  // 当前 DB 轮转下标
    private int _tick;

    // 最新一轮读取结果
    public ConcurrentDictionary<string, byte> LastValues { get; } = new();

    public bool IsRunning { get; private set; }
    public bool IsConnected { get; private set; }
    public string? LastError { get; private set; }
    public PollingConfig Config => _config;

    public PollingScheduler()
    {
        _config = new PollingConfig();
    }

    public PollingScheduler(PollingConfig config)
    {
        _config = config;
    }

    // ===== 启动/停止 =====

    public void Start(string localIp, string ip, int port, int rack, int slot)
    {
        lock (_lock)
        {
            Stop();

            _fastConn = new S7Client();
            _dbConn = new S7Client();

            int r1 = _fastConn.ConnectTo(ip, rack, slot);
            int r2 = r1 == 0 ? _dbConn.ConnectTo(ip, rack, slot) : 1;
            if (r1 != 0)
            {
                LastError = $"Fast 连接失败: {_fastConn.ErrorText(r1)}";
                return;
            }
            if (r2 != 0)
            {
                LastError = $"DB 连接失败: {_dbConn.ErrorText(r2)}";
                _fastConn.Disconnect();
                return;
            }

            IsConnected = true;
            _dbIndex = 0;
            _tick = 0;

            _timer = new Timer(Math.Max(20, Math.Min(_config.FastInterval, 200)));
            _timer.Elapsed += OnTimerElapsed;
            _timer.AutoReset = true;
            _timer.Start();
            IsRunning = true;
            LastError = null;
        }
    }

    public void Stop()
    {
        lock (_lock)
        {
            if (_timer != null)
            {
                _timer.Stop();
                _timer.Dispose();
                _timer = null;
            }

            _fastConn?.Disconnect();
            _dbConn?.Disconnect();
            _fastConn = null;
            _dbConn = null;

            IsRunning = false;
            IsConnected = false;
        }
    }

    // ===== 定时触发 =====

    private void OnTimerElapsed(object? sender, ElapsedEventArgs e)
    {
        if (!IsConnected) return;
        _tick++;

        // 连接1: 读 I/Q/M
        ReadFastPath();

        // 连接2: 读 DB（分片轮转）
        ReadDbSlice();
    }

    // ===== Fast Path: I/Q/M =====

    private void ReadFastPath()
    {
        var conn = _fastConn;
        if (conn == null || !conn.Connected) return;

        var cfg = _config.Fast;

        // I 区
        if (cfg.EnableI)
        {
            var addrs = cfg.IAddresses;
            if (addrs.Length > 0)
            {
                var data = ReadBytes(conn, S7Area.PE, addrs);
                foreach (var (addr, val) in data)
                    LastValues[$"I{addr}"] = val;
            }
        }

        // Q 区
        if (cfg.EnableQ)
        {
            var addrs = cfg.QAddresses;
            if (addrs.Length > 0)
            {
                var data = ReadBytes(conn, S7Area.PA, addrs);
                foreach (var (addr, val) in data)
                    LastValues[$"Q{addr}"] = val;
            }
        }

        // M 区
        if (cfg.EnableM)
        {
            var addrs = cfg.MAddresses;
            if (addrs.Length > 0)
            {
                var data = ReadBytes(conn, S7Area.MK, addrs);
                foreach (var (addr, val) in data)
                    LastValues[$"M{addr}"] = val;
            }
        }
    }

    // ===== DB Pool: 分片轮转 =====

    private void ReadDbSlice()
    {
        var conn = _dbConn;
        if (conn == null || !conn.Connected) return;

        var items = _config.DbItems.Where(d => d.Enabled).ToList();
        if (items.Count == 0) return;

        // 本轮要读的 DB 个数：尽量让每个 tick 读 ~2 个，但总量不超 ~30ms
        int maxThisTick = 2;

        for (int i = 0; i < maxThisTick && items.Count > 0; i++)
        {
            if (_dbIndex >= items.Count) _dbIndex = 0;
            var item = items[_dbIndex];
            _dbIndex++;

            int len = Math.Min(item.Length, 222);
            byte[] buffer = new byte[len];
            int result = conn.DBRead(item.DbNumber, item.Offset, len, buffer);

            if (result == 0)
            {
                for (int j = 0; j < len; j++)
                    LastValues[$"DB{item.DbNumber}[{item.Offset + j}]"] = buffer[j];
                item.Status = $"OK 0x{buffer[0]:X2}..";
            }
            else
            {
                item.Status = $"ERR {result}";
            }

            // 如果这一次耗时已经较多，不再读更多 DB
            if (i == 0 && len > 100) break; // 大块只读 1 个
        }
    }

    // ===== 读取工具 =====

    private static Dictionary<int, byte> ReadBytes(S7Client client, S7Area area, int[] addresses)
    {
        var result = new Dictionary<int, byte>();
        var sorted = addresses.Distinct().OrderBy(a => a).ToArray();
        var groups = GroupConsecutive(sorted);

        foreach (var (start, count) in groups)
        {
            byte[] buf = new byte[count];
            int ret = client.ReadArea(area, 0, start, count, S7WordLength.Byte, buf);
            if (ret == 0)
                for (int i = 0; i < count; i++) result[start + i] = buf[i];
        }
        return result;
    }

    private static List<(int Start, int Count)> GroupConsecutive(int[] sorted)
    {
        var groups = new List<(int, int)>();
        if (sorted.Length == 0) return groups;

        int gs = sorted[0], ge = sorted[0];
        for (int i = 1; i < sorted.Length; i++)
        {
            if (sorted[i] == ge + 1) { ge = sorted[i]; }
            else { groups.Add((gs, ge - gs + 1)); gs = sorted[i]; ge = sorted[i]; }
        }
        groups.Add((gs, ge - gs + 1));
        return groups;
    }

    // ===== 查询最新值 =====

    public byte? GetValue(string key) =>
        LastValues.TryGetValue(key, out var val) ? val : null;

    public bool GetBit(string key, int bitIndex)
    {
        if (LastValues.TryGetValue(key, out var val))
            return ((val >> bitIndex) & 1) == 1;
        return false;
    }

    // ===== 写操作 =====

    public bool WriteByte(string areaType, int byteAddr, byte value)
    {
        var conn = _fastConn;
        if (conn == null || !conn.Connected) return false;

        S7Area area = areaType.ToUpper() switch
        {
            "Q" => S7Area.PA,
            "M" => S7Area.MK,
            _ => S7Area.PA
        };

        byte[] buf = [value];
        int r = conn.WriteArea(area, 0, byteAddr, 1, S7WordLength.Byte, buf);
        if (r == 0)
        {
            LastValues[$"{areaType}{byteAddr}"] = value;
            return true;
        }
        LastError = conn.ErrorText(r);
        return false;
    }

    public void Dispose() => Stop();
}
