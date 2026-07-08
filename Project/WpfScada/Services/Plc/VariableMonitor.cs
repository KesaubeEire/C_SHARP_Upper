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

    // 诊断
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
        _disposed = false;
        _timer = new Timer(IntervalMs)
        {
            AutoReset = false,
        };
        _timer.Elapsed += OnTick;
        _timer.Start();
    }

    public void Stop()
    {
        _disposed = true;
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
            byte[]? buf = _s7.ReadBytesRaw(S7Service.AreaDB, Offset, S7Service.GetDataTypeSize(DataType), DbNumber);
            if (buf == null)
            {
                ConsecutiveFailures++;
                LastError = "ReadBytesRaw 返回 null";
                SafeLog(() => _logger.LogWarning("VariableMonitor[{Key}] 读取失败（连续 {Count} 次）", Key, ConsecutiveFailures));
                return;
            }

            double val = S7Service.DecodeValue(buf, DataType);
            LastValue = val;
            SampleGenerated?.Invoke(Key, val, DateTime.Now);

            if (ConsecutiveFailures > 0 && !_disposed)
            {
#pragma warning disable CA1873
                SafeLog(() => _logger.LogInformation("VariableMonitor[{Key}] 已恢复，此前连续失败 {Count} 次", Key, ConsecutiveFailures));
#pragma warning restore CA1873
            }
            ConsecutiveFailures = 0;
            LastError = null;
            LastSuccessAt = DateTime.Now;
        }
        catch (Exception ex) when (!_disposed)
        {
            ConsecutiveFailures++;
            LastError = ex.Message;
            SafeLog(() => _logger.LogWarning(ex, "VariableMonitor[{Key}] 异常（连续 {Count} 次失败）", Key, ConsecutiveFailures));
        }
        catch
        {
            // 关闭时 _logger 已被释放，什么都做不了，安静退出
        }
        finally
        {
            sw.Stop();
            TotalTicks++;
            LastDurationMs = sw.ElapsedMilliseconds;
            LastCompletedAt = DateTime.Now;

            long threshold = IntervalMs * 2;
            if (!_disposed && sw.ElapsedMilliseconds > threshold)
            {
                LongCycleCount++;
#pragma warning disable CA1873
                SafeLog(() => _logger.LogWarning("VariableMonitor[{Key}] 周期过长：{Duration}ms，超阈值 {Threshold}ms，累计 {Count} 次",
                    Key, sw.ElapsedMilliseconds, threshold, LongCycleCount));
#pragma warning restore CA1873
            }

            _busy = false;
            if (_timer != null && !_disposed)
            {
                try { _timer.Start(); }
                catch { /* 关闭时忽略 */ }
            }
        }
    }

    /// <summary>关闭时 logger 可能已被 DI 释放，吞掉所有日志异常。</summary>
    private void SafeLog(Action logAction)
    {
        if (_disposed) return;
        try { logAction(); }
        catch { /* 关闭时 logger 可能已释放，不抛 */ }
    }

    public void Dispose()
    {
        _disposed = true;
        Stop();
    }
}
