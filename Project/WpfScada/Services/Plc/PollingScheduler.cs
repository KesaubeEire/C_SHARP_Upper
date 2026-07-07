using System.Collections.Concurrent;
using System.Timers;
using Microsoft.Extensions.Logging;
using Sharp7;
using Timer = System.Timers.Timer;
using WpfScada.Controls.Plc;
using WpfScada.Models.Plc;

namespace WpfScada.Services.Plc;

public class PollingScheduler : IDisposable
{
    private readonly ILogger<PollingScheduler> _logger;
    private readonly PollingStore _store;
    private Timer? _timer;
    private volatile bool _busy;
    private S7Service? _s7;
    private int _dbIndex;
    private int _maxThisTick = 2;
    private readonly ConcurrentDictionary<string, byte> _lastValues = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, double> _typedValues = new(StringComparer.OrdinalIgnoreCase);

    public PollingScheduler(ILogger<PollingScheduler> logger, PollingStore store)
    {
        _logger = logger;
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
        _busy = false;

        _store.IsRunning = false;
        _store.StatusText = "已停止";
        _store.Quality = LedQuality.Disabled;
    }

    private void OnTimerElapsed(object? sender, ElapsedEventArgs e)
    {
        if (_s7 == null) return;

        if (_busy)
        {
            _store.MissedTicks++;
            _logger.LogWarning("轮询上一周期尚未完成，已跳过 {Count} 次",
                _store.MissedTicks);
            RestartTimer();
            return;
        }

        // 连续失败后退避：每隔一次跳过一个 tick
        if (_store.ConsecutiveFailures > 0)
        {
            _store.TotalTicks++;
            if (_store.TotalTicks % 2 == 1)
            {
#pragma warning disable CA1873 // 退避日志参数是简单值类型，开销可忽略
                _logger.LogInformation("轮询退避中，第 {N} 次跳过（连续 {F} 次失败）",
                    _store.TotalTicks, _store.ConsecutiveFailures);
#pragma warning restore CA1873
                RestartTimer();
                return;
            }
        }

        _busy = true;
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var updated = new HashSet<string>();
        _store.LastStartedAt = DateTime.Now;
        bool anyFailure = false;
        string? failureMessage = null;

        try
        {
            // Fast path: I/Q/M
            var fast = Config.Fast;
            if (!ReadFastArea(S7Service.AreaI, fast.PollIAddr, "I", updated, out var fastIFailure))
            {
                anyFailure = true;
                failureMessage ??= fastIFailure;
            }
            if (!ReadFastArea(S7Service.AreaQ, fast.PollQAddr, "Q", updated, out var fastQFailure))
            {
                anyFailure = true;
                failureMessage ??= fastQFailure;
            }
            if (!ReadFastArea(S7Service.AreaM, fast.PollMAddr, "M", updated, out var fastMFailure))
            {
                anyFailure = true;
                failureMessage ??= fastMFailure;
            }

            // DB polling (round-robin)
            if (_s7.IsConnected)
            {
                var enabled = Config.DbItems.Where(x => x.Enabled).ToList();
                int tickCount = 0;
                while (tickCount < _maxThisTick && enabled.Count > 0)
                {
                    var item = enabled[_dbIndex % enabled.Count];
                    _dbIndex++;
                    if (item.Length > 100) { _maxThisTick = 1; }
                    else { _maxThisTick = 2; }

                    byte[]? raw = _s7.ReadBytesRaw(S7Service.AreaDB, item.Offset, item.EffectiveLength, item.DbNumber);
                    if (raw != null)
                    {
                        for (int i = 0; i < item.EffectiveLength; i++)
                        {
                            string key = $"DB{item.DbNumber}[{item.Offset + i}]";
                            _lastValues[key] = raw[i];
                            updated.Add(key);
                        }

                        // 解析类型化值（非 BYTE 类型）
                        string typedKey = $"DB{item.DbNumber}:{item.Offset}";
                        double decoded = DecodeTypedValue(raw, item.DataType);
                        _typedValues[typedKey] = decoded;
                        updated.Add(typedKey);

                        item.Status = "OK";
                    }
                    else
                    {
                        string errorText = _s7.LastError ?? "未知错误";
                        item.Status = $"错误: {errorText}";
                        anyFailure = true;
#pragma warning disable CA1873 // 失败路径日志参数是简单值类型，开销可忽略
                        _logger.LogWarning(
                            "DB 轮询读取失败: DB{DbNumber} Offset={Offset} Length={Length} Error={Error}",
                            item.DbNumber,
                            item.Offset,
                            item.EffectiveLength,
                            errorText);
#pragma warning restore CA1873
                        failureMessage = $"DB{item.DbNumber}[{item.Offset}] 读取失败: {errorText}";
                    }
                    tickCount++;
                }
            }

            if (!anyFailure)
            {
                if (_store.ConsecutiveFailures > 0)
                {
#pragma warning disable CA1873 // 恢复日志参数是简单值类型
                    _logger.LogInformation("轮询已恢复，此前连续失败 {Count} 次",
                        _store.ConsecutiveFailures);
#pragma warning restore CA1873
                }
                _store.ConsecutiveFailures = 0;
                _store.LastError = null;
                _store.LastSuccessAt = DateTime.Now;
            }
            else
            {
                _store.ConsecutiveFailures++;
                _store.LastError = failureMessage ?? "轮询周期存在读取失败";
            }
        }
        catch (Exception ex)
        {
            _store.ConsecutiveFailures++;
            _store.LastError = ex.Message;
            anyFailure = true;
            _logger.LogWarning(ex, "轮询周期异常（连续 {Count} 次失败）",
                _store.ConsecutiveFailures);
        }
        finally
        {
            sw.Stop();
            _store.TotalTicks++;
            _store.LastDurationMs = sw.ElapsedMilliseconds;
            _store.LatencyMs = sw.ElapsedMilliseconds;
            _store.LastCompletedAt = DateTime.Now;

            // 长周期告警：耗时超过配置间隔的 2 倍
            long threshold = Config.FastInterval * 2;
            if (sw.ElapsedMilliseconds > threshold)
            {
                _store.LongCycleCount++;
#pragma warning disable CA1873 // 长周期告警参数是简单值类型，开销可忽略
                _logger.LogWarning("轮询周期过长：{Duration}ms，超过阈值 {Threshold}ms（间隔 {Interval}ms × 2），累计 {Count} 次",
                    sw.ElapsedMilliseconds, threshold, Config.FastInterval, _store.LongCycleCount);
#pragma warning restore CA1873
            }

            _busy = false;
            RestartTimer();
        }

        if (updated.Count > 0)
            DataUpdated?.Invoke(updated);
    }

