using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using LiveChartsCore;
using LiveChartsCore.Defaults;
using LiveChartsCore.Measure;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using LiveChartsCore.SkiaSharpView.WPF;
using SkiaSharp;
using Wpf.Ui.Controls;
using Wpf.Ui.Extensions;
using Wpf.Ui.Gallery.Models.Plc;
using TextBlock = System.Windows.Controls.TextBlock;
using Wpf.Ui.Gallery.Services.Plc;

namespace Wpf.Ui.Gallery.Views.Pages.Plc;

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

    // 监控 & Mock
    private readonly List<VariableMonitor> _monitors = [];
    private MockTrendService? _mock;
    private bool _isMock;
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
        StartMonitors();
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

            _series.Add(new LineSeries<NormPoint>
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
            });
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
                MinLimit = 0,
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
            stack.Children.Add(new TextBlock
            {
                Text = $"{ch.Label}: ",
                FontSize = 13,
                FontWeight = FontWeights.SemiBold,
                Foreground = Application.Current.TryFindResource("TextFillColorPrimaryBrush") as Brush
                             ?? new SolidColorBrush(Colors.White),
                VerticalAlignment = VerticalAlignment.Center,
            });

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
            stack.Children.Add(new TextBlock
            {
                Text = $" {ch.Unit}",
                FontSize = 12,
                Foreground = Application.Current.TryFindResource("TextFillColorSecondaryBrush") as Brush
                             ?? new SolidColorBrush(Colors.Gray),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(2, 0, 0, 0),
            });

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
        if (_isMock)
            return; // mock 模式时不接收真实数据

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

    // ===================== Mock =====================

    private void OnToggleMock(object sender, RoutedEventArgs e)
    {
        if (_mock != null && _mock.IsRunning)
        {
            _mock.Stop();
            _mock = null;
            _isMock = false;
            btnMock.Content = "▶ 模拟数据";
            btnMock.Appearance = Wpf.Ui.Controls.ControlAppearance.Secondary;

            // 清除 mock 数据，重新接收真实数据
            ClearAllBuffers();
            return;
        }

        // 启动 mock — 清真实数据，使用假通道
        _isMock = true;
        StopMonitors();
        ClearAllBuffers();

        // 注册假通道（归一化到 0~100，量程 0~100 即原值直接当百分比）
        var mockKeys = new[] { "mock_temp", "mock_press", "mock_flow" };
        var mockColors = new[] { SKColor.Parse("#E91E63"), SKColor.Parse("#9C27B0"), SKColor.Parse("#00BCD4") };
        var mockLabels = new[] { "温度模拟", "压力模拟", "流量模拟" };
        var mockChs = mockKeys.Select((k, i) => new ChannelDef { Key = k, Label = mockLabels[i], Unit = "", DbNumber = 0, ByteOffset = 0, Color = mockColors[i], Min = 0, Max = 100 }).ToArray();

        for (int i = 0; i < 3; i++)
        {
            var buf = new ObservableCollection<NormPoint>();
            _buffers[mockKeys[i]] = buf;
            _currentValues[mockKeys[i]] = 0;
            if (_series[i] is LineSeries<NormPoint> ls)
            {
                ls.Name = mockLabels[i];
                ls.Values = buf;
                ls.Stroke = new SolidColorPaint(mockColors[i]) { StrokeThickness = (float)trendChart.LineStrokeThickness };
                ls.Fill = trendChart.FillOpacity > 0
                    ? new SolidColorPaint(mockColors[i].WithAlpha((byte)(trendChart.FillOpacity * 255)))
                    : null;
                ls.GeometrySize = (float)trendChart.GeometrySize;
                ls.LineSmoothness = (float)trendChart.LineSmoothness;
                ls.GeometryStroke = trendChart.GeometrySize > 0 ? new SolidColorPaint(mockColors[i]) : null;
                ls.GeometryFill = trendChart.GeometrySize > 0 ? new SolidColorPaint(SKColors.White) : null;
            }

            // 更新图例
            UpdateLegendMock(i, mockLabels[i], mockColors[i]);
        }

        _mock = new MockTrendService();
        long mockTick = 0;
        _mock.DataGenerated += (key, val, ts) =>
        {
            mockTick++;
            double t = mockTick;
            // 3 条 mock 线 — 生成原始值后用 mockChs 归一化
            FeedMock(mockKeys[0], 50 + Math.Sin(t * 0.03) * 20 + Random.Shared.NextDouble() * 2, mockChs[0]);
            FeedMock(mockKeys[1], 30 + Math.Sin(t * 0.05) * 10 + Math.Sin(t * 0.20) * 3, mockChs[1]);
            FeedMock(mockKeys[2], 70 + Math.Sin(t * 0.04) * 14 + Random.Shared.NextDouble() * 4, mockChs[2]);
        };
        _mock.Start();
        btnMock.Content = "■ 停止模拟";
        btnMock.Appearance = Wpf.Ui.Controls.ControlAppearance.Danger;
    }

    private void UpdateLegendMock(int idx, string label, SKColor color)
    {
        if (idx >= _legendItems.Count)
            return;
        var border = _legendItems[idx];
        if (border.Child is StackPanel stack && stack.Children.Count >= 3)
        {
            if (stack.Children[0] is Ellipse el)
                el.Fill = new SolidColorBrush(System.Windows.Media.Color.FromArgb(
                    color.Alpha, color.Red, color.Green, color.Blue));
            if (stack.Children[1] is TextBlock tb)
                tb.Text = $"{label}: ";
        }
    }

    private void FeedMock(string key, double rawVal, ChannelDef ch)
    {
        double range = ch.Max - ch.Min;
        double normVal = range > 0 ? Math.Clamp((rawVal - ch.Min) / range * 100, 0, 100) : rawVal;

        Dispatcher.Invoke(() =>
        {
            if (!_buffers.TryGetValue(key, out var buf))
                return;
            buf.Add(new NormPoint(DateTime.Now, normVal, rawVal));
            _currentValues[key] = rawVal;
            TrimBuffer(buf);
            SlideAxis();
        });
    }

    private void ClearAllBuffers()
    {
        foreach (var buf in _buffers.Values)
            buf.Clear();
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


    // ===================== 生命周期 =====================

    public void Stop()
    {
        _mock?.Stop();
        StopMonitors();
    }
}
