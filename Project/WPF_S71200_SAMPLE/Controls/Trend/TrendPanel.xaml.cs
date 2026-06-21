using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using LiveChartsCore;
using LiveChartsCore.Defaults;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using LiveChartsCore.SkiaSharpView.WPF;
using SkiaSharp;
using TestWpf.Models;
using TestWpf.Services;

namespace TestWpf.Controls.Trend;

/// <summary>
/// 趋势图面板 — 通道列表 + 趋势图 + 时间范围 + 水平仪表 + 柱状图
/// </summary>
public partial class TrendPanel : UserControl
{
    private readonly MockTrendService _mockTrend = new(100);
    private readonly ObservableCollection<TrendChannelConfig> _trendChannels = [];
    private readonly ObservableCollection<ISeries> _trendSeries = [];
    private readonly ObservableCollection<ISeries> _barSeriesColl = [];
    private readonly Dictionary<string, ObservableCollection<DateTimePoint>> _trendNormBuffers = [];

    private static readonly (string label, int windowMs)[] TimeRangeOptions = [
        ("1 分钟",   60_000), ("5 分钟",   300_000), ("30 分钟",  1_800_000),
        ("1 小时",   3_600_000), ("12 小时",  43_200_000), ("24 小时",  86_400_000),
    ];
    private int _trendTimeWindowMs = 60_000;
    private const int MaxBufferPoints = 864000;

    private bool _gaugeDrawn;

    private static readonly SKColor[] TrendColors = [
        SKColors.Crimson, SKColors.Cyan, SKColors.SeaGreen, SKColors.Gold,
        SKColors.DodgerBlue, SKColors.MediumPurple
    ];

    public TrendPanel()
    {
        InitializeComponent();
        InitTrend();
    }

    private void InitTrend()
    {
        var defs = new (string key, string label, string color, string unit, double min, double max)[]
        {
            ("ch_temp",   "Reactor Temp",   "#E24B4A", "°C",    60.0, 110.0),
            ("ch_press",  "Pressure",        "#37D3E0", "bar",    0.0,  16.0),
            ("ch_flow",   "Feed Flow",       "#1D9E75", "m³/h",   0.0,  50.0),
            ("ch_level",  "Tank Level",      "#F4D03F", "%",      0.0, 100.0),
            ("ch_servo",  "Servo Pos",       "#3498DB", "mm",   -10.0,  90.0),
            ("ch_current","Motor Current",   "#9B59B6", "A",      0.0,  25.0),
        };
        int idx = 0;
        foreach (var (key, label, color, unit, min, max) in defs)
        {
            var buf = new ObservableCollection<DateTimePoint>();
            _trendNormBuffers[key] = buf;
            _trendChannels.Add(new TrendChannelConfig
            {
                Key = key, Label = label, Color = color, Unit = unit,
                Min = min, Max = max, Variable = key
            });
            double range = max - min;
            _trendSeries.Add(new LineSeries<DateTimePoint>
            {
                Values = buf,
                Stroke = new SolidColorPaint(TrendColors[idx++ % TrendColors.Length]) { StrokeThickness = 2 },
                Fill = null, GeometrySize = 0, LineSmoothness = 0.3, Name = label
            });
        }
        listTrendChannels.ItemsSource = _trendChannels;
        cmbTrendTimeRange.ItemsSource = TimeRangeOptions.Select(o => o.label).ToList();
        cmbTrendTimeRange.SelectedIndex = 0;
        _trendTimeWindowMs = TimeRangeOptions[0].windowMs;
        cartesianTrend.Series = _trendSeries;
        cartesianTrend.YAxes = [new Axis { MinLimit = 0, MaxLimit = 100, Labeler = _ => "" }];
        cartesianTrend.TooltipPosition = LiveChartsCore.Measure.TooltipPosition.Top;
        UpdateTrendXAxis();

        var barVals = new ObservableCollection<double> { 0, 0, 0, 0, 0, 0 };
        _barSeriesColl.Add(new ColumnSeries<double>
        {
            Values = barVals,
            Fill = new SolidColorPaint(SKColor.Parse("#3498DB")),
            Padding = 2, MaxBarWidth = 40
        });
        cartesianBars.Series = _barSeriesColl;
        cartesianBars.XAxes = [new Axis
        {
            Labels = ["Temp", "Press", "Flow", "Level", "Servo", "Curr."],
            LabelsRotation = 45
        }];
        cartesianBars.YAxes = [new Axis { MinLimit = 0 }];

        _mockTrend.SampleGenerated += OnSample;
        _gaugeDrawn = false;
    }

    private void OnSample(string key, double val, DateTime ts)
    {
        Dispatcher.Invoke(() =>
        {
            var cfg = _trendChannels.FirstOrDefault(c => c.Key == key);
            if (cfg == null) return;
            double range = cfg.Max - cfg.Min;
            double norm = range > 0 ? Math.Clamp((val - cfg.Min) / range * 100.0, 0, 100) : 50;
            if (!_trendNormBuffers.TryGetValue(key, out var buf)) return;
            buf.Add(new DateTimePoint(ts, norm));
            while (buf.Count > MaxBufferPoints) buf.RemoveAt(0);
            cfg.CurrentValue = val;
            SlideTrendXAxis();

            var col = (ColumnSeries<double>)_barSeriesColl[0];
            var vals = (ObservableCollection<double>)col.Values!;
            int bi = key switch
            {
                "ch_temp" => 0, "ch_press" => 1, "ch_flow" => 2,
                "ch_level" => 3, "ch_servo" => 4, "ch_current" => 5, _ => -1
            };
            if (bi >= 0) vals[bi] = val;
            if (key == "ch_servo") UpdateGaugeNeedle(val);
        });
    }

