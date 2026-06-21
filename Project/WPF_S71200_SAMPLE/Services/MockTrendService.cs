using System.Collections.Concurrent;
using System.Timers;
using TestWpf.Models;
using Timer = System.Timers.Timer;

namespace TestWpf.Services;

/// <summary>
/// Mock 趋势数据生成器（对标 Trioop mockData.ts）
/// 每 100ms 产生 4 个通道的正弦波/随机游走数据
/// </summary>
public class MockTrendService : IDisposable
{
    private readonly Timer _timer;
    private readonly ConcurrentDictionary<string, double> _lastValues = new();
    private readonly Random _rng = new();
    private DateTime _startTime = DateTime.Now;

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
        double t = (DateTime.Now - _startTime).TotalSeconds;

        // 4 个预设通道，模拟不同物理量
        Emit("ch_temp", "Reactor Temp", 85 + Math.Sin(t * 0.05) * 8 + Math.Sin(t * 0.4) * 1.5, DateTime.Now);
        Emit("ch_press", "Pressure", 8 + Math.Sin(t * 0.07) * 1.5 + (_rng.NextDouble() - 0.5) * 0.3, DateTime.Now);
        Emit("ch_flow", "Feed Flow", 28 + Math.Sin(t * 0.04) * 6 + Math.Sin(t * 0.3) * 1, DateTime.Now);
        Emit("ch_level", "Tank Level", 60 + Math.Sin(t * 0.03) * 18 + (_rng.NextDouble() - 0.5) * 2, DateTime.Now);

        // 伺服位置（正弦 + 随机）
        Emit("ch_servo", "Servo Pos", 45 + Math.Sin(t * 0.1) * 30 + (_rng.NextDouble() - 0.5) * 3, DateTime.Now);
        Emit("ch_current", "Motor Current", 12 + Math.Sin(t * 0.15) * 4 + (_rng.NextDouble() - 0.5) * 1, DateTime.Now);
    }

    private void Emit(string key, string label, double value, DateTime ts)
    {
        _lastValues[key] = value;
        SampleGenerated?.Invoke(key, value, ts);
    }

    public double? GetLastValue(string key)
        => _lastValues.TryGetValue(key, out var v) ? v : null;

    public void Dispose() => Stop();
}
