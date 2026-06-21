using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using LiveChartsCore;
using LiveChartsCore.Defaults;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using LiveChartsCore.SkiaSharpView.Painting.Effects;
using SkiaSharp;

namespace TestWpf.ViewModels;

public class TrendPanelViewModel : INotifyPropertyChanged
{
    private readonly ObservableCollection<TrendSeries> _channels = new();
    private readonly Dictionary<string, ObservableCollection<DateTimePoint>> _buffers = new();

    public ObservableCollection<TrendSeries> Channels => _channels;
    public ObservableCollection<ISeries> Series { get; } = new();
    public ObservableCollection<ISeries> BarSeries { get; } = new();
    public ObservableCollection<Models.TrendChannelConfig> ChannelConfigs { get; } = new();

    // 水平轴仪表
    private double _gaugePosition = 45;
    public double GaugePosition { get => _gaugePosition; set { _gaugePosition = value; OnChanged(); } }

    private double _gaugeLeft = 0;
    public double GaugeLeft { get => _gaugeLeft; set { _gaugeLeft = value; OnChanged(); } }

    private double _gaugeRight = 100;
    public double GaugeRight { get => _gaugeRight; set { _gaugeRight = value; OnChanged(); } }

    private int _gaugeTicks = 10;
    public int GaugeTicks { get => _gaugeTicks; set { _gaugeTicks = value; OnChanged(); } }

    private readonly int _maxPoints = 300; // 保留最近 300 个点

    public TrendPanelViewModel()
    {
        // 4 个默认通道
        var defaults = new (string key, string label, string color, string unit, double min, double max)[]
        {
            ("ch_temp", "Reactor Temp", "#E24B4A", "°C", 60, 110),
            ("ch_press", "Pressure", "#37D3E0", "bar", 0, 16),
            ("ch_flow", "Feed Flow", "#1D9E75", "m³/h", 0, 50),
            ("ch_level", "Tank Level", "#F4D03F", "%", 0, 100),
        };

        foreach (var (key, label, color, unit, min, max) in defaults)
        {
            var buf = new ObservableCollection<DateTimePoint>();
            _buffers[key] = buf;

            var cfg = new Models.TrendChannelConfig
            {
                Key = key, Label = label, Color = color,
                Unit = unit, Min = min, Max = max,
                Variable = $"{key}"
            };
            ChannelConfigs.Add(cfg);

            var series = new TrendSeries
            {
                Key = key,
                Values = buf,
                Stroke = new SolidColorPaint(SKColor.Parse(color)) { StrokeThickness = 2 },
                Fill = null,
                GeometrySize = 0,  // 无点标记
                LineSmoothness = 0.3,
            };
            Series.Add(series);
            _channels.Add(series);
        }

        // 柱状图 — 每个通道当前值
        BarSeries.Add(new ColumnSeries<double>
        {
            Values = new ObservableCollection<double> { 0, 0, 0, 0, 0, 0 },
            Stroke = null,
            Fill = new SolidColorPaint(SKColors.CornflowerBlue),
            Padding = 2,
        });
    }

    public void AddSample(string key, double value, DateTime ts)
    {
        if (!_buffers.TryGetValue(key, out var buf)) return;

        buf.Add(new DateTimePoint(ts, value));
        while (buf.Count > _maxPoints) buf.RemoveAt(0);

        // 更新柱状图（取最近6个通道值）
        if (key == "ch_temp" || key == "ch_press" || key == "ch_flow" || key == "ch_level"
            || key == "ch_servo" || key == "ch_current")
        {
            var barValues = (ColumnSeries<double>)BarSeries[0];
            var vals = barValues.Values as ObservableCollection<double>;
            if (vals == null) return;

            int idx = key switch
            {
                "ch_temp" => 0, "ch_press" => 1, "ch_flow" => 2,
                "ch_level" => 3, "ch_servo" => 4, "ch_current" => 5,
                _ => -1
            };
            if (idx >= 0 && idx < vals.Count)
            {
                vals[idx] = value;
                barValues.Values = vals; // trigger refresh
            }
        }

        // 更新伺服位置
        if (key == "ch_servo")
            GaugePosition = value;
    }

    public Axis XAxis => new Axis
    {
        Labeler = l => new DateTime((long)l).ToString("HH:mm:ss"),
        MaxLimit = null, // auto-scroll
        MinLimit = null,
    };

    public Axis YAxis => new Axis
    {
        MinLimit = null,
        MaxLimit = null,
        Labeler = l => $"{l:F1}",
    };

    public ISeries GetBarGaugeSeries()
    {
        return new ColumnSeries<double>
        {
            Values = new ObservableCollection<double> { GaugePosition },
            Stroke = null,
            Fill = new SolidColorPaint(SKColor.Parse("#3498DB")),
            MaxBarWidth = 40,
        };
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnChanged([CallerMemberName] string? n = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}

public class TrendSeries : LineSeries<DateTimePoint>
{
    public string Key { get; set; } = "";
}
