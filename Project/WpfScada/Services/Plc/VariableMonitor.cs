using System.Timers;
using Microsoft.Extensions.Logging;
using Sharp7;
using Timer = System.Timers.Timer;

namespace WpfScada.Services.Plc;

public class VariableMonitor : IDisposable
{
    private readonly ILogger<VariableMonitor> _logger;
    private Timer? _timer;
    private volatile bool _busy;
    private volatile bool _disposed;
    private readonly S7Service _s7;

    public int DbNumber { get; set; }
    public int Offset { get; set; }
    public string DataType { get; set; } = "REAL";
    public int IntervalMs { get; set; } = 100;
    public string Key { get; set; } = "";
    public string? Label { get; set; }
    public bool IsRunning => _timer?.Enabled ?? false;
    public double LastValue { get; private set; }

    // ── 诊断 ──
    public string? LastError { get; private set; }
    public long TotalTicks { get; private set; }
    public long LongCycleCount { get; private set; }
    public int ConsecutiveFailures { get; private set; }
    public long LastDurationMs { get; private set; }
    public DateTime? LastStartedAt { get; private set; }
    public DateTime? LastCompletedAt { get; private set; }
    public DateTime? LastSuccessAt { get; private set; }

    public event Action<string, double, DateTime>? SampleGenerated;

    public VariableMonitor(ILogger<VariableMonitor> logger, S7Service s7)
    {
        _logger = logger;
        _s7 = s7;
    }

    public void Start()
    {
        Stop();
        _timer = new Timer(IntervalMs);
        _timer.Elapsed += OnTick;
        _timer.AutoReset = false;
        _timer.Start();
    }

    public void Stop()
    {
        _timer?.Stop();
        _timer?.Dispose();
        _timer = null;
    }

    private void OnTick(object? sender, ElapsedEventArgs e)
    {
        if (_busy || _disposed) return;
        _busy = true;
        var sw = System.Diagnostics.Stopwatch.StartNew();
        LastStartedAt = DateTime.Now;

        try
        {
            byte[]? buf = _s7.ReadBytesRaw(S7Service.AreaDB, Offset, GetDataTypeSize(), DbNumber);
            if (buf == null)
            {
                ConsecutiveFailures++;
                LastError = "ReadBytesRaw 返回 null";
                _logger.LogWarning("VariableMonitor[{Key}] 读取失败（连续 {Count} 次）",
                    Key, ConsecutiveFailures);
                return;
            }

            double val = DecodeValue(buf, DataType);
            LastValue = val;
            SampleGenerated?.Invoke(Key, val, DateTime.Now);

            // 恢复
            if (ConsecutiveFailures > 0)
            {
#pragma warning disable CA1873 // 恢复日志参数是简单值类型，开销可忽略
                _logger.LogInformation("VariableMonitor[{Key}] 已恢复，此前连续失败 {Count} 次",
                    Key, ConsecutiveFailures);
#pragma warning restore CA1873
            }
            ConsecutiveFailures = 0;
            LastError = null;
            LastSuccessAt = DateTime.Now;
        }
        catch (Exception ex)
        {
            ConsecutiveFailures++;
            LastError = ex.Message;
            _logger.LogWarning(ex, "VariableMonitor[{Key}] 异常（连续 {Count} 次失败）",
                Key, ConsecutiveFailures);
        }
        finally
        {
            sw.Stop();
            TotalTicks++;
            LastDurationMs = sw.ElapsedMilliseconds;
            LastCompletedAt = DateTime.Now;

            // 长周期告警
            long threshold = IntervalMs * 2;
            if (sw.ElapsedMilliseconds > threshold)
            {
                LongCycleCount++;
#pragma warning disable CA1873 // 长周期告警参数是简单值类型
                _logger.LogWarning("VariableMonitor[{Key}] 周期过长：{Duration}ms，超阈值 {Threshold}ms，累计 {Count} 次",
                    Key, sw.ElapsedMilliseconds, threshold, LongCycleCount);
#pragma warning restore CA1873
            }

            _busy = false;
            if (_timer != null && !_disposed)
            {
                try { _timer.Start(); } catch (Exception ex) { _logger.LogWarning(ex, "VariableMonitor[{Key}] Timer 重启失败", Key); }
            }
        }
    }

    private int GetDataTypeSize() => DataType switch
    {
        "REAL" => 4,
        "INT" => 2,
        "DINT" => 4,
        "WORD" => 2,
        "BYTE" => 1,
        _ => 4
    };

    private static double DecodeValue(byte[] buf, string type) => type switch
    {
        "REAL" => S7.GetRealAt(buf, 0),
        "INT" => S7.GetIntAt(buf, 0),
        "DINT" => S7.GetDIntAt(buf, 0),
        "WORD" => S7.GetWordAt(buf, 0),
        "BYTE" => buf[0],
        _ => S7.GetRealAt(buf, 0)
    };

    public void Dispose()
    {
        _disposed = true;
        Stop();
    }
}
