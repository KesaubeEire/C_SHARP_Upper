using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using LiveChartsCore;
using LiveChartsCore.Defaults;
using LiveChartsCore.Kernel.Sketches;
using LiveChartsCore.Measure;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using LiveChartsCore.SkiaSharpView.WPF;
using SkiaSharp;
using Wpf.Ui.Controls;
using Wpf.Ui.Extensions;
using WpfScada.Controls.Plc;
using WpfScada.Models.Plc;
using TextBlock = System.Windows.Controls.TextBlock;
using WpfScada.Services.Plc;

namespace WpfScada.Views.Pages.Plc;

#pragma warning disable IDE1006 // 静态只读字段按项目约定需 _ 前缀，但图表页使用广泛，暂不修改
public partial class TrendChartPage : Page
{
    private readonly S7Service _s7;
    private readonly AppConfigService _config;
    private readonly IContentDialogService _contentDialog;

    // 通道定义
    private sealed class ChannelDef
    {
        public string Key = "", Label = "", Unit = "";
        public int DbNumber, ByteOffset;
        public SKColor Color;
        public double Min, Max;
    }

    private static readonly ChannelDef[] Channels =
    [
        new() { Key = "db1_6",  Label = "变频器频率",  Unit = "Hz",   DbNumber = 1,  ByteOffset = 6,  Color = SKColor.Parse("#42A5F5"), Min = 0,  Max = 50  },
        new() { Key = "db6_38", Label = "滑台位置", Unit = "mm",    DbNumber = 6,  ByteOffset = 38, Color = SKColor.Parse("#66BB6A"), Min = -200,  Max = 100 },
        new() { Key = "db7_38", Label = "圆盘角度", Unit = "°",  DbNumber = 7,  ByteOffset = 38, Color = SKColor.Parse("#FFA726"), Min = 0,  Max = 360  },
    ];

    /// <summary>
    /// 仪表数据源配置 —— 对应 PLC DB 地址
    /// </summary>
    private sealed class GaugeDef
    {
        public ServoGauge Gauge = null!;
        public string Key = "", Label = "";
        public int DbNumber, ByteOffset;
    }

    /// <summary>
    /// 速度仪表数据源：
    /// - 圆盘速度 → DB7.DBD42 (REAL)  量程 0~360
    /// - 步进速度 → DB6.DBD42 (REAL)  量程 0~100
    /// </summary>
    private readonly List<GaugeDef> _gaugeDefs = [];

    // 时间范围选项
    private static readonly (string Label, TimeSpan Duration)[] TimeRanges =
    [
        ("1 分钟",    TimeSpan.FromMinutes(1)),
        ("3 分钟",    TimeSpan.FromMinutes(3)),
        ("5 分钟",    TimeSpan.FromMinutes(5)),
        ("10 分钟",   TimeSpan.FromMinutes(10)),
        ("30 分钟",   TimeSpan.FromMinutes(30)),
        ("1 小时",    TimeSpan.FromHours(1)),
        ("6 小时",    TimeSpan.FromHours(6)),
        ("12 小时",   TimeSpan.FromHours(12)),
        ("24 小时",   TimeSpan.FromHours(24)),
    ];

    // 归一化数据点（Value=归一化0~100, RawValue=原始值）
    private sealed class NormPoint : DateTimePoint
    {
        public NormPoint(DateTime x, double normY, double rawVal) : base(x, normY) => RawValue = rawVal;
        public double RawValue { get; }
    }

    // 数据缓冲
    private readonly Dictionary<string, ObservableCollection<NormPoint>> _buffers = [];
    private readonly ObservableCollection<ISeries> _series = [];
    private readonly Dictionary<string, double> _currentValues = [];
    private readonly List<Border> _legendItems = [];

    // 监控
    private readonly List<VariableMonitor> _monitors = [];
    private TimeSpan _selectedDuration = TimeRanges[4].Duration; // 默认 30min

    public TrendChartPage(S7Service s7, AppConfigService config, IContentDialogService contentDialog)
    {
        _s7 = s7;
        _config = config;
        _contentDialog = contentDialog;
        InitializeComponent();
        InitTimeRangeCombo();
        InitChart();
        InitLegend();
        InitGauges();
        StartMonitors();
        StartGaugeMonitors();
    }

    // ===================== 速度仪表初始化 =====================

