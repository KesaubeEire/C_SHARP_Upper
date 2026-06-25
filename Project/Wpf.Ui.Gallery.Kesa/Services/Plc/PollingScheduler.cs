using System.Collections.Concurrent;
using System.Timers;
using Sharp7;
using Timer = System.Timers.Timer;
using Wpf.Ui.Gallery.Controls.Plc;
using Wpf.Ui.Gallery.Models.Plc;

namespace Wpf.Ui.Gallery.Services.Plc;

public class PollingScheduler : IDisposable
{
    private readonly PollingStore _store;
    private Timer? _timer;
    private volatile bool _busy;
    private S7Service? _s7;
    private S7Client? _dbClient;
    private int _dbIndex;
    private int _maxThisTick = 2;
    private readonly ConcurrentDictionary<string, byte> _lastValues = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, double> _typedValues = new(StringComparer.OrdinalIgnoreCase);

    public PollingScheduler(PollingStore store)
    {
        _store = store;
    }

    public PollingConfig Config { get; } = new();
    public bool IsRunning => _store.IsRunning;
    public long LatencyMs => _store.LatencyMs;
    public ConcurrentDictionary<string, byte> LastValues => _lastValues;

    public event Action<HashSet<string>>? DataUpdated;

    public void Start(S7Service s7, int port = 102)
    {
        Stop();
        _s7 = s7;

        if (Config.DbItems.Any(x => x.Enabled))
        {
            _dbClient = new S7Client();
            int ret = _dbClient.ConnectTo(Config.DbIp, Config.DbRack, Config.DbSlot);
            if (ret != 0)
            {
                _dbClient = null;
            }
        }

        _timer = new Timer(Config.FastInterval);
        _timer.Elapsed += OnTimerElapsed;
        _timer.AutoReset = false;
        _timer.Start();

        _store.IsRunning = true;
        _store.StatusText = "轮询运行中";
        _store.Quality = LedQuality.Good;
    }

    public void Stop()
    {
        _timer?.Stop();
        _timer?.Dispose();
        _timer = null;
        _dbClient?.Disconnect();
        _dbClient = null;
        _busy = false;

        _store.IsRunning = false;
        _store.StatusText = "已停止";
        _store.Quality = LedQuality.Disabled;
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

                    var buf = new byte[item.EffectiveLength];
                    int ret = _dbClient.DBRead(item.DbNumber, item.Offset, item.EffectiveLength, buf);
                    if (ret == 0)
                    {
                        for (int i = 0; i < item.EffectiveLength; i++)
                        {
                            string key = $"DB{item.DbNumber}[{item.Offset + i}]";
                            _lastValues[key] = buf[i];
                            updated.Add(key);
                        }

                        // 解析类型化值（非 BYTE 类型）
                        string typedKey = $"DB{item.DbNumber}:{item.Offset}";
                        double decoded = DecodeTypedValue(buf, item.DataType);
                        _typedValues[typedKey] = decoded;
                        updated.Add(typedKey);

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
            _store.LatencyMs = sw.ElapsedMilliseconds;
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

    /// <summary>
    /// 取类型化解码后的 double 值（REAL/DINT/INT/WORD）。
    /// 若不存在则回退到 GetValue 的 byte。
    /// 用于 AlarmService 等需要浮点数/整形比较的场景。
    /// </summary>
    public double? GetDoubleValue(string key)
    {
        if (_typedValues.TryGetValue(key, out var dv))
            return dv;
        var bv = GetValue(key);
        return bv;
    }

    /// <summary>
    /// 将原始字节数组按 DataType 解码为 double。
    /// 支持: REAL(4) → S7.GetRealAt, LREAL(8) → BitConverter (大端),
    ///       DINT(4) → S7.GetDIntAt, INT(2) → S7.GetIntAt,
    ///       WORD(2) → S7.GetWordAt, BYTE(1) → buf[0]。
    /// </summary>
    private static double DecodeTypedValue(byte[] buf, string dataType)
    {
        return dataType.ToUpperInvariant() switch
        {
            "REAL" => S7.GetRealAt(buf, 0),
            "LREAL" => DecodeLReal(buf),
            "DINT" => S7.GetDIntAt(buf, 0),
            "INT" => S7.GetIntAt(buf, 0),
            "WORD" => S7.GetWordAt(buf, 0),
            _ => buf[0],
        };
    }

    /// <summary>
    /// 解码 8 字节 LReal (IEEE 754 double，大端序)。
    /// Sharp7 没有内置 LReal 解码，用 BitConverter 手动转换。
    /// </summary>
    private static double DecodeLReal(byte[] buf)
    {
        if (buf.Length < 8) return 0;
        // Siemens 是大端，如果 BitConverter 是小端需要翻转
        if (BitConverter.IsLittleEndian)
            return BitConverter.ToDouble([buf[7], buf[6], buf[5], buf[4], buf[3], buf[2], buf[1], buf[0]], 0);
        return BitConverter.ToDouble(buf, 0);
    }

    public void Dispose()
    {
        Stop();
    }
}
