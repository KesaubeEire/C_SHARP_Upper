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
///
/// 数据模型：ObservableValue（只有 Y 值，X 自动按序号索引）
///   - 每个数据点只存归一化的 double，X 轴 = 点在缓冲中的序号
///   - 时间窗口 → 点数：100ms 间隔下 1min=600点, 5min=3000点, ...
///   - X 轴标签显示相对时间：索引 × 100ms → "mm:ss" 格式
/// </summary>
public partial class TrendPanel : UserControl
{
    private readonly MockTrendService _mockTrend = new(100);
    private readonly ObservableCollection<TrendChannelConfig> _trendChannels = [];
    private readonly ObservableCollection<ISeries> _trendSeries = [];
    private readonly ObservableCollection<ISeries> _barSeriesColl = [];
    private readonly Dictionary<string, ObservableCollection<ObservableValue>> _trendBuffers = [];

    /// <summary>Mock 数据间隔，用于 X 轴标签的时间推算</summary>
    private const int MockIntervalMs = 100;

    /// <summary>时间范围选项：标签 / 毫秒窗口 / 对应点数(100ms间隔)</summary>
    private static readonly (string label, int windowMs, int maxPoints)[] TimeRangeOptions = [
        ("1 分钟",   60_000,     600),
        ("5 分钟",   300_000,    3_000),
        ("30 分钟",  1_800_000,  18_000),
        ("1 小时",   3_600_000,  36_000),
        ("12 小时",  43_200_000, 432_000),
        ("24 小时",  86_400_000, 864_000),
    ];
    private int _trendWindowPoints = 600;   // 当前窗口对应的最大点数

    /// <summary>缓存的 X 轴引用，避免每帧重建 Axis[]</summary>
    private Axis? _cachedXAxis;

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

    // ===== 公开 API：外部数据源接入 =====

    /// <summary>
    /// 注册一个动态通道（例如来自 DB 变量监控的数据）。
    /// 自动扩展柱状图。
    /// </summary>
    public void AddChannel(string key, string label, double min, double max, string unit, SKColor color)
    {
        if (_trendBuffers.ContainsKey(key)) return;

        var buf = new ObservableCollection<ObservableValue>();
        _trendBuffers[key] = buf;
        _trendChannels.Add(new TrendChannelConfig
        {
            Key = key, Label = label, Color = color.ToString(), Unit = unit,
            Min = min, Max = max, Variable = key
        });
        _trendSeries.Add(new LineSeries<ObservableValue>
        {
            Values = buf,
            Stroke = new SolidColorPaint(color) { StrokeThickness = 2 },
            Fill = null, GeometrySize = 0, LineSmoothness = 0.3, Name = label
        });

        // 扩展柱状图（追加一条柱 + 标签）
        var col = (ColumnSeries<double>)_barSeriesColl[0];
        var vals = (ObservableCollection<double>)col.Values!;
        vals.Add(0);
        var axis = cartesianBars.XAxes?.FirstOrDefault() as Axis;
        if (axis?.Labels != null)
        {
            var labels = axis.Labels.ToList();
            labels.Add(label);
            axis.Labels = labels;
        }
    }

