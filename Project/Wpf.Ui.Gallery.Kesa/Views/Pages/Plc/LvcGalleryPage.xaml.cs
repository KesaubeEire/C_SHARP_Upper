using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using LiveChartsCore;
using LiveChartsCore.Defaults;
using LiveChartsCore.Kernel;
using LiveChartsCore.Measure;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using LiveChartsCore.SkiaSharpView.WPF;
using SkiaSharp;

namespace Wpf.Ui.Gallery.Views.Pages.Plc;

public partial class LvcGalleryPage : Page
{
    private static readonly Random Rng = new();

    public LvcGalleryPage()
    {
        InitializeComponent();
        BuildGallery();
    }

    private static double[] Mock(int c) =>
        Enumerable.Range(0, c).Select(_ => (double)Rng.Next(10, 100)).ToArray();

    private static double[] Sine(int c, double f) =>
        Enumerable.Range(0, c).Select(i => 50.0 + 30.0 * Math.Sin(i * f) + Rng.Next(-5, 5)).ToArray();

    private static readonly SKColor[] Pal =
    [
        SKColor.Parse("#409EFF"), SKColor.Parse("#67C23A"), SKColor.Parse("#E6A23C"),
        SKColor.Parse("#F56C6C"), SKColor.Parse("#909399"), SKColor.Parse("#B37FEB"),
        SKColor.Parse("#36CFC9"), SKColor.Parse("#FF85C0"), SKColor.Parse("#FBD437"), SKColor.Parse("#5CB87A"),
    ];

    private static readonly string[] CatLabels = ["一", "二", "三", "四", "五"];
    private static readonly string[] MonthLabels = ["一月", "二月", "三月", "四月", "五月", "六月"];
    private static readonly string[] RowLabels = ["A", "B", "C", "D", "E"];
    private static readonly string[] StackCatLabels = ["Cat1", "Cat2", "Cat3", "Cat4", "Cat5"];
    private static readonly string[] HeatXLabels = ["A", "B", "C", "D"];
    private static readonly string[] HeatYLabels = ["J", "F", "M", "A", "M", "J"];
    private static readonly double[] BasicBarS1 = [3, 5, 4, 6, 2];
    private static readonly double[] BasicBarS2 = [4, 2, 5, 3, 6];
    private static readonly double[] PieBasic = [4, 6, 3, 5, 2];
    private static readonly double[] PiePush = [8, 5, 3, 6, 4];
    private static readonly double[] PieDough = [5, 3, 4, 6, 2];
    private static readonly double[] CorePolar = [12, 10, 14, 8, 11, 9, 13, 7];

    private static SolidColorPaint P(int i) => new(Pal[i % Pal.Length]);

    private void BuildGallery()
    {
        AddGroup("折线图 (Line)", BuildLineGroup());
        AddGroup("阶梯 & 堆叠", BuildStackGroup());
        AddGroup("柱状图 (Bar)", BuildBarGroup());
        AddGroup("饼图 (Pie)", BuildPieGroup());
        AddGroup("散点 & 金融", BuildScatterGroup());
        AddGroup("热力 & 极坐标", BuildHeatGroup());
        AddGroup("坐标轴 (Axes)", BuildAxesGroup());
        AddGroup("通用 (General)", BuildGeneralGroup());
        AddGroup("设计 (Design)", BuildDesignGroup());
    }

    private (string, Func<FrameworkElement>)[] BuildLineGroup() =>
    [
        ("Basic line",      () => CC(c => c.Series = [LPW(Mock(12))])),
        ("Smoothness",      () => CC(c => c.Series = [LPW(Sine(18, 0.6), true, true, 0)])),
        ("Basic area",      () => CC(c => c.Series = [LPW(Sine(12, 0.5), true, true, 0)])),
        ("Line geometries", () => CC(c => c.Series = [LPW(Mock(10), false, false, 15)])),
        ("Wind direction",  () => CC(c => { c.Series = [new ScatterSeries<double> { Values = Mock(12), GeometrySize = 18, Stroke = null, Fill = P(2) }]; c.YAxes = [new Axis { MinLimit = 0, MaxLimit = 360 }]; })),
        ("Specify X and Y", () => CC(c => c.Series = [new LineSeries<ObservablePoint> { Values = ScatterPts(10), Mapping = (p, _) => new Coordinate((double)p.X!, (double)p.Y!), Fill = null, Stroke = P(1), GeometrySize = 10 }])),
        ("Zoom and pan",    () => CC(c => { c.Series = [LPW(Sine(25, 0.3))]; c.ZoomMode = ZoomAndPanMode.Both; })),
    ];