    /// <summary>
    /// 初始化两个速度仪表的数据源。
    /// 数据地址（PLC DB 配置）：
    /// - 圆盘速度 → DB7.DBD42 (REAL)  量程 0~360
    /// - 步进速度 → DB6.DBD42 (REAL)  量程 0~100
    /// </summary>
    private void InitGauges()
    {
        _gaugeDefs.Add(new GaugeDef
        {
            Gauge = gaugeDiscSpeed,
            Key = "gauge_disc",
            Label = "圆盘速度",
            DbNumber = 7,
            ByteOffset = 42,        // DB7.DBD42 — 圆盘速度 REAL
        });
        _gaugeDefs.Add(new GaugeDef
        {
            Gauge = gaugeStepSpeed,
            Key = "gauge_step",
            Label = "步进速度",
            DbNumber = 6,
            ByteOffset = 42,        // DB6.DBD42 — 步进速度 REAL
        });
    }

    // ===================== 初始化 =====================

    private void InitTimeRangeCombo()
    {
        timeRangeCombo.ItemsSource = TimeRanges.Select(r => r.Label).ToList();
        timeRangeCombo.SelectedIndex = 4; // 30min
    }

    private void InitChart()
    {
        foreach (var ch in Channels)
        {
            var buf = new ObservableCollection<NormPoint>();
            _buffers[ch.Key] = buf;
            _currentValues[ch.Key] = 0;

            double thickness = trendChart.LineStrokeThickness;
            double smoothness = trendChart.LineSmoothness;
            double geoSize = trendChart.GeometrySize;
            double fillOpacity = trendChart.FillOpacity;

            var series = new LineSeries<NormPoint>
            {
                Values = buf,
                Name = $"{ch.Label} ({ch.Unit})",
                Stroke = new SolidColorPaint(ch.Color) { StrokeThickness = (float)thickness },
                Fill = fillOpacity > 0
                    ? new SolidColorPaint(ch.Color.WithAlpha((byte)(fillOpacity * 255)))
                    : null,
                GeometrySize = (float)geoSize,
                LineSmoothness = (float)smoothness,
                GeometryStroke = geoSize > 0 ? new SolidColorPaint(ch.Color) : null,
                GeometryFill = geoSize > 0 ? new SolidColorPaint(SKColors.White) : null,
            };
            // 悬浮提示显示：实际值 (百分比)
            if (series is ICartesianSeries cs)
                cs.YToolTipLabelFormatter = point => $"{(point.Context.DataSource as NormPoint)?.RawValue ?? point.Coordinate.PrimaryValue:F2} {ch.Unit} ({point.Coordinate.PrimaryValue:F0}%)";
            _series.Add(series);
        }

        trendChart.Series = _series;
        trendChart.LegendPosition = LegendPosition.Hidden; // 用自定义图例

        // X 轴 — 时间轴
        trendChart.XAxes =
        [
            new DateTimeAxis(TimeSpan.FromSeconds(1), formattableString => $"{formattableString:HH:mm:ss}")
            {
                Name = "时间",
                NameTextSize = 12,
                TextSize = 11,
            },
        ];

        // Y 轴 — 固定 0~100%
        trendChart.YAxes =
        [
            new Axis
            {
                Name = "百分比",
                NameTextSize = 12,
                TextSize = 11,
                MinLimit = -5,
                MaxLimit = 100,
                ShowSeparatorLines = true,
                Labeler = v => $"{v:F0}%",
            },
        ];

        // Tooltip handled by CartesianChartKesaTrend DP
    }

    // ===================== 图例 =====================