    /// <summary>
    /// 安全重启 Timer。AutoReset=false 模式下必须显式调用 Start() 来触发下一 tick。
    /// </summary>
    private void RestartTimer()
    {
        if (_timer != null)
        {
            try { _timer.Start(); } catch (Exception ex) { _logger.LogWarning(ex, "Timer 重启失败"); }
        }
    }

    private bool ReadFastArea(int area, string addrStr, string prefix, HashSet<string> updated, out string? failureMessage)
    {
        failureMessage = null;
        if (_s7 == null || string.IsNullOrWhiteSpace(addrStr)) return true;
        var addrs = Config.Fast.ResolveAddr(addrStr);
        if (addrs.Length == 0) return true;

        bool success = true;
        foreach (var (start, count) in BuildContiguousGroups(addrs))
        {
            byte[]? buffer = _s7.ReadBytesRaw(area, start, count);
            if (buffer == null)
            {
                success = false;
                failureMessage = $"{prefix}{start}-{start + count - 1} 读取失败: {_s7.LastError ?? "未知错误"}";
#pragma warning disable CA1873 // 失败路径日志参数是简单值类型，开销可忽略
                _logger.LogWarning(
                    "快速区轮询读取失败: Area={Area} Start={Start} Count={Count} Error={Error}",
                    prefix,
                    start,
                    count,
                    _s7.LastError ?? "未知错误");
#pragma warning restore CA1873
                continue;
            }

            for (int i = 0; i < count; i++)
            {
                string key = $"{prefix}{start + i}";
                _lastValues[key] = buffer[i];
                updated.Add(key);
            }
        }

        return success;
    }

    private static List<(int Start, int Count)> BuildContiguousGroups(int[] byteAddresses)
    {
        var groups = new List<(int Start, int Count)>();
        if (byteAddresses.Length == 0) return groups;

        var sorted = byteAddresses.Distinct().OrderBy(a => a).ToArray();
        int start = sorted[0];
        int end = sorted[0];
        for (int i = 1; i < sorted.Length; i++)
        {
            if (sorted[i] == end + 1)
            {
                end = sorted[i];
                continue;
            }

            groups.Add((start, end - start + 1));
            start = sorted[i];
            end = sorted[i];
        }

        groups.Add((start, end - start + 1));
        return groups;
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
