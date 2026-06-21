using System.Collections.Concurrent;
using System.Timers;
using Timer = System.Timers.Timer;

namespace TestWpf.Services;

/// <summary>
/// Mock 趋势数据生成器 — 每 100ms 产生 6 通道正弦波数据
/// 使用相对毫秒数（_elapsedMs）作为 X 轴值，避开 DateTime.Ticks 溢出问题
/// </summary>
public class MockTrendService : IDisposable
{
    private readonly Timer _timer;
    private readonly ConcurrentDictionary<string, double> _lastValues = new();
    private readonly Random _rng = new();
    private DateTime _startTime = DateTime.Now;
    private long _elapsedMs;  // 自启动以来的累计毫秒数，用于 X 轴

    public bool IsRunning { get; private set; }
    public event Action<string, double, DateTime>? SampleGenerated;

    public MockTrendService(int intervalMs = 100)
    {
        _timer = new Timer(intervalMs);
        _timer.Elapsed += (_, _) => Tick();
        _timer.AutoReset = true;
    }

    public void Start()
    {
        _startTime = DateTime.Now;
        _elapsedMs = 0;
        _timer.Start();
        IsRunning = true;
    }

    public void Stop()
    {
        _timer.Stop();
        IsRunning = false;
    }

    private void Tick()
    {
        _elapsedMs += (long)_timer.Interval;
        double t = _elapsedMs / 1000.0;
        DateTime now = DateTime.Now;

        Emit("ch_temp",   85 + Math.Sin(t * 0.05) * 8 + Math.Sin(t * 0.40) * 1.5, now);
        Emit("ch_press",   8 + Math.Sin(t * 0.07) * 1.5 + (_rng.NextDouble() - 0.5) * 0.3, now);
        Emit("ch_flow",   28 + Math.Sin(t * 0.04) * 6 + Math.Sin(t * 0.30) * 1, now);
        Emit("ch_level",  60 + Math.Sin(t * 0.03) * 18 + (_rng.NextDouble() - 0.5) * 2, now);
        Emit("ch_servo",  45 + Math.Sin(t * 0.10) * 30 + (_rng.NextDouble() - 0.5) * 3, now);
        Emit("ch_current",12 + Math.Sin(t * 0.15) * 4 + (_rng.NextDouble() - 0.5) * 1, now);
    }

    private void Emit(string key, double value, DateTime ts)
    {
        _lastValues[key] = value;
        SampleGenerated?.Invoke(key, value, ts);
    }

    public double? GetLastValue(string key)
        => _lastValues.TryGetValue(key, out var v) ? v : null;

    public void Dispose() => Stop();
}