    private void InitLegend()
    {
        legendPanel.Children.Clear();
        _legendItems.Clear();

        foreach (var ch in Channels)
        {
            var border = new Border
            {
                Margin = new Thickness(0, 0, 24, 0),
                Child = new StackPanel { Orientation = Orientation.Horizontal },
            };
            var stack = (StackPanel)border.Child;

            // 色点
            stack.Children.Add(new Ellipse
            {
                Width = 10,
                Height = 10,
                Fill = new SolidColorBrush(System.Windows.Media.Color.FromArgb(
                    ch.Color.Alpha, ch.Color.Red, ch.Color.Green, ch.Color.Blue)),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 6, 0),
            });

            // 标签 + 实时值
            var labelTb = new TextBlock
            {
                Text = $"{ch.Label}: ",
                FontSize = 13,
                FontWeight = FontWeights.SemiBold,
                VerticalAlignment = VerticalAlignment.Center,
            };
            labelTb.SetResourceReference(TextBlock.ForegroundProperty, "TextFillColorPrimaryBrush");
            stack.Children.Add(labelTb);

            var valTb = new TextBlock
            {
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(System.Windows.Media.Color.FromArgb(
                    ch.Color.Alpha, ch.Color.Red, ch.Color.Green, ch.Color.Blue)),
                VerticalAlignment = VerticalAlignment.Center,
                Text = "---",
            };
            stack.Children.Add(valTb);

            // 单位
            var unitTb = new TextBlock
            {
                Text = $" {ch.Unit}",
                FontSize = 12,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(2, 0, 0, 0),
            };
            unitTb.SetResourceReference(TextBlock.ForegroundProperty, "TextFillColorSecondaryBrush");
            stack.Children.Add(unitTb);

            _legendItems.Add(border);
            legendPanel.Children.Add(border);
        }
    }

    private void UpdateLegendValue(string key, double val)
    {
        int idx = Array.FindIndex(Channels, c => c.Key == key);
        if (idx < 0 || idx >= _legendItems.Count)
            return;

        var border = _legendItems[idx];
        if (border.Child is StackPanel stack && stack.Children.Count >= 3
            && stack.Children[2] is TextBlock tb)
        {
            tb.Text = $"{val:F3}";
        }
    }

    // ===================== 数据源 =====================

    private void StartMonitors()
    {
        foreach (var ch in Channels)
        {
            var monitor = new VariableMonitor(_s7)
            {
                Key = ch.Key,
                Label = ch.Label,
                DbNumber = ch.DbNumber,
                Offset = ch.ByteOffset,
                DataType = "REAL",
                IntervalMs = 100,
            };
            monitor.SampleGenerated += OnSample;
            monitor.Start();
            _monitors.Add(monitor);
        }
    }

    // ===================== 速度仪表监控 =====================

    private readonly List<VariableMonitor> _gaugeMonitors = [];

    private void StartGaugeMonitors()
    {
        foreach (var g in _gaugeDefs)
        {
            var monitor = new VariableMonitor(_s7)
            {
                Key = g.Key,
                Label = g.Label,
                DbNumber = g.DbNumber,
                Offset = g.ByteOffset,
                DataType = "REAL",
                IntervalMs = 200,
            };
            monitor.SampleGenerated += OnGaugeSample;
            monitor.Start();
            _gaugeMonitors.Add(monitor);
        }
    }

    private void StopGaugeMonitors()
    {
        foreach (var m in _gaugeMonitors)
        {
            m.SampleGenerated -= OnGaugeSample;
            m.Stop();
        }
        _gaugeMonitors.Clear();
    }

    private void OnGaugeSample(string key, double val, DateTime ts)
    {
        var def = _gaugeDefs.FirstOrDefault(g => g.Key == key);
        if (def == null)
            return;

        Dispatcher.Invoke(() => def.Gauge.UpdateValue(Math.Abs(val)));
    }

    private void StopMonitors()
    {
        foreach (var m in _monitors)
        {
            m.SampleGenerated -= OnSample;
            m.Stop();
        }
        _monitors.Clear();
    }

    private void OnSample(string key, double val, DateTime ts)
    {
        var ch = Channels.FirstOrDefault(c => c.Key == key);
        if (ch == null)
            return;

        double range = ch.Max - ch.Min;
        double normVal = range > 0 ? Math.Clamp((val - ch.Min) / range * 100, 0, 100) : 0;

        Dispatcher.Invoke(() =>
        {
            if (!_buffers.TryGetValue(key, out var buf))
                return;
            buf.Add(new NormPoint(ts, normVal, val));
            _currentValues[key] = val;
            UpdateLegendValue(key, val);
            TrimBuffer(buf);
            SlideAxis();
        });
    }

    private void TrimBuffer(ObservableCollection<NormPoint> buf)
    {
        int maxPoints = (int)(_selectedDuration.TotalMilliseconds / 100) + 10;
        while (buf.Count > maxPoints)
            buf.RemoveAt(0);
    }

    private void SlideAxis()
    {
        DateTime now = DateTime.Now;
        DateTime start = now - _selectedDuration;

        if (trendChart.XAxes?.FirstOrDefault() is DateTimeAxis dtAxis)
        {
            dtAxis.MinLimit = start.Ticks;
            dtAxis.MaxLimit = now.Ticks;
        }
    }

    // ===================== 时间范围切换 =====================

    private void OnTimeRangeChanged(object sender, SelectionChangedEventArgs e)
    {
        int idx = timeRangeCombo.SelectedIndex;
        if (idx < 0 || idx >= TimeRanges.Length)
            return;

        _selectedDuration = TimeRanges[idx].Duration;
        SlideAxis();

        // 裁剪所有缓冲到新窗口大小
        foreach (var buf in _buffers.Values)
            TrimBuffer(buf);
    }

    // ===================== 设备控制（3×3 按1松0） =====================