    private (string, Func<FrameworkElement>)[] BuildStackGroup() =>
    [
        ("Step line",    () => CC(c => c.Series = [new StepLineSeries<double> { Values = Mock(8), Fill = null, Stroke = P(0), GeometrySize = 8 }])),
        ("Stacked area", () => CC(c => c.Series = [new StackedAreaSeries<double> { Values = Mock(8), Fill = P(0), Stroke = null }, new StackedAreaSeries<double> { Values = Mock(8), Fill = P(1), Stroke = null }, new StackedAreaSeries<double> { Values = Mock(8), Fill = P(2), Stroke = null }])),
    ];

    private (string, Func<FrameworkElement>)[] BuildBarGroup() =>
    [
        ("Basic bars",   () => CC(c => { c.Series = [new ColumnSeries<double> { Values = BasicBarS1, Stroke = null, Fill = P(0) }, new ColumnSeries<double> { Values = BasicBarS2, Stroke = null, Fill = P(1) }]; c.XAxes = [new Axis { Labels = CatLabels }]; c.LegendPosition = LegendPosition.Right; })),
        ("Custom bars",  () => CC(c => c.Series = [new ColumnSeries<double> { Values = Mock(6), Stroke = P(2), Fill = P(2) }])),
        ("Row series",   () => CC(c => { c.Series = [new RowSeries<double> { Values = Mock(5), Stroke = null, Fill = P(4) }]; c.YAxes = [new Axis { Labels = RowLabels }]; })),
        ("Stacked bars", () => CC(c => { c.Series = [new StackedColumnSeries<double> { Values = Mock(5), Stroke = null, Fill = P(0), Name = "A" }, new StackedColumnSeries<double> { Values = Mock(5), Stroke = null, Fill = P(1), Name = "B" }, new StackedColumnSeries<double> { Values = Mock(5), Stroke = null, Fill = P(2), Name = "C" }]; c.XAxes = [new Axis { Labels = StackCatLabels }]; c.LegendPosition = LegendPosition.Right; })),
    ];

    private (string, Func<FrameworkElement>)[] BuildPieGroup() =>
    [
        ("Basic pie",   () => PC(c => { c.Series = PieV(PieBasic); c.LegendPosition = LegendPosition.Right; })),
        ("Pushout",     () => PC(c => { c.Series = PiePush.Select((x, i) => new PieSeries<double> { Values = [x], Fill = P(i), Stroke = null, Pushout = i == 1 ? 15 : 0 } as ISeries).ToArray(); c.LegendPosition = LegendPosition.Right; })),
        ("Doughnut",    () => PC(c => { c.Series = PieDough.Select((x, i) => new PieSeries<double> { Values = [x], Fill = P(i), Stroke = null, InnerRadius = 50 } as ISeries).ToArray(); c.LegendPosition = LegendPosition.Right; })),
        ("Basic gauge", () => PC(c => { c.Series = [new PieSeries<double> { Values = [65.0], Fill = P(0), Stroke = null, InnerRadius = 70 }, new PieSeries<double> { Values = [35.0], Fill = new SolidColorPaint(new SKColor(230, 230, 230)), Stroke = null, InnerRadius = 70 }]; c.MaxValue = 100; })),
        ("Slim gauge",  () => PC(c => { c.Series = [new PieSeries<double> { Values = [72.0], Fill = P(2), Stroke = null, InnerRadius = 75 }, new PieSeries<double> { Values = [28.0], Fill = new SolidColorPaint(new SKColor(230, 230, 230)), Stroke = null, InnerRadius = 75 }]; c.MaxValue = 100; })),
    ];

    private (string, Func<FrameworkElement>)[] BuildScatterGroup() =>
    [
        ("Basic scatter", () => CC(c => c.Series = [new ScatterSeries<double> { Values = Mock(10), Fill = P(0), Stroke = null, GeometrySize = 15 }])),
        ("Bubble",        () => CC(c => c.Series = [new ScatterSeries<ObservablePoint> { Values = ScatterPts(8), Mapping = (p, _) => new Coordinate((double)p.X!, (double)p.Y!), Fill = P(1), Stroke = null, GeometrySize = 12 }])),
        ("Candle sticks", () => CC(c => { var fi = Fin(8); c.Series = [new CandlesticksSeries<FinancialPoint> { Values = fi }]; c.XAxes = [new DateTimeAxis(TimeSpan.FromDays(1), v => v.ToString("MM/dd"))]; })),
    ];