    // ===== Mock 控制 =====

    private void OnMockToggle(object sender, RoutedEventArgs e)
    {
        if (_mockTrend.IsRunning)
        {
            _mockTrend.Stop();
            btnTrendMock.Content = "▶ 启动 Mock";
        }
        else
        {
            _mockTrend.Start();
            btnTrendMock.Content = "■ 停止 Mock";
        }
    }

    // ===== 时间范围 =====

    private void OnTimeRangeChanged(object sender, SelectionChangedEventArgs e)
    {
        int idx = cmbTrendTimeRange.SelectedIndex;
        if (idx < 0 || idx >= TimeRangeOptions.Length) return;
        _trendTimeWindowMs = TimeRangeOptions[idx].windowMs;
        UpdateTrendXAxis();
    }

    private void UpdateTrendXAxis()
    {
        double n = DateTime.Now.Ticks;
        cartesianTrend.XAxes = [MakeAxis(n - _trendTimeWindowMs * 10_000, n)];
    }

    private void SlideTrendXAxis()
    {
        double n = DateTime.Now.Ticks;
        cartesianTrend.XAxes = [MakeAxis(n - _trendTimeWindowMs * 10_000, n)];
    }

    private static Axis MakeAxis(double min, double max) => new Axis
    {
        MinLimit = min, MaxLimit = max,
        Labeler = v => v <= 0 || v > 1e18 ? "" : new DateTime((long)v).ToLocalTime().ToString("HH:mm:ss")
    };

    // ===== 水平轴仪表 =====

    public void DrawGaugeScale(double initialPos)
    {
        var c = canvasGauge;
        if (c.ActualWidth < 10) return;
        double w = c.ActualWidth, h = c.ActualHeight;
        double left = double.TryParse(txtGaugeLeft.Text, out var l) ? l : 0;
        double right = double.TryParse(txtGaugeRight.Text, out var r) ? r : 100;
        int ticks = int.TryParse(txtGaugeTicks.Text, out var tc) ? tc : 10;
        if (ticks < 2) ticks = 2;
        c.Children.Clear();
        double pad = 20, drawW = w - pad * 2, range = right - left;
        if (range <= 0) range = 100;
        for (int i = 0; i <= ticks; i++)
        {
            double frac = (double)i / ticks, x = pad + drawW * frac;
            c.Children.Add(new Line
            {
                X1 = x, Y1 = 0, X2 = x, Y2 = h * 0.35,
                Stroke = new SolidColorBrush(Color.FromRgb(0x88, 0x88, 0x88)),
                StrokeThickness = 1
            });
            if (ticks <= 10 || i % 2 == 0)
            {
                var lbl = new TextBlock
                {
                    Text = $"{left + range * frac:F0}", FontSize = 8,
                    Foreground = new SolidColorBrush(Color.FromRgb(0x88, 0x88, 0x88))
                };
                Canvas.SetLeft(lbl, x - 12);
                Canvas.SetTop(lbl, h * 0.38);
                c.Children.Add(lbl);
            }
        }
        c.Tag = (pad, drawW, range, left, h);
        UpdateGaugeNeedle(initialPos);
    }

    public void UpdateGaugeNeedle(double pos)
    {
        var c = canvasGauge;
        if (c.Tag is not (double pad, double drawW, double range, double left, double h)) return;
        txtGaugePos.Text = $"{pos:F1} mm";
        while (c.Children.Count > 3) c.Children.RemoveAt(c.Children.Count - 1);
        double nFrac = Math.Clamp((pos - left) / range, 0, 1), nx = pad + drawW * nFrac;
        c.Children.Add(new Rectangle
        {
            Width = 3, Height = h * 0.55,
            Fill = new SolidColorBrush(Color.FromRgb(0xE7, 0x4C, 0x3C)),
            RadiusX = 2, RadiusY = 2
        });
        Canvas.SetTop(c.Children[^1], h * 0.4);
        Canvas.SetLeft(c.Children[^1], nx - 1.5);
        c.Children.Add(new Polygon
        {
            Points = new PointCollection { new(nx - 5, h * 0.4 + 10), new(nx + 5, h * 0.4 + 10), new(nx, h * 0.4 - 2) },
            Fill = new SolidColorBrush(Color.FromRgb(0xE7, 0x4C, 0x3C))
        });
        var vl = new TextBlock
        {
            Text = $"{pos:F1}", FontSize = 10, FontWeight = FontWeights.Bold,
            Foreground = new SolidColorBrush(Color.FromRgb(0xE7, 0x4C, 0x3C))
        };
        Canvas.SetLeft(vl, Math.Clamp(nx - 15, 0, c.ActualWidth - 35));
        Canvas.SetTop(vl, 0);
        c.Children.Add(vl);
    }

    public void NeedsGaugeDraw(double initialPos)
    {
        if (!_gaugeDrawn && canvasGauge.ActualWidth >= 10)
        {
            _gaugeDrawn = true;
            DrawGaugeScale(initialPos);
        }
    }

    public void Stop()
    {
        _mockTrend.SampleGenerated -= OnSample;
        _mockTrend.Stop();
    }
}