#pragma warning disable IDE1006 // XAML 事件绑定不能加 Async 后缀
    private async void OnMotorPressDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        e.Handled = true;
        if (sender is FrameworkElement fe && fe.Tag is string tag)
        {
            if (!_s7.IsConnected)
                await _contentDialog.ShowSimpleDialogAsync(
                    new SimpleContentDialogCreateOptions
                    {
                        Title = "提示",
                        Content = "PLC 未连接",
                        CloseButtonText = "确定",
                    });
            else
                WriteMotorBit(tag, true);
        }
    }

    private async void OnMotorPressUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.Tag is string tag)
        {
            if (!_s7.IsConnected)
                await _contentDialog.ShowSimpleDialogAsync(
                    new SimpleContentDialogCreateOptions
                    {
                        Title = "提示",
                        Content = "PLC 未连接",
                        CloseButtonText = "确定",
                    });
            else
                WriteMotorBit(tag, false);
        }
    }

    private async void OnMotorPressLeave(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (sender is FrameworkElement { IsMouseCaptured: true } fe && fe.Tag is string tag)
        {
            fe.ReleaseMouseCapture();
            if (!_s7.IsConnected)
                await _contentDialog.ShowSimpleDialogAsync(
                    new SimpleContentDialogCreateOptions
                    {
                        Title = "提示",
                        Content = "PLC 未连接",
                        CloseButtonText = "确定",
                    });
            else
                WriteMotorBit(tag, false);
        }
    }
#pragma warning restore IDE1006

    /// <summary>按1松0 — 从 Tag "db.byte.bit" 解析地址后写位</summary>
    private void WriteMotorBit(string tag, bool setBit)
    {
        var parts = tag.Split('.');
        if (parts.Length != 3
            || !int.TryParse(parts[0], out int dbNum) || dbNum <= 0
            || !int.TryParse(parts[1], out int byteOff)
            || !int.TryParse(parts[2], out int bitOff) || bitOff < 0 || bitOff > 7)
            return;

        byte? current = _s7.ReadByte(S7Service.AreaDB, byteOff, dbNum);
        if (!current.HasValue)
            return;

        byte newVal;
        if (setBit)
            newVal = (byte)(current.Value | (byte)(1 << bitOff));
        else
            newVal = (byte)(current.Value & (byte)~(1 << bitOff));

        _s7.WriteByte(S7Service.AreaDB, byteOff, newVal, dbNum);
    }

    /// <summary>使能按钮：点按取反 — 读当前位 → 取反 → 写回</summary>
    private async void OnEnableToggleClick(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: string tag }) return;

        if (!_s7.IsConnected)
        {
            await _contentDialog.ShowSimpleDialogAsync(
                new SimpleContentDialogCreateOptions
                {
                    Title = "提示",
                    Content = "PLC 未连接",
                    CloseButtonText = "确定",
                });
            return;
        }

        var parts = tag.Split('.');
        if (parts.Length != 3
            || !int.TryParse(parts[0], out int dbNum) || dbNum <= 0
            || !int.TryParse(parts[1], out int byteOff)
            || !int.TryParse(parts[2], out int bitOff) || bitOff < 0 || bitOff > 7)
            return;

        byte? current = _s7.ReadByte(S7Service.AreaDB, byteOff, dbNum);
        if (!current.HasValue) return;

        // 取反指定位
        byte newVal = (byte)(current.Value ^ (1 << bitOff));
        _s7.WriteByte(S7Service.AreaDB, byteOff, newVal, dbNum);
    }


    // ===================== 生命周期 =====================

    public void Stop()
    {
        StopMonitors();
        StopGaugeMonitors();
    }
}
