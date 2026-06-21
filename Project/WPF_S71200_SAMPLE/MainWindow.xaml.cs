using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using LiveChartsCore;
using LiveChartsCore.Defaults;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Extensions;
using LiveChartsCore.SkiaSharpView.Painting;
using LiveChartsCore.SkiaSharpView.WPF;
using SkiaSharp;
using TestWpf.Models;
using TestWpf.Controls;
using TestWpf.Services;

namespace TestWpf;

public partial class MainWindow : Window
{
    // ─── 手动模式 ───
    private readonly S7Service _plc = new();
    private readonly ObservableCollection<ByteRowViewModel> _iRows = [];
    private readonly ObservableCollection<ByteRowViewModel> _qRows = [];
    private readonly ObservableCollection<ByteRowViewModel> _mRows = [];
    private Dictionary<int, byte> _lastIBytes = [], _lastQBytes = [], _lastMBytes = [];
    private bool _qWriteMode, _mWriteMode;

    // ─── 轮询 ───
    private readonly PollingScheduler _scheduler = new();

    // ─── 导入 DB/UDT ───
    private readonly ObservableCollection<DbStructure> _importedDbs = [];
    private readonly ObservableCollection<UdtStructure> _importedUdts = [];

    // ─── 趋势图 ───
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

    private readonly AppConfig _config = AppConfig.Load();
    private static readonly SKColor[] TrendColors = [SKColors.Crimson, SKColors.Cyan, SKColors.SeaGreen, SKColors.Gold, SKColors.DodgerBlue, SKColors.MediumPurple];

    // ─── 仪表盘（AngularGauge） ───
    /// <summary>
    /// AngularGauge #1 的系列集合（由一个值弧 + 一个背景弧组成）
    /// 通过 GaugeGenerator.BuildSolidGauge() 初始化，之后可以通过替换整个集合来更新值。
    /// </summary>
    private ObservableCollection<ISeries> _gaugeSeries1 = [];

    /// <summary>
    /// AngularGauge #2 的系列集合（多区段仪表）
    /// 改用 XAML 中声明的 XamlAngularGaugeSeries（gaugeSeg0~gaugeSeg4），
    /// 此字段已废弃，保留作为 API 参考
    /// </summary>
    [Obsolete("改用 XAML 中声明的 gaugeSeg0~gaugeSeg4")]
    private ObservableCollection<ISeries> _gaugeSeries2 = [];

    /// <summary>
    /// 多区段仪表的数据源，绑定到 listGaugeSections ItemsControl
    /// </summary>
    private readonly ObservableCollection<GaugeSectionInfo> _gaugeSections = [];

    /// <summary>当前 AngularGauge 的弧厚度（px），由 sldGaugeThickness 控制</summary>
    private double _gaugeThickness = 20;

    /// <summary>是否已链接 Mock 数据</summary>
    private bool _gaugeMockLinked;

    public MainWindow()
    {
        InitializeComponent();
        InitTrendPanel();
        InitGaugePanel();

        listIRows.ItemsSource = _iRows; listQRows.ItemsSource = _qRows; listMRows.ItemsSource = _mRows;
        UpdateEmptyState();
        listImportedDb.ItemsSource = _importedDbs; listImportedUdt.ItemsSource = _importedUdts;

        var adapters = NetworkAdapter.Enumerate();
        cmbAdapter.ItemsSource = adapters;

        _scheduler.DataUpdated += OnPollData;
        tabControl.SelectionChanged += TabControl_SelectionChanged;
        RestoreFromConfig();
    }

    // ====================== 趋势图 ======================

    private void InitTrendPanel()
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
            _trendChannels.Add(new TrendChannelConfig { Key = key, Label = label, Color = color, Unit = unit, Min = min, Max = max, Variable = key });