    private (string, Func<FrameworkElement>)[] BuildHeatGroup() =>
    [
        ("Basic heat", () => CC(c => { c.Series = [new HeatSeries<WeightedPoint> { Values = HeatData(), HeatMap = [SKColor.Parse("#FFF176").AsLvcColor(), SKColor.Parse("#2F4F4F").AsLvcColor(), SKColor.Parse("#0000FF").AsLvcColor()] }]; c.XAxes = [new Axis { Labels = HeatXLabels }]; c.YAxes = [new Axis { Labels = HeatYLabels }]; })),
        ("Polar",      () => PolC(c => { c.Series = [new PolarLineSeries<double> { Values = CorePolar, GeometrySize = 10, IsClosed = true, Stroke = P(0), Fill = new SolidColorPaint(Pal[0].WithAlpha(40)) }]; c.RadiusAxes = [new PolarAxis { MaxLimit = 20 }]; })),
    ];

    private (string, Func<FrameworkElement>)[] BuildAxesGroup() =>
    [
        ("Axis labels", () => CC(c => { c.Series = [new ColumnSeries<double> { Values = Mock(6), Stroke = null }]; c.XAxes = [new Axis { Labels = MonthLabels }]; })),
        ("Multi axes",  () => CC(c => { c.Series = [new ColumnSeries<double> { Values = Mock(6), Stroke = null, Fill = P(0), Name = "柱状" }, new LineSeries<double> { Values = Sine(6, 0.5), Fill = null, Stroke = P(1), GeometrySize = 8, ScalesYAt = 1 }]; c.YAxes = [new Axis { Name = "左轴", MinLimit = 0 }, new Axis { Name = "右轴", MinLimit = 0, Position = AxisPosition.End }]; c.LegendPosition = LegendPosition.Right; })),
        ("Axis style",  () => CC(c => c.Series = [new LineSeries<double> { Values = Sine(10, 0.5), Fill = null, Stroke = P(2), GeometrySize = 8 }])),
        ("Date time",   () => CC(c => { var now = DateTime.UtcNow; var vs = Mock(8); c.Series = [new LineSeries<DateTimePoint> { Values = Enumerable.Range(0, 8).Select(i => new DateTimePoint(now.AddDays(-7 + i), vs[i])).ToArray(), Fill = null, Stroke = P(4), GeometrySize = 8 }]; c.XAxes = [new DateTimeAxis(TimeSpan.FromDays(1), v => v.ToString("MM/dd"))]; })),
    ];

    private (string, Func<FrameworkElement>)[] BuildGeneralGroup() =>
    [
        ("Sections",        () => CC(c => { c.Series = [new LineSeries<double> { Values = Sine(12, 0.5), Fill = null, Stroke = P(0), GeometrySize = 0 }]; c.Sections = [new RectangularSection { Yi = 60, Yj = 80, Fill = P(3) }, new RectangularSection { Yi = 30, Yj = 50, Fill = P(1) }]; })),
        ("Null points",     () => CC(c => c.Series = [new LineSeries<double?> { Values = new double?[] { 10, 15, null, 20, 18, null, 25, 22 }, Fill = null, Stroke = P(3), GeometrySize = 8 }])),
        ("Custom tooltips", () => CC(c => { c.Series = [new LineSeries<double> { Values = Sine(8, 0.5), Fill = null, Stroke = P(0), GeometrySize = 8, Name = "温度°C" }, new LineSeries<double> { Values = Sine(8, 0.4).Select(v => v / 2 + 30).ToArray(), Fill = null, Stroke = P(1), GeometrySize = 8, Name = "湿度%" }]; c.TooltipPosition = TooltipPosition.Top; c.LegendPosition = LegendPosition.Right; })),
        ("Scrollable",      () => CC(c => { c.Series = [new LineSeries<double> { Values = Mock(25), Fill = null, Stroke = P(0), GeometrySize = 6 }]; c.ZoomMode = ZoomAndPanMode.X; })),
    ];

