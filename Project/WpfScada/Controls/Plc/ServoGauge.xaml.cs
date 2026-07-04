using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using LiveChartsCore.Painting;
using LiveChartsCore.SkiaSharpView.Painting;
using LiveChartsCore.SkiaSharpView.WPF;
using SkiaSharp;

namespace WpfScada.Controls.Plc;

/// <summary>
/// 支持 DynamicResource 主题自适应的仪表控件。
/// 通过 <see cref="DependencyProperty"/> 暴露着色属性，WPF 主题刷子变化时自动桥接 Skia Paint。
///
/// 量程配置：
/// - <see cref="MinValue"/> / <see cref="MaxValue"/> 控制仪表量程（默认 0~200）
/// - 色段边界 <see cref="GreenMax"/> / <see cref="YellowMax"/> / <see cref="RedMax"/> 为绝对值，
///   需根据实际量程手工设置（例如量程 360 时可设 GreenMax="180" YellowMax="300" RedMax="360"）
/// </summary>
public partial class ServoGauge : UserControl
{
    // ===================== 依赖属性 (DynamicResource 兼容) =====================

    /// <summary>指针填充色刷</summary>
    public static readonly DependencyProperty NeedleBrushProperty =
        DependencyProperty.Register(nameof(NeedleBrush), typeof(Brush), typeof(ServoGauge),
            new PropertyMetadata(new SolidColorBrush(Colors.Black), OnNeedleBrushChanged));

    /// <summary>刻度标签色刷</summary>
    public static readonly DependencyProperty LabelBrushProperty =
        DependencyProperty.Register(nameof(LabelBrush), typeof(Brush), typeof(ServoGauge),
            new PropertyMetadata(new SolidColorBrush(Colors.Gray), OnLabelBrushChanged));

    /// <summary>刻度线色刷</summary>
    public static readonly DependencyProperty TickBrushProperty =
        DependencyProperty.Register(nameof(TickBrush), typeof(Brush), typeof(ServoGauge),
            new PropertyMetadata(new SolidColorBrush(Colors.Gray), OnTickBrushChanged));

    /// <summary>数值文字色刷</summary>
    public static readonly DependencyProperty ValueBrushProperty =
        DependencyProperty.Register(nameof(ValueBrush), typeof(Brush), typeof(ServoGauge),
            new PropertyMetadata(new SolidColorBrush(Colors.Black), OnValueBrushChanged));

    /// <summary>单位文字色刷</summary>
    public static readonly DependencyProperty UnitBrushProperty =
        DependencyProperty.Register(nameof(UnitBrush), typeof(Brush), typeof(ServoGauge),
            new PropertyMetadata(new SolidColorBrush(Colors.Gray), OnUnitBrushChanged));

    /// <summary>绿色段填充色刷</summary>
    public static readonly DependencyProperty GreenFillBrushProperty =
        DependencyProperty.Register(nameof(GreenFillBrush), typeof(Brush), typeof(ServoGauge),
            new PropertyMetadata(new SolidColorBrush(Color.FromRgb(0x4a, 0xde, 0x80)), OnGreenFillBrushChanged));

    /// <summary>黄色段填充色刷</summary>
    public static readonly DependencyProperty YellowFillBrushProperty =
        DependencyProperty.Register(nameof(YellowFillBrush), typeof(Brush), typeof(ServoGauge),
            new PropertyMetadata(new SolidColorBrush(Color.FromRgb(0xfa, 0xcc, 0x15)), OnYellowFillBrushChanged));

    /// <summary>红色段填充色刷</summary>
    public static readonly DependencyProperty RedFillBrushProperty =
        DependencyProperty.Register(nameof(RedFillBrush), typeof(Brush), typeof(ServoGauge),
            new PropertyMetadata(new SolidColorBrush(Color.FromRgb(0xef, 0x44, 0x44)), OnRedFillBrushChanged));

    public Brush NeedleBrush { get => (Brush)GetValue(NeedleBrushProperty); set => SetValue(NeedleBrushProperty, value); }
    public Brush LabelBrush { get => (Brush)GetValue(LabelBrushProperty); set => SetValue(LabelBrushProperty, value); }
    public Brush TickBrush { get => (Brush)GetValue(TickBrushProperty); set => SetValue(TickBrushProperty, value); }
    public Brush ValueBrush { get => (Brush)GetValue(ValueBrushProperty); set => SetValue(ValueBrushProperty, value); }
    public Brush UnitBrush { get => (Brush)GetValue(UnitBrushProperty); set => SetValue(UnitBrushProperty, value); }
    public Brush GreenFillBrush { get => (Brush)GetValue(GreenFillBrushProperty); set => SetValue(GreenFillBrushProperty, value); }
    public Brush YellowFillBrush { get => (Brush)GetValue(YellowFillBrushProperty); set => SetValue(YellowFillBrushProperty, value); }
    public Brush RedFillBrush { get => (Brush)GetValue(RedFillBrushProperty); set => SetValue(RedFillBrushProperty, value); }

