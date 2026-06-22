using System.Timers;
using Sharp7;
using Timer = System.Timers.Timer;

namespace Wpf.Ui.Gallery.Services.Plc;

public class VariableMonitor : IDisposable
{
    private Timer? _timer;
    private volatile bool _busy;
    private readonly S7Service _s7;

    public int DbNumber { get; set; }
    public int Offset { get; set; }
    public string DataType { get; set; } = "REAL";
    public int IntervalMs { get; set; } = 100;
    public string Key { get; set; } = "";
    public string? Label { get; set; }
    public bool IsRunning => _timer?.Enabled ?? false;
    public double LastValue { get; private set; }
    public string? LastError { get; private set; }

    public event Action<string, double, DateTime>? SampleGenerated;

    public VariableMonitor(S7Service s7)
    {
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
        if (_busy) return;
        _busy = true;

        try
        {
            byte[]? buf = _s7.ReadBytesRaw(S7Service.AreaDB, Offset, GetDataTypeSize(), DbNumber);
            if (buf == null) return;

            double val = DecodeValue(buf, DataType);
            LastValue = val;
            SampleGenerated?.Invoke(Key, val, DateTime.Now);
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
        }
        finally
        {
            _busy = false;
            if (_timer != null)
            {
                try { _timer.Start(); } catch { }
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
        Stop();
    }
}