    private (string, Func<FrameworkElement>)[] BuildDesignGroup() =>
    [
        ("Linear gradient", () => CC(c => { var g = new LinearGradientPaint([SKColor.Parse("#409EFF"), SKColor.Parse("#67C23A")]); c.Series = [new LineSeries<double> { Values = Sine(8, 0.5), Fill = null, Stroke = g, GeometrySize = 8, GeometryStroke = g }]; })),
        ("Dashed lines",    () => CC(c => c.Series = [new LineSeries<double> { Values = Sine(10, 0.5), Fill = null, Stroke = new SolidColorPaint(SKColor.Parse("#F56C6C"), 3), GeometrySize = 8 }])),
    ];

    private void AddGroup(string title, (string name, Func<FrameworkElement> factory)[] charts)
    {
        var wrap = new WrapPanel { Margin = new Thickness(8) };
        foreach (var (name, factory) in charts)
        {
            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(24) });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            grid.Children.Add(new TextBlock { Text = name, FontWeight = FontWeights.Bold, Margin = new Thickness(6, 2, 0, 0) });
            try
            {
                var chart = factory();
                chart.Margin = new Thickness(4);
                Grid.SetRow(chart, 1);
                grid.Children.Add(chart);
            }
            catch (Exception ex)
            {
                var tb = new TextBlock { Text = "⚠ " + ex.Message, Foreground = System.Windows.Media.Brushes.Red, TextWrapping = TextWrapping.Wrap };
                Grid.SetRow(tb, 1);
                grid.Children.Add(tb);
            }
            wrap.Children.Add(new Border
            {
                Width = 360,
                Height = 290,
                Margin = new Thickness(6),
                Background = System.Windows.Media.Brushes.White,
                CornerRadius = new CornerRadius(6),
                Child = grid,
            });
        }
        _rootPanel.Children.Add(new GroupBox { Header = title, Margin = new Thickness(0, 0, 0, 12), Content = wrap });
    }

    private static ObservablePoint[] ScatterPts(int c) =>
        Enumerable.Range(0, c).Select(_ => new ObservablePoint(Rng.Next(1, 20), Rng.Next(1, 20))).ToArray();

    private static WeightedPoint[] HeatData()
    {
        var d = new WeightedPoint[24];
        var k = 0;
        for (var r = 0; r < 4; r++)
            for (var c = 0; c < 6; c++)
                d[k++] = new WeightedPoint(r, c, Rng.Next(50, 550));
        return d;
    }

    private static FinancialPoint[] Fin(int c)
    {
        var bp = 100.0;
        var d = new FinancialPoint[c];
        for (var i = 0; i < c; i++)
        {
            var o = bp + Rng.Next(-5, 5);
            var cl = o + Rng.Next(-8, 8);
            d[i] = new FinancialPoint
            {
                Date = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddDays(i),
                Open = (float)o,
                Close = (float)cl,
                High = (float)Math.Max(o, cl) + Rng.Next(0, 5),
                Low = (float)Math.Min(o, cl) - Rng.Next(0, 5),
            };
            bp = cl;
        }
        return d;
    }

    private static CartesianChart CC(Action<CartesianChart> setup)
    {
        var c = new CartesianChart { LegendPosition = LegendPosition.Hidden, AnimationsSpeed = TimeSpan.FromMilliseconds(400) };
        setup(c);
        return c;
    }

    private static PieChart PC(Action<PieChart> setup)
    {
        var c = new PieChart { AnimationsSpeed = TimeSpan.FromMilliseconds(400) };
        setup(c);
        return c;
    }

    private static PolarChart PolC(Action<PolarChart> setup)
    {
        var c = new PolarChart { AnimationsSpeed = TimeSpan.FromMilliseconds(400) };
        setup(c);
        return c;
    }

    private static LineSeries<double> LPW(double[] v, bool fill = false, bool smooth = false, int geo = 8) =>
        new()
        {
            Values = v,
            Fill = fill ? P(0) : null,
            Stroke = P(0),
            GeometrySize = geo,
            GeometryStroke = P(0),
            GeometryFill = fill ? null : new SolidColorPaint(SKColors.White),
            LineSmoothness = smooth ? 1.0 : 0.0,
        };

    private static ISeries[] PieV(double[] vals) =>
        vals.Select((v, i) => new PieSeries<double> { Values = [v], Fill = P(i), Stroke = null } as ISeries).ToArray();
}