    /// <summary>仪表量程最小值（默认 0）</summary>
    public static readonly DependencyProperty MinValueProperty =
        DependencyProperty.Register(nameof(MinValue), typeof(double), typeof(ServoGauge),
            new PropertyMetadata(0.0, OnRangeChanged));

    /// <summary>仪表量程最大值（默认 200）</summary>
    public static readonly DependencyProperty MaxValueProperty =
        DependencyProperty.Register(nameof(MaxValue), typeof(double), typeof(ServoGauge),
            new PropertyMetadata(200.0, OnRangeChanged));

    public double MinValue { get => (double)GetValue(MinValueProperty); set => SetValue(MinValueProperty, value); }
    public double MaxValue { get => (double)GetValue(MaxValueProperty); set => SetValue(MaxValueProperty, value); }

    public double GaugeValue
    {
        get => needle.Value;
        set
        {
            valueText.Text = value.ToString("F1");
            needle.Value = Math.Clamp(value, MinValue, MaxValue);
        }
    }

    public string UnitLabel
    {
        get => unitText.Text;
        set => unitText.Text = value;
    }

    // 三色段设置
    public double GreenMax { get; set; } = 96;
    public double YellowMax { get; set; } = 160;
    public double RedMax { get; set; } = 200;

    // 防 CornerRadius 覆盖

    public ServoGauge()
    {
        InitializeComponent();

        foreach (var s in new[] { seriesGreen, seriesYellow, seriesRed })
            s.CornerRadius = 0.1;

        Loaded += (_, _) =>
        {
            foreach (var s in new[] { seriesGreen, seriesYellow, seriesRed })
                s.CornerRadius = 0;
            InitFixedBands();
        };
    }

    /// <summary>设固定的背景色段（不随指针值变化）</summary>
    public void InitFixedBands()
    {
        seriesGreen.GaugeValue = GreenMax;
        seriesYellow.GaugeValue = YellowMax - GreenMax;
        seriesRed.GaugeValue = RedMax - YellowMax;
    }

    /// <summary>更新指针位置和数值显示</summary>
    public void UpdateValue(double value)
    {
        valueText.Text = value.ToString("F1");
        needle.Value = Math.Clamp(value, MinValue, MaxValue);
    }

    /// <summary>量程变化时刷新 PieChart 边界和色段</summary>
    private static void OnRangeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ServoGauge c)
        {
            c.gaugeChart.MaxValue = c.MaxValue;
            c.gaugeChart.MinValue = c.MinValue;
            c.InitFixedBands();
        }
    }

    // ===================== Brush → SKColor 桥接 =====================

    private static SKColor BrushToSkColor(Brush brush)
    {
        if (brush is SolidColorBrush scb)
        {
            var c = scb.Color;
            return new SKColor(c.R, c.G, c.B, c.A);
        }
        return SKColors.Black;
    }

    private static SKColor ResolveThemeColor(string resourceKey, SKColor fallback)
    {
        if (Application.Current.TryFindResource(resourceKey) is SolidColorBrush scb)
            return new SKColor(scb.Color.R, scb.Color.G, scb.Color.B, scb.Color.A);
        return fallback;
    }

    // ===================== DP 变更回调 =====================

    private static void OnNeedleBrushChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ServoGauge c && e.NewValue is Brush b)
            c.needle.Fill = new SolidColorPaint(BrushToSkColor(b));
    }

    private static void OnLabelBrushChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ServoGauge c && e.NewValue is Brush b)
            c.ticks.LabelsPaint = new SolidColorPaint(BrushToSkColor(b));
    }

    private static void OnTickBrushChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ServoGauge c && e.NewValue is Brush b)
            c.ticks.Stroke = new SolidColorPaint(BrushToSkColor(b)) { StrokeThickness = 1.5f };
    }

    private static void OnValueBrushChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ServoGauge c && e.NewValue is Brush b)
            c.valueText.Foreground = b;
    }

    private static void OnUnitBrushChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ServoGauge c && e.NewValue is Brush b)
            c.unitText.Foreground = b;
    }

    private static void OnGreenFillBrushChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ServoGauge c && e.NewValue is Brush b)
            c.seriesGreen.Fill = new SolidColorPaint(BrushToSkColor(b));
    }

    private static void OnYellowFillBrushChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ServoGauge c && e.NewValue is Brush b)
            c.seriesYellow.Fill = new SolidColorPaint(BrushToSkColor(b));
    }

    private static void OnRedFillBrushChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ServoGauge c && e.NewValue is Brush b)
            c.seriesRed.Fill = new SolidColorPaint(BrushToSkColor(b));
    }
}
