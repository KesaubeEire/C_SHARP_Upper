namespace WpfScada.Services.Plc;

public class MockTrendService : IDisposable
{
    private System.Timers.Timer? _timer;
    private long _t;
    private readonly Random _rng = new();

    public bool IsRunning => _timer?.Enabled ?? false;

    public event Action<string, double, DateTime>? DataGenerated;

    public void Start(int intervalMs = 100)
    {
        Stop();
        _timer = new System.Timers.Timer(intervalMs);
        _timer.Elapsed += OnTick;
        _timer.AutoReset = true;
        _timer.Start();
    }

    public void Stop()
    {
        _timer?.Stop();
        _timer?.Dispose();
        _timer = null;
    }

    private void OnTick(object? sender, System.Timers.ElapsedEventArgs e)
    {
        _t++;
        double t = _t;

        Generate("ch_temp", 85 + Math.Sin(t * 0.05) * 8 + Math.Sin(t * 0.40) * 1.5);
        Generate("ch_press", 8 + Math.Sin(t * 0.07) * 1.5 + _rng.NextDouble() * 0.3);
        Generate("ch_flow", 28 + Math.Sin(t * 0.04) * 6 + Math.Sin(t * 0.30) * 1);
        Generate("ch_level", 60 + Math.Sin(t * 0.03) * 18 + _rng.NextDouble() * 2);
        Generate("ch_servo", 45 + Math.Sin(t * 0.10) * 30 + _rng.NextDouble() * 3);
        Generate("ch_current", 12 + Math.Sin(t * 0.15) * 4 + _rng.NextDouble() * 1);
    }

    private void Generate(string key, double value)
    {
        DataGenerated?.Invoke(key, value, DateTime.Now);
    }

    public void Dispose()
    {
        Stop();
    }
}