            // ── 2.0.4 LineSeries 配置 ──
            // Stroke           : 线条颜色 + 粗细（SolidColorPaint）
            // Fill             : null = 不填充面积
            // GeometrySize     : 0 = 不显示数据点标记（实时曲线不需要点）
            // LineSmoothness   : 0~1 曲线平滑度（0=折线, 0.3=轻微平滑）
            // TooltipLabelFormatter: 2.0.4 自定义悬停提示格式
            //   内置变量: {Series.Name}, {Point.X}, {Point.Y}, {Value}
            //   通过 Series.Name 存标签，在 formatter 中拼接单位和值
            // ScalesYAt        : 如果要用多个 Y 轴，指定索引
            var series = new LineSeries<DateTimePoint>
            {
                Values = buf,
                Name = label, // 用于 tooltip 和图例
                Stroke = new SolidColorPaint(TrendColors[idx++ % TrendColors.Length]) { StrokeThickness = 2 },
                Fill = null,
                GeometrySize = 0,
                LineSmoothness = 0.3,
                // 2.0.4 默认 tooltip 显示通道名 + 坐标值
                // 自定义格式需要 TooltipLabelFormatter（属性名视版本略有差异）
            };
            _trendSeries.Add(series);
        }
        listTrendChannels.ItemsSource = _trendChannels;
        cmbTrendTimeRange.ItemsSource = TimeRangeOptions.Select(o => o.label).ToList();
        cmbTrendTimeRange.SelectedIndex = 0;
        _trendTimeWindowMs = TimeRangeOptions[0].windowMs;
        cartesianTrend.Series = _trendSeries;

        // ── 2.0.4 Y 轴配置 ──
        // 归一化后显示 0~100%，Labeler 格式化刻度标签
        // 用 " %" 后缀提示用户这是 normalized 百分比
        cartesianTrend.YAxes = [
            new Axis
            {
                MinLimit = 0,
                MaxLimit = 100,
                Labeler = value => $"{value:F0}%",
                // 2.0.4 轴线样式
                ShowSeparatorLines = true,
                SeparatorsPaint = new SolidColorPaint(SKColors.Gray) { StrokeThickness = 0.5f },
            }
        ];
        cartesianTrend.TooltipPosition = LiveChartsCore.Measure.TooltipPosition.Top;
        // 2.0.4 提示框背景
        cartesianTrend.TooltipBackgroundPaint = new SolidColorPaint(new SKColor(40, 40, 40, 230));

        UpdateTrendXAxis();

        // ── 柱状图 ──
        // 2.0.4 ColumnSeries 直接用 double 数组，无需 ObservableCollection
        var barVals = new ObservableCollection<double> { 0, 0, 0, 0, 0, 0 };
        _barSeriesColl.Add(new ColumnSeries<double>
        {
            Values = barVals,
            Fill = new SolidColorPaint(SKColor.Parse("#3498DB")),
            Padding = 2,
            MaxBarWidth = 40,
        });
        cartesianBars.Series = _barSeriesColl;
        cartesianBars.XAxes = [new Axis
        {
            Labels = ["Temp", "Press", "Flow", "Level", "Servo", "Curr."],
            LabelsRotation = 45,
            // 2.0.4 标签样式
            LabelsPaint = new SolidColorPaint(SKColors.LightGray),
        }];
        cartesianBars.YAxes = [new Axis
        {
            MinLimit = 0,
            Labeler = value => $"{value:F0}",
            SeparatorsPaint = new SolidColorPaint(SKColors.Gray) { StrokeThickness = 0.5f },
        }];
        cartesianBars.TooltipPosition = LiveChartsCore.Measure.TooltipPosition.Top;

        _mockTrend.SampleGenerated += OnTrendSample;
        // 刻度等趋势Tab首次选中时再画（此时canvas才有尺寸）
        _gaugeDrawn = false;
    }
    private bool _gaugeDrawn;

    private void OnTrendSample(string key, double val, DateTime ts)
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
            int bi = key switch { "ch_temp" => 0, "ch_press" => 1, "ch_flow" => 2, "ch_level" => 3, "ch_servo" => 4, "ch_current" => 5, _ => -1 };
            if (bi >= 0) vals[bi] = val;
            if (key == "ch_servo") UpdateGaugeNeedle(val);
            if (_gaugeMockLinked) UpdateAngularGaugesFromMock(key, val);
        });
    }

    private void BtnTrendMock_Click(object sender, RoutedEventArgs e)
    {
        if (_mockTrend.IsRunning) { _mockTrend.Stop(); btnTrendMock.Content = "▶ 启动 Mock"; }
        else { _mockTrend.Start(); btnTrendMock.Content = "■ 停止 Mock"; }
    }

    private void CanvasGauge_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (!_gaugeDrawn && e.NewSize.Width >= 10)
        { _gaugeDrawn = true; DrawGaugeScale(45.0); }
    }

    private void CmbTrendTimeRange_Changed(object _, SelectionChangedEventArgs e)
    {
        int idx = cmbTrendTimeRange.SelectedIndex;
        if (idx < 0 || idx >= TimeRangeOptions.Length) return;
        _trendTimeWindowMs = TimeRangeOptions[idx].windowMs;
        UpdateTrendXAxis();
    }

    private void UpdateTrendXAxis() { double n = DateTime.Now.Ticks; cartesianTrend.XAxes = [MakeTrendAxis(n - _trendTimeWindowMs * 10_000, n)]; }
    private void SlideTrendXAxis() { double n = DateTime.Now.Ticks; cartesianTrend.XAxes = [MakeTrendAxis(n - _trendTimeWindowMs * 10_000, n)]; }
    private static Axis MakeTrendAxis(double min, double max) => new Axis { MinLimit = min, MaxLimit = max, Labeler = v => v <= 0 || v > 1e18 ? "" : new DateTime((long)v).ToLocalTime().ToString("HH:mm:ss") };

    /// <summary>画刻度尺（仅画一次，由 InitTrendPanel 调用）</summary>
    private void DrawGaugeScale(double initialPos)
    {
        var c = canvasGauge; if (c.ActualWidth < 10) return;
        double w = c.ActualWidth, h = c.ActualHeight;
        double left = double.TryParse(txtGaugeLeft.Text, out var l) ? l : 0;
        double right = double.TryParse(txtGaugeRight.Text, out var r) ? r : 100;
        int ticks = int.TryParse(txtGaugeTicks.Text, out var tc) ? tc : 10;
        if (ticks < 2) ticks = 2;
        c.Children.Clear();
        double pad = 20, drawW = w - pad * 2, range = right - left;
        if (range <= 0) range = 100;
        // 刻度线 + 标签（静态，只画一次）
        for (int i = 0; i <= ticks; i++)
        {
            double frac = (double)i / ticks, x = pad + drawW * frac;
            c.Children.Add(new Line { X1 = x, Y1 = 0, X2 = x, Y2 = h * 0.35, Stroke = new SolidColorBrush(Color.FromRgb(0x88, 0x88, 0x88)), StrokeThickness = 1 });
            if (ticks <= 10 || i % 2 == 0)
            {
                var lbl = new TextBlock { Text = $"{left + range * frac:F0}", FontSize = 8, Foreground = new SolidColorBrush(Color.FromRgb(0x88, 0x88, 0x88)) };
                Canvas.SetLeft(lbl, x - 12); Canvas.SetTop(lbl, h * 0.38); c.Children.Add(lbl);
            }
        }
        // 保存布局参数到 Tag，供 UpdateGaugeNeedle 使用
        c.Tag = (pad, drawW, range, left, h);
        UpdateGaugeNeedle(initialPos);
    }

    /// <summary>仅更新指针位置和数值标签（每次数据到达时调用，不重画刻度）</summary>
    private void UpdateGaugeNeedle(double pos)
    {
        var c = canvasGauge;
        if (c.Tag is not (double pad, double drawW, double range, double left, double h)) return;
        txtGaugePos.Text = $"{pos:F1} mm";
        // 移除旧指针元素（从索引3开始，前面3个是刻度线+标签）
        while (c.Children.Count > 3) c.Children.RemoveAt(c.Children.Count - 1);
        double nFrac = Math.Clamp((pos - left) / range, 0, 1), nx = pad + drawW * nFrac;
        c.Children.Add(new Rectangle { Width = 3, Height = h * 0.55, Fill = new SolidColorBrush(Color.FromRgb(0xE7, 0x4C, 0x3C)), RadiusX = 2, RadiusY = 2 });
        Canvas.SetTop(c.Children[^1], h * 0.4); Canvas.SetLeft(c.Children[^1], nx - 1.5);
        c.Children.Add(new Polygon { Points = new PointCollection { new(nx - 5, h * 0.4 + 10), new(nx + 5, h * 0.4 + 10), new(nx, h * 0.4 - 2) }, Fill = new SolidColorBrush(Color.FromRgb(0xE7, 0x4C, 0x3C)) });
        var vl = new TextBlock { Text = $"{pos:F1}", FontSize = 10, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(Color.FromRgb(0xE7, 0x4C, 0x3C)) };
        Canvas.SetLeft(vl, Math.Clamp(nx - 15, 0, c.ActualWidth - 35)); Canvas.SetTop(vl, 0); c.Children.Add(vl);
    }

    // ====================== 仪表盘（AngularGauge） ======================
    //
    // LiveCharts2 2.0.4 WPF AngularGauge 使用说明：
    // ─────────────────────────────────────────────────────────────────────────
    // WPF 2.0.4 新增了专用的 XAML 控件，无需全部用 C# 代码生成：
    //
    // 1. XamlAngularGaugeSeries（仪表弧系列）
    //    在 XAML 的 <PieChart.Series> 中定义，属性：
    //      GaugeValue           : 当前值（double），支持 x:Name 在 C# 直接更新
    //      OuterRadiusOffset    : 外径偏移（px），正数向内缩，多层叠加时逐层+15~20px
    //      MaxRadialColumnWidth : 弧的最大厚度（px）
    //      CornerRadius         : 弧端圆角（px），2.0.4 已实现
    //      Fill                 : 颜色（在 C# 中设置）
    //
    // 2. XamlNeedle（仪表指针）
    //    放在 <PieChart.VisualElements> 中，属性：
    //      Value : 指针指向的值（double）
    //      Width : 指针宽度（px）
    //
    // 3. XamlAngularTicks（刻度线）
    //    也放在 <PieChart.VisualElements> 中，属性：
    //      LabelsSize       : 刻度标签字号
    //      LabelsOuterOffset: 标签离外缘的距离（px）
    //      OuterOffset      : 刻度线离外缘的距离（px）
    //      TicksLength      : 刻度线长度（px）
    //
    // 4. GaugeGenerator.BuildSolidGauge()（单值实心表盘 — 代码方式）
    //    仍然可用，适合需要背景弧 + 值弧的场景。
    //    但在 2.0.4 中，也可以在 XAML 中用两个 XamlAngularGaugeSeries 叠加。
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// 初始化 AngularGauge 面板（在构造函数中调用）
    ///
    /// 初始化步骤：
    /// 1. 创建 AngularGauge #1（单值实心表盘），初始值设为 0
    /// 2. 创建 AngularGauge #2（多区段仪表），显示 6 个通道的状态
    /// 3. 绑定区段列表到 ItemsControl
    /// </summary>
    private void InitGaugePanel()
    {
        // ── AngularGauge #1: 单值实心表盘 ──
        // GaugeGenerator.BuildSolidGauge() 返回 ISeries[]
        // 第一个 GaugeItem: 实际值的弧（彩色）
        // 第二个 GaugeItem: 背景弧（灰色，GaugeItem.Background 常量）
        UpdateSolidGauge(0);

        // ── AngularGauge #2: 多区段仪表 ──
        // 定义 5 个区段，对应不同数据通道
        var sectionDefs = new (string label, string color)[]
        {
            ("温度",  "#E24B4A"),
            ("压力",  "#37D3E0"),
            ("流量",  "#1D9E75"),
            ("液位",  "#F4D03F"),
            ("电流",  "#9B59B6"),
        };
        foreach (var (label, color) in sectionDefs)
            _gaugeSections.Add(new GaugeSectionInfo { Label = label, Color = color, Value = 0 });

        // 多区段仪表使用 XAML 声明的 XamlAngularGaugeSeries（2.0.4+ WPF 专用控件）
        // x:Name = gaugeSeg0 ~ gaugeSeg4，在 XAML 的 <PieChart.Series> 中定义
        // 只需在 C# 中设置颜色（Fill）即可
        var xamlSections = new[] { gaugeSeg0, gaugeSeg1, gaugeSeg2, gaugeSeg3, gaugeSeg4 };
        for (int i = 0; i < Math.Min(xamlSections.Length, sectionDefs.Length); i++)
        {
            var color = SKColor.Parse(sectionDefs[i].color);
            xamlSections[i].Fill = new SolidColorPaint(color);
        }

        // 绑定多区段的文字列表
        listGaugeSections.ItemsSource = _gaugeSections;
    }

    /// <summary>
    /// 更新 AngularGauge #1（单值实心表盘）的值
    ///
    /// GaugeGenerator.BuildSolidGauge() 创建一个新集合替换旧集合。
    /// PieChart 检测到 Series 引用变化会自动重绘。
    ///
    /// 关键参数说明：
    ///   value        : 当前仪表值（会被映射到 InitialRotation~InitialRotation+MaxAngle 角度之间）
    ///   InnerRadius  : 内径偏移 10px -> 弧默认从距圆心 10px 处开始绘制
    ///   Fill         : 值弧颜色，示例用青色
    ///   DataLabelsPosition: ChartCenter -> 数字显示在仪表圆心位置
    ///   DataLabelsFormatter: 格式化显示的文本
    /// </summary>
    private void UpdateSolidGauge(double value)
    {
        var thickness = _gaugeThickness;
        var minVal = double.TryParse(txtAngularMin.Text, out var mn) ? mn : 0;
        var maxVal = double.TryParse(txtAngularMax.Text, out var mx) ? mx : 100;

        _gaugeSeries1 = new ObservableCollection<ISeries>(
            GaugeGenerator.BuildSolidGauge(
                // ── 值弧（前弧）：显示实际值的彩色弧段 ──
                new GaugeItem(value, series =>
                {
                    // InnerRadius: 内径偏移量（px）。值越大，弧形环越细（从内部向中心收缩）
                    // 建议范围 5~30，必须和背景弧一致
                    series.InnerRadius = 10;

                    // MaxRadialColumnWidth: 弧的最大厚度（px），控制弧的视觉宽度
                    // 由 sldGaugeThickness 滑块动态控制（5~60）
                    series.MaxRadialColumnWidth = thickness;

                    // Fill: 弧的填充颜色，使用 SkiaSharp SKColor
                    series.Fill = new SolidColorPaint(SKColors.Cyan);

                    // Stroke/Fill: 也可设置弧的边框
                    // series.Stroke = new SolidColorPaint(SKColors.White) { StrokeThickness = 1 };

                    // CornerRadius: 弧端圆角（px），2.0.4 已实现，4~8 为圆润弧端
                    series.CornerRadius = 4;

                    // DataLabelsPosition: 标签显示位置
                    // ChartCenter → 显示在仪表圆心
                    // PolarLabelsPosition.ChartCenter | End | Middle | Start
                    series.DataLabelsPosition = LiveChartsCore.Measure.PolarLabelsPosition.ChartCenter;

                    // DataLabelsFormatter: 格式化标签文字
                    // point.Coordinate.PrimaryValue → 当前值
                    // point.Coordinate.MinLimit / MaxLimit → 量程边界
                    series.DataLabelsFormatter = point => $"{point.Coordinate.PrimaryValue:F1}%";

                    // DataLabelsPaint: 标签文字样式（SKTypeface 可选）
                    series.DataLabelsPaint = new SolidColorPaint(SKColors.White);
                }),

                // ── 背景弧（底弧）：显示灰色半透明背景 ──
                // GaugeItem.Background 常量表示这是一个背景层
                // InnerRadius 必须与值弧一致，否则会错位
                new GaugeItem(GaugeItem.Background, series =>
                {
                    series.InnerRadius = 10;
                    series.MaxRadialColumnWidth = thickness;
                    series.Fill = new SolidColorPaint(new SKColor(64, 64, 64, 60)); // 半透明灰色
                    // CornerRadius: 2.0.4 已实现，背景弧也设圆角保持视觉一致
                    series.CornerRadius = 4;
                })
            )
        );

        // 重新赋值 Series 会触发 PieChart 重绘
        angularGauge1.Series = _gaugeSeries1;

        // 同步更新 PieChart 的量程
        if (angularGauge1.MinValue != minVal || angularGauge1.MaxValue != maxVal)
        {
            angularGauge1.MinValue = minVal;
            angularGauge1.MaxValue = maxVal;
        }

        // 更新指针（XAML 中声明的 XamlNeedle，x:Name="gaugeNeedle"）
        // gaugeNeedle.Value 控制指针在仪表上的指向角度
        if (gaugeNeedle != null)
            gaugeNeedle.Value = Math.Clamp(value, minVal, maxVal);

        // 更新数值显示
        double clampedVal = Math.Clamp(value, minVal, maxVal);
        txtAngularGaugeVal.Text = $"{clampedVal:F1} mm";
    }

    /// <summary>
    /// 更新 AngularGauge #2（多区段仪表）的各个区段值
    ///
    /// XAML 中已声明 gaugeSeg0~gaugeSeg4（XamlAngularGaugeSeries），
    /// 直接设置它们的 GaugeValue 属性即可触发重绘。
    /// </summary>
    private void UpdateSectionsGauge(double[] values)
    {
        var xamlSections = new[] { gaugeSeg0, gaugeSeg1, gaugeSeg2, gaugeSeg3, gaugeSeg4 };
        int count = Math.Min(values.Length, xamlSections.Length);
        for (int i = 0; i < count; i++)
        {
            xamlSections[i].GaugeValue = values[i];
        }
    }

    // ====================== AngularGauge 事件处理 ======================

    /// <summary>
    /// 量程（Min/Max）修改时调用，刷新 AngularGauge #1
    /// 也支持实时预览：输入新值后按回车或焦点离开即更新
    /// </summary>
    /// <remarks>
    /// XAML 绑定: TextChanged="TxtAngularMinMax_Changed"
    /// 注意：频繁 TextChanged 触发 UpdateSolidGauge 可能影响性能，
    /// 可以改为 LostFocus 事件只在输入完成后触发一次
    /// </remarks>
    private void TxtAngularMinMax_Changed(object sender, TextChangedEventArgs e)
    {
        // 只在仪表盘 Tab 可见时才自动刷新，避免后台不必要的重绘
        if (tabControl.SelectedIndex == 2)
        {
            UpdateSolidGauge(0);
        }
    }

    /// <summary>
    /// 弧厚度滑块值变化时调用，刷新 AngularGauge #1
    /// 让用户可以实时调节弧的粗细
    /// </summary>
    /// <remarks>
    /// XAML 绑定: ValueChanged="SldGaugeThickness_Changed"
    /// Minimum=5, Maximum=60, Value=20（初始 20px）
    /// 每次滑动都触发更新，由于 GaugeGenerator 生成新集合，频繁操作可能有开销
    /// </remarks>
    private void SldGaugeThickness_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        _gaugeThickness = e.NewValue;
        // 获取当前值并刷新
        // 从当前仪表上获取显示的数值
        double currentVal = 0;
        if (_gaugeSeries1.Count > 0 && _gaugeSeries1[0] is PieSeries<ObservableValue> ps
            && ps.Values is IEnumerable<ObservableValue> gaugeVals)
        {
            var ov = gaugeVals.FirstOrDefault();
            currentVal = ov?.Value ?? 0;
        }
        UpdateSolidGauge(currentVal);
    }

    /// <summary>
    /// "联动 Mock 数据" 按钮点击事件
    /// 点击后订阅 MockTrendService 的最新值更新 AngularGauge
    /// 再点击一次取消联动
    /// </summary>
    private void BtnGaugeLinkMock_Click(object sender, RoutedEventArgs e)
    {
        _gaugeMockLinked = !_gaugeMockLinked;

        if (_gaugeMockLinked)
        {
            btnGaugeLinkMock.Content = "🔗 已联动 ✓";
            btnGaugeLinkMock.Background = new SolidColorBrush(Color.FromRgb(0x27, 0xAE, 0x60));
            // 如果 Mock 没在运行，自动启动
            if (!_mockTrend.IsRunning)
                _mockTrend.Start();
        }
        else
        {
            btnGaugeLinkMock.Content = "🔗 联动 Mock 数据";
            btnGaugeLinkMock.Background = new SolidColorBrush(Color.FromRgb(0x52, 0xAE, 0xC3));
            // 如果趋势图也没在用 Mock，可以停止
            if (!_mockTrend.IsRunning) { }
        }
    }

    /// <summary>
    /// 由 OnTrendSample 调用，更新 AngularGauge #1 的值（伺服通道）
    /// 同时更新多区段表 #2
    /// </summary>
    private void UpdateAngularGaugesFromMock(string key, double val)
    {
        if (!_gaugeMockLinked) return;

        // AngularGauge #1: 显示伺服位置（ch_servo）
        if (key == "ch_servo")
        {
            UpdateSolidGauge(val);
        }

        // AngularGauge #2: 每收到一个通道数据就更新对应区段
        int sectionIdx = key switch
        {
            "ch_temp"    => 0,
            "ch_press"   => 1,
            "ch_flow"    => 2,
            "ch_level"   => 3,
            "ch_current" => 4,
            _            => -1,
        };
        if (sectionIdx >= 0)
        {
            // 更新区段值
            UpdateSectionsGauge(
                _trendChannels
                    .Where(c => c.Key != "ch_servo") // 排除伺服（用 AngularGauge #1 展示）
                    .Select(c => Math.Clamp(c.CurrentValue, 0, 100))
                    .ToArray()
            );

            // 更新文字列表
            if (sectionIdx < _gaugeSections.Count)
            {
                var ch = _trendChannels.FirstOrDefault(c => c.Key == key);
                if (ch != null)
                    _gaugeSections[sectionIdx].Value = ch.CurrentValue;
            }
        }
    }

    // ====================== Tab 切换 ======================

    private void TabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        // 三个 Tab 的可见性切换：0=手动读写，1=趋势图，2=仪表盘
        manualPanel.Visibility = tabControl.SelectedIndex == 0 ? Visibility.Visible : Visibility.Collapsed;
        trendPanel.Visibility = tabControl.SelectedIndex == 1 ? Visibility.Visible : Visibility.Collapsed;
        gaugePanel.Visibility = tabControl.SelectedIndex == 2 ? Visibility.Visible : Visibility.Collapsed;
        if (tabControl.SelectedIndex == 1 && !_gaugeDrawn && canvasGauge.ActualWidth >= 10)
        { _gaugeDrawn = true; DrawGaugeScale(45.0); }
    }

    // ====================== 主题 ======================

    private void BtnTheme_Click(object sender, RoutedEventArgs e)
    {
        bool d = ThemeManager.Current == AppThemeMode.Dark;
        ThemeManager.Toggle();
        btnTheme.Content = d ? "☀" : "🌙";
        SaveConfig();
    }

    // ====================== 连接 ======================

    private void BtnConnect_Click(object sender, RoutedEventArgs e)
    {
        string ip = txtIP.Text.Trim();
        if (!int.TryParse(txtPort.Text.Trim(), out int p)) p = 102;
        if (!int.TryParse(txtRack.Text.Trim(), out int r)) r = 0;
        if (!int.TryParse(txtSlot.Text.Trim(), out int s)) s = 0;
        string localIp = cmbAdapter.SelectedItem is NetworkAdapter na ? na.Ip : "";
if (_plc.Connect(localIp, ip, p, r, s) != 0) { MessageBox.Show(this, $"连接失败: {_plc.LastError}", "错误"); UpdateUI(); return; }
        txtStatus.Text = $"已连接 {ip}:{p}";
        txtStatus.Foreground = new SolidColorBrush(Color.FromRgb(0x27, 0xAE, 0x60));
        indicator.Fill = new SolidColorBrush(Color.FromRgb(0x27, 0xAE, 0x60));
        UpdateUI(); SaveConfig();
    }

    private void BtnDisconnect_Click(object _, RoutedEventArgs _2) { _plc.Disconnect(); txtStatus.Text = "未连接"; txtStatus.Foreground = new SolidColorBrush(Color.FromRgb(0x55, 0x55, 0x55)); indicator.Fill = new SolidColorBrush(Color.FromRgb(0x55, 0x55, 0x55)); UpdateUI(); SaveConfig(); }

    private void UpdateUI() { bool c = _plc.IsConnected; bool p = _scheduler.IsRunning; btnConnect.IsEnabled = !c; btnDisconnect.IsEnabled = c; btnIRead.IsEnabled = c && !p; btnQRead.IsEnabled = c; btnQWriteMode.IsEnabled = c; btnMRead.IsEnabled = c; btnMWriteMode.IsEnabled = c; btnStartPoll.IsEnabled = c && !p; btnStopPoll.IsEnabled = c && p; }

    // ====================== 手动读写 ======================

    private static int[] ParseAddrs(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return [];
        return input.Split(',', '，', ';', '；', ' ').Select(s => s.Trim()).Where(s => int.TryParse(s, out _)).Select(int.Parse).Distinct().OrderBy(a => a).ToArray();
    }

    private void BtnIRead_Click(object _, RoutedEventArgs _2) { var a = ParseAddrs(txtIAddress.Text); if (a.Length == 0) return; _lastIBytes = _plc.ReadBytes(S7Service.AreaI, a); _iRows.Clear(); foreach (int i in a) _iRows.Add(new ByteRowViewModel(i, "I", true) { Value = _lastIBytes.GetValueOrDefault(i) }); UpdateEmptyState(); }
    private void BtnQRead_Click(object _, RoutedEventArgs _2) { var a = ParseAddrs(txtQAddress.Text); if (a.Length == 0) return; _lastQBytes = _plc.ReadBytes(S7Service.AreaQ, a); _qRows.Clear(); foreach (int i in a) _qRows.Add(new ByteRowViewModel(i, "Q", false) { Value = _lastQBytes.GetValueOrDefault(i) }); UpdateEmptyState(); }
    private void BtnMRead_Click(object _, RoutedEventArgs _2) { var a = ParseAddrs(txtMAddress.Text); if (a.Length == 0) return; _lastMBytes = _plc.ReadBytes(S7Service.AreaM, a); _mRows.Clear(); foreach (int i in a) _mRows.Add(new ByteRowViewModel(i, "M", false) { Value = _lastMBytes.GetValueOrDefault(i) }); UpdateEmptyState(); }

    private void BtnQWriteMode_Click(object sender, RoutedEventArgs e) { _qWriteMode = !_qWriteMode; btnQWriteMode.Content = _qWriteMode ? "🔓 写入模式 (开)" : "🔒 写入模式 (关)"; btnQWriteMode.Background = _qWriteMode ? new SolidColorBrush(Color.FromRgb(0xE7, 0x4C, 0x3C)) : new SolidColorBrush(Color.FromRgb(0x55, 0x55, 0x55)); }
    private void BtnMWriteMode_Click(object sender, RoutedEventArgs e) { _mWriteMode = !_mWriteMode; btnMWriteMode.Content = _mWriteMode ? "🔓 写入模式 (开)" : "🔒 写入模式 (关)"; btnMWriteMode.Background = _mWriteMode ? new SolidColorBrush(Color.FromRgb(0xE7, 0x4C, 0x3C)) : new SolidColorBrush(Color.FromRgb(0x55, 0x55, 0x55)); }

    private void BitBlock_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not Border b || b.DataContext is not BitViewModel bit || bit.Parent == null) return;
        if (!((bit.Parent.AreaLabel == "Q" && _qWriteMode) || (bit.Parent.AreaLabel == "M" && _mWriteMode))) return;
        bit.Toggle();
        _plc.WriteByte(bit.Parent.AreaLabel == "Q" ? S7Service.AreaQ : S7Service.AreaM, bit.Parent.ByteAddress, bit.Parent.ToByte());
    }

    private void UpdateEmptyState() { txtIEmpty.Visibility = _iRows.Count == 0 ? Visibility.Visible : Visibility.Collapsed; txtQEmpty.Visibility = _qRows.Count == 0 ? Visibility.Visible : Visibility.Collapsed; txtMEmpty.Visibility = _mRows.Count == 0 ? Visibility.Visible : Visibility.Collapsed; }

    // ====================== 导入 DB/UDT ======================

    private void BtnImportDb_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.OpenFileDialog { Filter = "DB 文件 (*.db)|*.db|所有文件 (*.*)|*.*", Title = "选择 TIA Portal 导出的 .db 文件", Multiselect = false };
        if (dlg.ShowDialog(this) != true) return;
        var db = DbFileParser.Parse(dlg.FileName);
        if (db.HasUnknownType) { MessageBox.Show(this, $"解析失败: {db.ParseError}", "未知数据类型", MessageBoxButton.OK, MessageBoxImage.Warning); return; }
        if (db.ParseError != null) { MessageBox.Show(this, $"解析失败: {db.ParseError}", "错误", MessageBoxButton.OK, MessageBoxImage.Error); return; }
        var inputDlg = new InputDialog($"请输入 DB{db.DbName} 的 DB 编号:", "1");
        if (inputDlg.ShowDialog() != true) return;
        if (!int.TryParse(inputDlg.InputText, out int dbNum) || dbNum <= 0) { MessageBox.Show(this, "无效的 DB 编号", "错误"); return; }
        if (_importedDbs.Any(d => d.DbNumber == dbNum)) { MessageBox.Show(this, $"DB{dbNum} 已导入，请先删除再重新导入", "提示"); return; }
        db.DbNumber = dbNum; _importedDbs.Add(db); SaveConfig();
    }

    private void BtnImportUdt_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.OpenFileDialog { Filter = "UDT 文件 (*.udt)|*.udt|所有文件 (*.*)|*.*", Title = "选择 TIA Portal 导出的 .udt 文件", Multiselect = false };
        if (dlg.ShowDialog(this) != true) return;
        var udt = UdtFileParser.Parse(dlg.FileName);
        if (udt.HasUnknownType) { MessageBox.Show(this, $"解析失败: {udt.ParseError}", "未知数据类型", MessageBoxButton.OK, MessageBoxImage.Warning); return; }
        if (udt.ParseError != null) { MessageBox.Show(this, $"解析失败: {udt.ParseError}", "错误", MessageBoxButton.OK, MessageBoxImage.Error); return; }
        if (_importedUdts.Any(u => u.UdtName == udt.UdtName)) { MessageBox.Show(this, $"UDT \"{udt.UdtName}\" 已导入", "提示"); return; }
        _importedUdts.Add(udt); SaveConfig();
    }

    private void BtnDeleteImportedDb_Click(object sender, RoutedEventArgs e) { if (sender is Button btn && btn.Tag is DbStructure db) { _importedDbs.Remove(db); SaveConfig(); } }
    private void BtnDeleteImportedUdt_Click(object sender, RoutedEventArgs e) { if (sender is Button btn && btn.Tag is UdtStructure udt) { _importedUdts.Remove(udt); SaveConfig(); } }

    // ====================== 轮询 ======================

    private void BtnStartPoll_Click(object sender, RoutedEventArgs e)
    {
        if (!_plc.IsConnected) { MessageBox.Show(this, "请先连接 PLC", "提示"); return; }
        int port = TryParse(txtPort.Text, 102), rack = TryParse(txtRack.Text, 0), slot = TryParse(txtSlot.Text, 0);
        int interval = TryParse(txtPollInterval.Text, 50);
        var cfg = _scheduler.Config;
        cfg.Fast.PollIAddr = txtIAddress.Text; cfg.Fast.PollQAddr = txtQAddress.Text; cfg.Fast.PollMAddr = txtMAddress.Text;
        cfg.FastInterval = interval;
        cfg.DbIp = txtIP.Text.Trim(); cfg.DbRack = rack; cfg.DbSlot = slot;
        _scheduler.Start(_plc, port);
        if (!_scheduler.IsConnected) { MessageBox.Show(this, $"轮询连接失败:\n{_scheduler.LastError}", "错误"); return; }
        txtPollStatus.Text = "● 轮询中"; txtPollStatus.Foreground = new SolidColorBrush(Color.FromRgb(0x27, 0xAE, 0x60));
        UpdateUI();
    }

    private void BtnStopPoll_Click(object sender, RoutedEventArgs e) { StopPolling(); }
    private void StopPolling() { _scheduler.Stop(); txtPollStatus.Text = "■ 已停止"; txtPollStatus.Foreground = new SolidColorBrush(Color.FromRgb(0xE7, 0x4C, 0x3C)); UpdateUI(); }

    /// <summary>轮询数据直接写入 I/Q/M 行（无中间缓存）</summary>
    private void OnPollData(HashSet<string> updated)
    {
        // InvokeAsync: 不阻塞 timer 线程，避免轮询和 UI 写操作竞争 S7Client
        Dispatcher.InvokeAsync(() =>
        {
            // 更新延迟显示
            txtPollLatency.Text = $"{_scheduler.LatencyMs}ms";

            void UpdateRows(ObservableCollection<ByteRowViewModel> rows)
            {
                foreach (var row in rows)
                {
                    string key = $"{row.AreaLabel}{row.ByteAddress}";
                    if (updated.Contains(key) && _scheduler.GetValue(key) is byte val && val != row.Value)
                        row.Value = val;
                }
            }
            UpdateRows(_iRows); UpdateRows(_qRows); UpdateRows(_mRows);
        });
    }

    // ====================== 工具 + 关闭 ======================

    private static int TryParse(string s, int def) => int.TryParse(s?.Trim(), out int r) ? r : def;
    protected override void OnClosed(EventArgs e) { _mockTrend.SampleGenerated -= OnTrendSample; _scheduler.Dispose(); SaveConfig(); _plc.Dispose(); _mockTrend.Dispose(); base.OnClosed(e); }

    // ====================== 配置持久化 ======================

    private void RestoreFromConfig()
    {
        txtIP.Text = _config.IP; txtPort.Text = _config.Port.ToString();
        txtRack.Text = _config.Rack.ToString(); txtSlot.Text = _config.Slot.ToString();
        txtIAddress.Text = _config.ManualIAddress; txtQAddress.Text = _config.ManualQAddress; txtMAddress.Text = _config.ManualMAddress;
        if (cmbAdapter.ItemsSource is List<NetworkAdapter> list)
        {
            var idx = list.FindIndex(a => a.Ip == _config.LocalIP);
            if (idx >= 0) cmbAdapter.SelectedIndex = idx;
        }
        _importedDbs.Clear();
        foreach (var info in _config.ImportedDbs)
            _importedDbs.Add(new DbStructure { DbNumber = info.DbNumber, DbName = info.DbName, SourceFile = info.SourceFile, Variables = System.Text.Json.JsonSerializer.Deserialize<List<DbVariable>>(info.VariablesJson) ?? [] });
        _importedUdts.Clear();
        foreach (var info in _config.ImportedUdts)
            _importedUdts.Add(new UdtStructure { UdtName = info.UdtName, SourceFile = info.SourceFile, Variables = System.Text.Json.JsonSerializer.Deserialize<List<DbVariable>>(info.VariablesJson) ?? [] });
        if (_config.ThemeMode == "Light") { ThemeManager.Apply(AppThemeMode.Light); btnTheme.Content = "☀"; }
        if (_config.WindowLeft >= 0 && _config.WindowTop >= 0) { Left = _config.WindowLeft; Top = _config.WindowTop; }
        Width = _config.WindowWidth; Height = _config.WindowHeight;
        if (Enum.TryParse<WindowState>(_config.WindowState, out var ws)) WindowState = ws;
    }

    private void SaveConfig()
    {
        _config.IP = txtIP.Text; _config.Port = TryParse(txtPort.Text, 102); _config.Rack = TryParse(txtRack.Text, 0); _config.Slot = TryParse(txtSlot.Text, 0);
        _config.LocalIP = cmbAdapter.SelectedItem is NetworkAdapter a ? a.Ip : "";
        _config.ManualIAddress = txtIAddress.Text; _config.ManualQAddress = txtQAddress.Text; _config.ManualMAddress = txtMAddress.Text;
        _config.ImportedDbs = _importedDbs.Select(d => new ImportedDbInfo { DbNumber = d.DbNumber, DbName = d.DbName, SourceFile = d.SourceFile, VariablesJson = System.Text.Json.JsonSerializer.Serialize(d.Variables) }).ToList();
        _config.ImportedUdts = _importedUdts.Select(u => new ImportedUdtInfo { UdtName = u.UdtName, SourceFile = u.SourceFile, VariablesJson = System.Text.Json.JsonSerializer.Serialize(u.Variables) }).ToList();
        _config.ThemeMode = ThemeManager.Current == AppThemeMode.Dark ? "Dark" : "Light";
        _config.WindowLeft = Left; _config.WindowTop = Top; _config.WindowWidth = Width; _config.WindowHeight = Height; _config.WindowState = WindowState.ToString();
        _config.Save();
    }
}

/// <summary>
/// 多区段仪表 (AngularGauge #2) 的文字列表数据模型
///
/// 绑定到 listGaugeSections ItemsControl，显示每个区段的：
///   Label : 区段名称（如 "温度", "压力"）
///   Color : 区段颜色（与弧颜色一致）
///   Value : 当前数值
///
/// 实现 INotifyPropertyChanged 以便 Value 变化时 UI 自动更新
/// </summary>
public class GaugeSectionInfo : System.ComponentModel.INotifyPropertyChanged
{
    public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;

    private string _label = "";
    public string Label
    {
        get => _label;
        set { _label = value; PropertyChanged?.Invoke(this, new(nameof(Label))); }
    }

    private string _color = "#000000";
    public string Color
    {
        get => _color;
        set { _color = value; PropertyChanged?.Invoke(this, new(nameof(Color))); }
    }

    private double _value;
    public double Value
    {
        get => _value;
        set
        {
            _value = value;
            PropertyChanged?.Invoke(this, new(nameof(Value)));
        }
    }
}