    /// <summary>
    /// 从外部数据源（VariableMonitor 等）喂数据。
    /// 自动归一化、缓冲、裁剪、滑动 X 轴、更新柱状图。
    /// </summary>
    public void FeedData(string key, double val, DateTime ts)
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.Invoke(() => FeedData(key, val, ts));
            return;
        }

        var cfg = _trendChannels.FirstOrDefault(c => c.Key == key);
        if (cfg == null) return;
        double range = cfg.Max - cfg.Min;
        double norm = range > 0 ? Math.Clamp((val - cfg.Min) / range * 100.0, 0, 100) : 50;
        if (!_trendBuffers.TryGetValue(key, out var buf)) return;
        buf.Add(new ObservableValue(norm));
        TrimBuffer(buf);
        cfg.CurrentValue = val;
        SlideTrendXAxis();

        // 更新柱状图：把所有通道的最新值填入
        var col = (ColumnSeries<double>)_barSeriesColl[0];
        var vals = (ObservableCollection<double>)col.Values!;
        int idx = _trendChannels.IndexOf(cfg);
        if (idx >= 0 && idx < vals.Count)
            vals[idx] = val;
    }

    // ===== 初始化 =====

    private void InitTrend()
    {
        // 去掉 temp/press/level，保留 flow/servo/current + DB 通道动态添加
        var defs = new (string key, string label, string color, string unit, double min, double max)[]
        {
            ("ch_flow",   "Feed Flow",       "#1D9E75", "m³/h",   0.0,  50.0),
            ("ch_servo",  "Servo Pos",       "#3498DB", "mm",   -10.0,  90.0),
            ("ch_current","Motor Current",   "#9B59B6", "A",      0.0,  25.0),
        };
        int idx = 0;
        foreach (var (key, label, color, unit, min, max) in defs)
        {
            var buf = new ObservableCollection<ObservableValue>();
            _trendBuffers[key] = buf;
            _trendChannels.Add(new TrendChannelConfig
            {
                Key = key, Label = label, Color = color, Unit = unit,
                Min = min, Max = max, Variable = key
            });
            _trendSeries.Add(new LineSeries<ObservableValue>
            {
                Values = buf,
                Stroke = new SolidColorPaint(TrendColors[idx++ % TrendColors.Length]) { StrokeThickness = 2 },
                Fill = null, GeometrySize = 0, LineSmoothness = 0.3, Name = label
            });
        }
        listTrendChannels.ItemsSource = _trendChannels;
        cmbTrendTimeRange.ItemsSource = TimeRangeOptions.Select(o => o.label).ToList();
        cmbTrendTimeRange.SelectedIndex = 0;
        _trendWindowPoints = TimeRangeOptions[0].maxPoints;
        cartesianTrend.Series = _trendSeries;

        // ── Y 轴配置 ──
        cartesianTrend.YAxes = [
            new Axis
            {
                MinLimit = 0,
                MaxLimit = 100,
                Labeler = value => $"{value:F0}%",
                ShowSeparatorLines = true,
                SeparatorsPaint = new SolidColorPaint(SKColors.Gray) { StrokeThickness = 0.5f },
            }
        ];
        cartesianTrend.TooltipPosition = LiveChartsCore.Measure.TooltipPosition.Top;
        cartesianTrend.TooltipBackgroundPaint = new SolidColorPaint(new SKColor(40, 40, 40, 230));

        // 首次创建 X 轴（基于点数，非 DateTime）
        _cachedXAxis = MakeAxis(0, _trendWindowPoints, _trendWindowPoints);
        cartesianTrend.XAxes = [_cachedXAxis];

        var barVals = new ObservableCollection<double> { 0, 0, 0 };
        _barSeriesColl.Add(new ColumnSeries<double>
        {
            Values = barVals,
            Fill = new SolidColorPaint(SKColor.Parse("#3498DB")),
            Padding = 2, MaxBarWidth = 40
        });
        cartesianBars.Series = _barSeriesColl;
        cartesianBars.XAxes = [new Axis
        {
            Labels = ["Flow", "Servo", "Curr."],
            LabelsRotation = 45,
            LabelsPaint = new SolidColorPaint(SKColors.LightGray),
        }];
        cartesianBars.YAxes = [new Axis
        {
            MinLimit = 0,
            Labeler = value => $"{value:F0}",
            SeparatorsPaint = new SolidColorPaint(SKColors.Gray) { StrokeThickness = 0.5f },
        }];
        cartesianBars.TooltipPosition = LiveChartsCore.Measure.TooltipPosition.Top;

        _mockTrend.SampleGenerated += OnSample;
        _gaugeDrawn = false;
    }

    // ===== 数据到达 =====

    private void OnSample(string key, double val, DateTime ts)
    {
        Dispatcher.Invoke(() =>
        {
            var cfg = _trendChannels.FirstOrDefault(c => c.Key == key);
            if (cfg == null) return;
            double range = cfg.Max - cfg.Min;
            double norm = range > 0 ? Math.Clamp((val - cfg.Min) / range * 100.0, 0, 100) : 50;
            if (!_trendBuffers.TryGetValue(key, out var buf)) return;

            buf.Add(new ObservableValue(norm));
            TrimBuffer(buf);
            cfg.CurrentValue = val;
            SlideTrendXAxis();

            var col = (ColumnSeries<double>)_barSeriesColl[0];
            var vals = (ObservableCollection<double>)col.Values!;
            // 更新柱状图：按通道在列表中的动态索引
            int bi = _trendChannels.IndexOf(cfg);
            if (bi >= 0 && bi < vals.Count) vals[bi] = val;
            if (key == "ch_servo") UpdateGaugeNeedle(val);
        });
    }

    /// <summary>
    /// 裁剪缓冲：只保留当前窗口 2 倍的点数。
    /// ObservableValue 没有时间戳，直接用 Count 判断。
    /// </summary>
    private void TrimBuffer(ObservableCollection<ObservableValue> buf)
    {
        int maxKeep = _trendWindowPoints * 2;
        while (buf.Count > maxKeep)
            buf.RemoveAt(0);
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
        _trendWindowPoints = TimeRangeOptions[idx].maxPoints;

        // 重建 X 轴：范围 0 ~ maxPoints，标签格式按窗口自适应
        _cachedXAxis = MakeAxis(0, _trendWindowPoints, _trendWindowPoints);
        cartesianTrend.XAxes = [_cachedXAxis];
    }

    /// <summary>
    /// 创建 X 轴实例。
    /// X 轴数据 = 点的序号索引（0, 1, 2, ...），
    /// Labeler 将序号转为相对时间字符串（索引 × 100ms）。
    /// </summary>
    private static Axis MakeAxis(double min, double max, int windowPoints)
    {
        // 根据窗口大小选择时间格式
        // 每点 = 100ms，所以 totalSeconds = windowPoints / 10
        int totalSeconds = windowPoints / 10;
        string format = totalSeconds switch
        {
            <= 300    => @"mm\:ss",       // ≤5分钟：分:秒
            <= 3600   => @"mm\:ss",       // ≤1小时：分:秒
            _         => @"hh\:mm\:ss",   // >1小时：时:分:秒
        };

        return new Axis
        {
            MinLimit = min,
            MaxLimit = max,
            // 序号 → 相对时间：索引 × 100ms
            Labeler = v =>
            {
                long ms = (long)(v * MockIntervalMs);
                var span = TimeSpan.FromMilliseconds(ms);
                // 超过1小时显示 hh:mm:ss，否则显示 mm:ss
                if (span.TotalHours >= 1)
                    return span.ToString(@"hh\:mm\:ss");
                return span.ToString(@"mm\:ss");
            },
        };
    }

    /// <summary>
    /// 滑动 X 轴窗口。
    /// 以当前所有通道中最大的点数为 MaxLimit，往前推窗口大小。
    /// </summary>
    private void SlideTrendXAxis()
    {
        if (_cachedXAxis == null) return;

        // 找到所有通道中最新的点数
        int maxCount = 0;
        foreach (var buf in _trendBuffers.Values)
        {
            if (buf.Count > maxCount) maxCount = buf.Count;
        }

        _cachedXAxis.MinLimit = Math.Max(0, maxCount - _trendWindowPoints);
        _cachedXAxis.MaxLimit = Math.Max(maxCount, _trendWindowPoints);
    }

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
