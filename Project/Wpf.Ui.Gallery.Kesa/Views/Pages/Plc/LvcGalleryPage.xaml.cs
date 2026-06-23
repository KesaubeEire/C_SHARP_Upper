using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using LiveChartsCore;
using LiveChartsCore.Defaults;
using LiveChartsCore.Drawing;
using LiveChartsCore.Kernel;
using LiveChartsCore.Measure;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using LiveChartsCore.SkiaSharpView.Painting.Effects;
using LiveChartsCore.SkiaSharpView.WPF;
using SkiaSharp;

#pragma warning disable CA1861 // 画廊中频繁使用内联数组，提取为字段不利于可读性

namespace Wpf.Ui.Gallery.Views.Pages.Plc;

public partial class LvcGalleryPage : Page
{
    private static readonly Random Rng = new();

    public LvcGalleryPage()
    {
        InitializeComponent();
        BuildGallery();
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    private static double[] Mock(int c) =>
        Enumerable.Range(0, c).Select(_ => (double)Rng.Next(10, 100)).ToArray();

    private static double[] Sine(int c, double f = 0.5, double a = 30, double b = 50) =>
        Enumerable.Range(0, c).Select(i => b + a * Math.Sin(i * f) + Rng.Next(-5, 5)).ToArray();

    private static double[] Noise(int c, int min = 0, int max = 100) =>
        Enumerable.Range(0, c).Select(_ => (double)Rng.Next(min, max)).ToArray();

    private static readonly SKColor[] Pal =
    [
        SKColor.Parse("#409EFF"), SKColor.Parse("#67C23A"), SKColor.Parse("#E6A23C"),
        SKColor.Parse("#F56C6C"), SKColor.Parse("#909399"), SKColor.Parse("#B37FEB"),
        SKColor.Parse("#36CFC9"), SKColor.Parse("#FF85C0"), SKColor.Parse("#FBD437"), SKColor.Parse("#5CB87A"),
    ];

    private static SolidColorPaint P(int i, byte alpha = 255) => new(Pal[i % Pal.Length].WithAlpha(alpha));
    private static SolidColorPaint Paint(SKColor c) => new(c);
    private static SolidColorPaint Stroke(int i, float w = 2) => new(Pal[i % Pal.Length]) { StrokeThickness = w };

    private static readonly string[] Cat5 = ["一", "二", "三", "四", "五"];
    private static readonly string[] Cat6 = ["一月", "二月", "三月", "四月", "五月", "六月"];
    private static readonly string[] CatMonth = ["Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug"];
    private static readonly string[] RowLbl = ["A", "B", "C", "D", "E"];
    private static readonly string[] StackCat = ["Cat1", "Cat2", "Cat3", "Cat4", "Cat5"];

    // ── Build ────────────────────────────────────────────────────────────

    private void BuildGallery()
    {
        AddGroup("折线图 (Line)", BuildLineGroup());
        AddGroup("面积图 (Area)", BuildAreaGroup());
        AddGroup("阶梯 & 堆叠", BuildStackGroup());
        AddGroup("柱状图 (Column)", BuildColumnGroup());
        AddGroup("条形图 (Row)", BuildRowGroup());
        AddGroup("堆叠柱状/条形", BuildStackedBarGroup());
        AddGroup("饼图 & 仪表 (Pie & Gauge)", BuildPieGroup());
        AddGroup("散点 & 气泡", BuildScatterGroup());
        AddGroup("K线 & 箱线 & 误差", BuildFinancialGroup());
        AddGroup("热力图 (Heat)", BuildHeatGroup());
        AddGroup("极坐标 (Polar)", BuildPolarGroup());
        AddGroup("坐标轴 (Axes)", BuildAxesGroup());
        AddGroup("通用功能 (General)", BuildGeneralGroup());
        AddGroup("设计样式 (Design)", BuildDesignGroup());
    }

    // ── Line Group ───────────────────────────────────────────────────────

    private (string, Func<FrameworkElement>)[] BuildLineGroup() =>
    [
        ("Basic line — 基础折线", () => CC(c =>
        {
            c.Series =
            [
                new LineSeries<double>
                {
                    Values = Mock(12),
                    Stroke = Stroke(0),
                    Fill = null,
                    GeometrySize = 8,
                    GeometryStroke = Stroke(0),
                    GeometryFill = Paint(SKColors.White),
                    LineSmoothness = 0,
                },
            ];
        })),
        ("Smooth line — 平滑曲线", () => CC(c =>
        {
            c.Series =
            [
                new LineSeries<double>
                {
                    Values = Sine(18, 0.6),
                    Stroke = Stroke(0),
                    Fill = null,
                    GeometrySize = 8,
                    LineSmoothness = 1,
                },
            ];
        })),
        ("Straight line — 直角折线 (StepLine)", () => CC(c =>
        {
            c.Series =
            [
                new StepLineSeries<double>
                {
                    Values = Mock(10),
                    Stroke = Stroke(0),
                    Fill = null,
                    GeometrySize = 8,
                },
            ];
        })),
        ("Fill area — 填充面积", () => CC(c =>
        {
            c.Series =
            [
                new LineSeries<double>
                {
                    Values = Sine(12, 0.5),
                    Stroke = Stroke(0),
                    Fill = P(0, 40),
                    GeometrySize = 0,
                    LineSmoothness = 1,
                },
            ];
        })),
        ("No fill area — 半透明面积", () => CC(c =>
        {
            c.Series =
            [
                new LineSeries<double>
                {
                    Values = Sine(14, 0.4, 35, 45),
                    Stroke = Stroke(1),
                    Fill = P(1, 30),
                    GeometrySize = 6,
                    GeometryStroke = Stroke(1),
                    GeometryFill = Paint(SKColors.White),
                    LineSmoothness = 0.5f,
                },
            ];
        })),
        ("Custom geometries — 自定义点大小", () => CC(c =>
        {
            c.Series =
            [
                new LineSeries<double>
                {
                    Values = Mock(10),
                    Stroke = Stroke(0, 3),
                    Fill = null,
                    GeometrySize = 18,
                    GeometryStroke = Stroke(0, 3),
                    GeometryFill = P(0, 60),
                    LineSmoothness = 0,
                },
            ];
        })),
        ("Dual series — 双折线对比", () => CC(c =>
        {
            c.Series =
            [
                new LineSeries<double>
                {
                    Values = Sine(10, 0.5, 25, 60),
                    Stroke = Stroke(0),
                    Fill = null,
                    GeometrySize = 8,
                    Name = "温度°C",
                },
                new LineSeries<double>
                {
                    Values = Sine(10, 0.4, 15, 30),
                    Stroke = Stroke(1),
                    Fill = null,
                    GeometrySize = 8,
                    Name = "湿度%",
                },
            ];
            c.LegendPosition = LegendPosition.Right;
        })),
        ("XY scatter points — 自定义XY点", () => CC(c =>
        {
            c.Series =
            [
                new LineSeries<ObservablePoint>
                {
                    Values = ScatterPts(10),
                    Mapping = (p, _) => new Coordinate(p.X ?? 0, p.Y ?? 0),
                    Stroke = Stroke(1),
                    Fill = null,
                    GeometrySize = 10,
                    LineSmoothness = 0,
                },
            ];
        })),
        ("Zoom and pan — 缩放平移", () => CC(c =>
        {
            c.Series =
            [
                new LineSeries<double>
                {
                    Values = Sine(40, 0.3),
                    Stroke = Stroke(0),
                    Fill = null,
                    GeometrySize = 6,
                },
            ];
            c.ZoomMode = ZoomAndPanMode.Both;
        })),
        ("Wind direction — 风向图", () => CC(c =>
        {
            c.Series =
            [
                new ScatterSeries<double>
                {
                    Values = Mock(15),
                    GeometrySize = 20,
                    Stroke = null,
                    Fill = P(2, 80),
                },
            ];
            c.YAxes = [new Axis { MinLimit = 0, MaxLimit = 360, Name = "风向 (°)" }];
        })),
    ];

    // ── Area Group ───────────────────────────────────────────────────────

    private (string, Func<FrameworkElement>)[] BuildAreaGroup() =>
    [
        ("Basic area — 基础面积", () => CC(c =>
        {
            c.Series =
            [
                new LineSeries<double>
                {
                    Values = Sine(12, 0.5),
                    Stroke = Stroke(0, 2),
                    Fill = P(0, 50),
                    GeometrySize = 0,
                    LineSmoothness = 1,
                },
            ];
        })),
        ("Dual area — 双面积对比", () => CC(c =>
        {
            c.Series =
            [
                new LineSeries<double>
                {
                    Values = Sine(10, 0.5, 20, 55),
                    Stroke = Stroke(0),
                    Fill = P(0, 35),
                    GeometrySize = 0,
                    Name = "Series A",
                },
                new LineSeries<double>
                {
                    Values = Sine(10, 0.4, 18, 35),
                    Stroke = Stroke(1),
                    Fill = P(1, 35),
                    GeometrySize = 0,
                    Name = "Series B",
                },
            ];
            c.LegendPosition = LegendPosition.Right;
        })),
        ("Stacked area — 堆叠面积", () => CC(c =>
        {
            c.Series =
            [
                new StackedAreaSeries<double> { Values = Mock(8), Fill = P(0, 60), Stroke = null },
                new StackedAreaSeries<double> { Values = Mock(8), Fill = P(1, 60), Stroke = null },
                new StackedAreaSeries<double> { Values = Mock(8), Fill = P(2, 60), Stroke = null },
            ];
            c.LegendPosition = LegendPosition.Right;
        })),
        ("Stacked step area — 堆叠阶梯面积", () => CC(c =>
        {
            c.Series =
            [
                new StackedStepAreaSeries<double> { Values = Mock(8), Fill = P(0, 55), Stroke = null },
                new StackedStepAreaSeries<double> { Values = Mock(8), Fill = P(1, 55), Stroke = null },
                new StackedStepAreaSeries<double> { Values = Mock(8), Fill = P(2, 55), Stroke = null },
            ];
            c.LegendPosition = LegendPosition.Right;
        })),
    ];

    // ── Stack Group ──────────────────────────────────────────────────────

    private (string, Func<FrameworkElement>)[] BuildStackGroup() =>
    [
        ("Step line — 阶梯折线", () => CC(c =>
        {
            c.Series =
            [
                new StepLineSeries<double>
                {
                    Values = Mock(10),
                    Stroke = Stroke(0, 3),
                    Fill = null,
                    GeometrySize = 8,
                    GeometryStroke = Stroke(0),
                    GeometryFill = Paint(SKColors.White),
                },
            ];
        })),
        ("Dual step line — 双阶梯折线", () => CC(c =>
        {
            c.Series =
            [
                new StepLineSeries<double>
                {
                    Values = Mock(10),
                    Stroke = Stroke(0),
                    Fill = null,
                    GeometrySize = 6,
                    Name = "通道A",
                },
                new StepLineSeries<double>
                {
                    Values = Mock(10),
                    Stroke = Stroke(1),
                    Fill = null,
                    GeometrySize = 6,
                    Name = "通道B",
                },
            ];
            c.LegendPosition = LegendPosition.Right;
        })),
        ("Stacked area — 堆叠面积", () => CC(c =>
        {
            c.Series =
            [
                new StackedAreaSeries<double> { Values = Mock(8), Fill = P(0, 60), Stroke = null },
                new StackedAreaSeries<double> { Values = Mock(8), Fill = P(1, 60), Stroke = null },
                new StackedAreaSeries<double> { Values = Mock(8), Fill = P(2, 60), Stroke = null },
            ];
            c.LegendPosition = LegendPosition.Right;
        })),
        ("Stacked step area — 堆叠阶梯面积", () => CC(c =>
        {
            c.Series =
            [
                new StackedStepAreaSeries<double> { Values = Mock(8), Fill = P(0, 50), Stroke = null },
                new StackedStepAreaSeries<double> { Values = Mock(8), Fill = P(1, 50), Stroke = null },
            ];
            c.LegendPosition = LegendPosition.Right;
        })),
    ];

    // ── Column Group ─────────────────────────────────────────────────────

    private (string, Func<FrameworkElement>)[] BuildColumnGroup() =>
    [
        ("Basic columns — 基础柱状图", () => CC(c =>
        {
            c.Series =
            [
                new ColumnSeries<double> { Values = [3, 5, 4, 6, 2], Stroke = null, Fill = P(0) },
                new ColumnSeries<double> { Values = [4, 2, 5, 3, 6], Stroke = null, Fill = P(1) },
            ];
            c.XAxes = [new Axis { Labels = Cat5 }];
            c.LegendPosition = LegendPosition.Right;
        })),
        ("Custom stroke — 自定义边框", () => CC(c =>
        {
            c.Series =
            [
                new ColumnSeries<double>
                {
                    Values = Mock(6),
                    Stroke = Stroke(2, 2),
                    Fill = P(2, 40),
                },
            ];
        })),
        ("With background — 带背景色", () => CC(c =>
        {
            c.Series =
            [
                new ColumnSeries<double>
                {
                    Values = [20, 50, 40, 20, 40, 30, 50, 20, 50, 40],
                    Stroke = null,
                    Fill = P(0, 80),
                    MaxBarWidth = 20,
                },
            ];
        })),
        ("Spacing — 自定义间距", () => CC(c =>
        {
            c.Series =
            [
                new ColumnSeries<double>
                {
                    Values = [20, 50, 40, 20, 40, 30, 50, 20, 50, 40],
                    Stroke = null,
                    Fill = P(3),
                    MaxBarWidth = 15,
                },
            ];
            c.XAxes = [new Axis { Labels = CatMonth }];
            c.LegendPosition = LegendPosition.Right;
        })),
        ("Layered — 层叠柱状图", () => CC(c =>
        {
            c.Series =
            [
                new ColumnSeries<double>
                {
                    Values = [6, 3, 5, 7, 3, 4, 6, 3],
                    Stroke = null,
                    Fill = P(0, 60),
                    MaxBarWidth = 40,
                },
                new ColumnSeries<double>
                {
                    Values = [2, 4, 8, 9, 5, 2, 4, 7],
                    Stroke = null,
                    Fill = P(1, 60),
                    MaxBarWidth = 20,
                },
            ];
            c.LegendPosition = LegendPosition.Right;
        })),
        ("Delayed animation — 延迟动画", () => CC(c =>
        {
            // Note: full delayed-animation requires event hooks;
            // this is a simplified visual demo with staggered feel
            c.Series =
            [
                new ColumnSeries<double>
                {
                    Values = Mock(7),
                    Stroke = null,
                    Fill = P(0),
                    AnimationsSpeed = TimeSpan.FromMilliseconds(800),
                },
            ];
        })),
    ];

    // ── Row Group ────────────────────────────────────────────────────────

    private (string, Func<FrameworkElement>)[] BuildRowGroup() =>
    [
        ("Basic rows — 基础条形图", () => CC(c =>
        {
            c.Series =
            [
                new RowSeries<double> { Values = Mock(5), Stroke = null, Fill = P(4) },
            ];
            c.YAxes = [new Axis { Labels = RowLbl }];
        })),
        ("Dual rows — 双条形对比", () => CC(c =>
        {
            c.Series =
            [
                new RowSeries<double> { Values = [8, -3, 4], Stroke = null, Fill = P(0) },
                new RowSeries<double> { Values = [4, -6, 5], Stroke = null, Fill = P(1) },
                new RowSeries<double> { Values = [6, -9, 3], Stroke = null, Fill = P(2) },
            ];
            c.YAxes = [new Axis { Labels = ["产品A", "产品B", "产品C"] }];
            c.LegendPosition = LegendPosition.Right;
        })),
        ("Negative rows — 负值条形图", () => CC(c =>
        {
            c.Series =
            [
                new RowSeries<double>
                {
                    Values = [5, -3, 7, -2, 4],
                    Stroke = null,
                    Fill = P(5),
                },
            ];
            c.YAxes = [new Axis { Labels = ["Q1", "Q2", "Q3", "Q4", "Q5"] }];
        })),
    ];

    // ── Stacked Bar Group ────────────────────────────────────────────────

    private (string, Func<FrameworkElement>)[] BuildStackedBarGroup() =>
    [
        ("Stacked columns — 堆叠柱状图", () => CC(c =>
        {
            c.Series =
            [
                new StackedColumnSeries<double> { Values = Mock(5), Stroke = null, Fill = P(0), Name = "A" },
                new StackedColumnSeries<double> { Values = Mock(5), Stroke = null, Fill = P(1), Name = "B" },
                new StackedColumnSeries<double> { Values = Mock(5), Stroke = null, Fill = P(2), Name = "C" },
            ];
            c.XAxes = [new Axis { Labels = StackCat }];
            c.LegendPosition = LegendPosition.Right;
        })),
        ("Stacked columns with negatives — 含负值堆叠", () => CC(c =>
        {
            c.Series =
            [
                new StackedColumnSeries<double> { Values = [3, 5, -3, 2, 5, -4, -2], Stroke = null, Fill = P(0) },
                new StackedColumnSeries<double> { Values = [4, 2, -3, 2, 3, 4, -2], Stroke = null, Fill = P(1) },
                new StackedColumnSeries<double> { Values = [-2, 6, 6, 5, 4, 3, -2], Stroke = null, Fill = P(2) },
            ];
            c.LegendPosition = LegendPosition.Right;
        })),
        ("Stacked groups — 堆叠分组", () => CC(c =>
        {
            c.Series =
            [
                new StackedColumnSeries<double> { Values = [3, 5, 3], Stroke = null, Fill = P(0), Name = "G1" },
                new StackedColumnSeries<double> { Values = [4, 2, 3], Stroke = null, Fill = P(1), Name = "G2" },
                new StackedColumnSeries<double> { Values = [4, 6, 6], Stroke = null, Fill = P(2), Name = "G3" },
                new StackedColumnSeries<double> { Values = [2, 5, 4], Stroke = null, Fill = P(3), Name = "G4" },
            ];
            c.XAxes = [new Axis { Labels = ["类别1", "类别2", "类别3"] }];
            c.LegendPosition = LegendPosition.Right;
        })),
        ("Stacked rows — 堆叠条形图", () => CC(c =>
        {
            c.Series =
            [
                new StackedRowSeries<double> { Values = Mock(5), Stroke = null, Fill = P(0), Name = "A" },
                new StackedRowSeries<double> { Values = Mock(5), Stroke = null, Fill = P(1), Name = "B" },
            ];
            c.YAxes = [new Axis { Labels = RowLbl }];
            c.LegendPosition = LegendPosition.Right;
        })),
    ];

    // ── Pie & Gauge Group ────────────────────────────────────────────────

    private (string, Func<FrameworkElement>)[] BuildPieGroup() =>
    [
        ("Basic pie — 基础饼图", () => PC(c =>
        {
            c.Series = PieV(new double[] { 4, 6, 3, 5, 2 });
            c.LegendPosition = LegendPosition.Right;
        })),
        ("Pushout — 突出扇区", () => PC(c =>
        {
            var data = new double[] { 4, 6, 3, 5, 2 };
            c.Series = data.Select((x, i) =>
                new PieSeries<double>
                {
                    Values = new double[] { x },
                    Fill = P(i),
                    Stroke = null,
                    Pushout = i == 2 ? 20 : 0,
                } as ISeries).ToArray();
            c.LegendPosition = LegendPosition.Right;
        })),
        ("Doughnut — 环形图", () => PC(c =>
        {
            var data = new double[] { 5, 3, 4, 6, 2 };
            c.Series = data.Select((x, i) =>
                new PieSeries<double>
                {
                    Values = new double[] { x },
                    Fill = P(i),
                    Stroke = null,
                    InnerRadius = 55,
                } as ISeries).ToArray();
            c.LegendPosition = LegendPosition.Right;
        })),
        ("Nightingale rose — 南丁格尔玫瑰图", () => PC(c =>
        {
            var data = new double[] { 10, 20, 30, 40 };
            c.Series = data.Select((x, i) =>
                new PieSeries<double>
                {
                    Values = new double[] { x },
                    Fill = P(i),
                    Stroke = null,
                    InnerRadius = i * 20,
                } as ISeries).ToArray();
            c.LegendPosition = LegendPosition.Right;
        })),
        ("Basic gauge — 基础仪表 65%", () => PC(c =>
        {
            c.Series = new ISeries[]
            {
                new PieSeries<double> { Values = new double[] { 65.0 }, Fill = P(0), Stroke = null, InnerRadius = 70 },
                new PieSeries<double> { Values = new double[] { 35.0 }, Fill = Paint(new SKColor(230, 230, 230)), Stroke = null, InnerRadius = 70 },
            };
            c.MaxValue = 100;
        })),
        ("Slim gauge — 细环仪表 72%", () => PC(c =>
        {
            c.Series = new ISeries[]
            {
                new PieSeries<double> { Values = new double[] { 72.0 }, Fill = P(2), Stroke = null, InnerRadius = 80 },
                new PieSeries<double> { Values = new double[] { 28.0 }, Fill = Paint(new SKColor(230, 230, 230)), Stroke = null, InnerRadius = 80 },
            };
            c.MaxValue = 100;
        })),
        ("Gauge with label — 仪表带标签", () => PC(c =>
        {
            c.Series = new ISeries[]
            {
                new PieSeries<double> { Values = new double[] { 82.0 }, Fill = P(1), Stroke = null, InnerRadius = 75 },
                new PieSeries<double> { Values = new double[] { 18.0 }, Fill = Paint(new SKColor(230, 230, 230)), Stroke = null, InnerRadius = 75 },
            };
            c.MaxValue = 100;
        })),
        ("Angular gauge style — 角度仪表风格", () => PC(c =>
        {
            c.Series = new ISeries[]
            {
                new PieSeries<double> { Values = new double[] { 240.0 }, Fill = P(3), Stroke = null, InnerRadius = 65 },
                new PieSeries<double> { Values = new double[] { 120.0 }, Fill = Paint(new SKColor(230, 230, 230)), Stroke = null, InnerRadius = 65 },
            };
            c.MaxValue = 360;
        })),
    ];

    // ── Scatter Group ────────────────────────────────────────────────────

    private (string, Func<FrameworkElement>)[] BuildScatterGroup() =>
    [
        ("Basic scatter — 基础散点图", () => CC(c =>
        {
            c.Series =
            [
                new ScatterSeries<double>
                {
                    Values = Mock(15),
                    Fill = P(0, 70),
                    Stroke = null,
                    GeometrySize = 15,
                },
            ];
        })),
        ("Bubble — 气泡图", () => CC(c =>
        {
            c.Series =
            [
                new ScatterSeries<ObservablePoint>
                {
                    Values = ScatterPts(10),
                    Mapping = (p, _) => new Coordinate(p.X ?? 0, p.Y ?? 0),
                    Fill = P(1, 60),
                    Stroke = null,
                    GeometrySize = 15,
                },
            ];
        })),
        ("Weighted bubble — 加权气泡 (大小=权重)", () => CC(c =>
        {
            c.Series =
            [
                new ScatterSeries<WeightedPoint>
                {
                    Values = WeightedPts(8),
                    Mapping = (p, _) => new Coordinate(p.X ?? 0, p.Y ?? 0),
                    Fill = P(2, 50),
                    Stroke = null,
                    GeometrySize = 20,
                },
            ];
        })),
        ("Custom marker — 自定义标记", () => CC(c =>
        {
            c.Series =
            [
                new ScatterSeries<double>
                {
                    Values = Mock(10),
                    Fill = P(5, 80),
                    Stroke = Stroke(5, 2),
                    GeometrySize = 25,
                },
            ];
        })),
        ("Scatter + Line overlay — 散点+折线叠加", () => CC(c =>
        {
            var pts = ScatterPts(8);
            c.Series = new ISeries[]
            {
                new LineSeries<ObservablePoint>
                {
                    Values = pts,
                    Mapping = (p, _) => new Coordinate(p.X ?? 0, p.Y ?? 0),
                    Stroke = Stroke(0),
                    Fill = null,
                    GeometrySize = 0,
                    LineSmoothness = 0,
                },
                new ScatterSeries<ObservablePoint>
                {
                    Values = pts,
                    Mapping = (p, _) => new Coordinate(p.X ?? 0, p.Y ?? 0),
                    Fill = P(0, 80),
                    Stroke = null,
                    GeometrySize = 14,
                },
            };
        })),
    ];

    // ── Financial Group ──────────────────────────────────────────────────

    private (string, Func<FrameworkElement>)[] BuildFinancialGroup() =>
    [
        ("Candlesticks — K线图", () => CC(c =>
        {
            var fi = Fin(10);
            c.Series =
            [
                new CandlesticksSeries<FinancialPoint>
                {
                    Values = fi,
                    MaxBarWidth = 20,
                },
            ];
            c.XAxes = [new DateTimeAxis(TimeSpan.FromDays(1), v => v.ToString("MM/dd"))];
        })),
        ("Candlesticks up/down colors — 涨跌色K线", () => CC(c =>
        {
            var fi = Fin(12);
            c.Series =
            [
                new CandlesticksSeries<FinancialPoint>
                {
                    Values = fi,
                    MaxBarWidth = 18,
                },
            ];
            c.XAxes = [new DateTimeAxis(TimeSpan.FromDays(1), v => v.ToString("MM/dd"))];
        })),
        ("Box series — 箱线图", () => CC(c =>
        {
            c.Series =
            [
                new BoxSeries<BoxValue>
                {
                    Values =
                    [
                        new BoxValue(100, 80, 60, 20, 70),
                        new BoxValue(90, 70, 50, 30, 60),
                        new BoxValue(80, 60, 40, 10, 50),
                        new BoxValue(85, 65, 45, 25, 55),
                        new BoxValue(95, 75, 55, 35, 65),
                    ],
                    Stroke = Stroke(4),
                    Fill = P(4, 50),
                },
            ];
            c.XAxes = [new Axis { Labels = ["组1", "组2", "组3", "组4", "组5"] }];
        })),
        ("Box series with negative — 负值箱线", () => CC(c =>
        {
            c.Series =
            [
                new BoxSeries<BoxValue>
                {
                    Values = new BoxValue[]
                    {
                        new(80, 60, 40, 10, 50),
                        new(70, 50, 30, 20, 40),
                        new(60, 40, 20, 10, 30),
                    },
                },
            ];
        })),
    ];

    // ── Heat Group ───────────────────────────────────────────────────────

    private (string, Func<FrameworkElement>)[] BuildHeatGroup() =>
    [
        ("Basic heat — 基础热力图", () => CC(c =>
        {
            c.Series =
            [
                new HeatSeries<WeightedPoint>
                {
                    Values = HeatData(4, 6),
                    HeatMap =
                    [
                        SKColor.Parse("#FFF176").AsLvcColor(),
                        SKColor.Parse("#2F4F4F").AsLvcColor(),
                        SKColor.Parse("#0000FF").AsLvcColor(),
                    ],
                },
            ];
            c.XAxes = [new Axis { Labels = ["A", "B", "C", "D"] }];
            c.YAxes = [new Axis { Labels = ["J", "F", "M", "A", "M", "J"] }];
        })),
        ("Heat with labels — 热力图带标签", () => CC(c =>
        {
            c.Series =
            [
                new HeatSeries<WeightedPoint>
                {
                    Values = HeatData(4, 6),
                    HeatMap =
                    [
                        SKColor.Parse("#00BCD4").AsLvcColor(),
                        SKColor.Parse("#FF9800").AsLvcColor(),
                        SKColor.Parse("#F44336").AsLvcColor(),
                    ],
                    PointPadding = new Padding(2),
                },
            ];
            c.XAxes = [new Axis { Labels = ["Charles", "Richard", "Ana", "Mari"] }];
            c.YAxes = [new Axis { Labels = ["Jan", "Feb", "Mar", "Apr", "May", "Jun"] }];
        })),
        ("Heat with custom range — 自定义色域", () => CC(c =>
        {
            c.Series =
            [
                new HeatSeries<WeightedPoint>
                {
                    Values = HeatData(5, 8),
                    HeatMap =
                    [
                        SKColor.Parse("#E8F5E9").AsLvcColor(),
                        SKColor.Parse("#66BB6A").AsLvcColor(),
                        SKColor.Parse("#1B5E20").AsLvcColor(),
                    ],
                },
            ];
        })),
    ];

    // ── Polar Group ──────────────────────────────────────────────────────

    private (string, Func<FrameworkElement>)[] BuildPolarGroup() =>
    [
        ("Basic polar — 基础极坐标", () => PolC(c =>
        {
            c.Series =
            [
                new PolarLineSeries<double>
                {
                    Values = [12, 10, 14, 8, 11, 9, 13, 7],
                    GeometrySize = 10,
                    IsClosed = true,
                    Stroke = Stroke(0),
                    Fill = P(0, 30),
                },
            ];
            c.RadiusAxes = [new PolarAxis { MaxLimit = 20 }];
        })),
        ("Radial area — 径向面积", () => PolC(c =>
        {
            c.Series =
            [
                new PolarLineSeries<double>
                {
                    Values = [7, 5, 7, 5, 6],
                    GeometrySize = 8,
                    IsClosed = true,
                    Stroke = Stroke(1),
                    Fill = P(1, 35),
                },
                new PolarLineSeries<double>
                {
                    Values = [2, 7, 5, 9, 7],
                    GeometrySize = 8,
                    IsClosed = true,
                    Stroke = Stroke(2),
                    Fill = P(2, 35),
                },
            ];
            c.AngleAxes = [new PolarAxis { Labels = ["一", "二", "三", "四", "五"] }];
        })),
        ("Polar with coordinates — 极坐标点", () => PolC(c =>
        {
            c.Series =
            [
                new PolarLineSeries<double>
                {
                    Values = [15, 14, 13, 12, 11, 10, 9, 8, 7, 6, 5, 4, 3, 2, 1],
                    GeometrySize = 6,
                    IsClosed = true,
                    Stroke = Stroke(4),
                    Fill = P(4, 20),
                },
            ];
        })),
        ("Polar scatter — 极坐标散点", () => PolC(c =>
        {
            c.Series =
            [
                new PolarLineSeries<double>
                {
                    Values = Mock(12),
                    GeometrySize = 14,
                    IsClosed = false,
                    Stroke = Stroke(5, 3),
                    Fill = null,
                },
            ];
        })),
    ];

    // ── Axes Group ───────────────────────────────────────────────────────

    private (string, Func<FrameworkElement>)[] BuildAxesGroup() =>
    [
        ("Axis labels — 坐标轴标签", () => CC(c =>
        {
            c.Series =
            [
                new ColumnSeries<double> { Values = Mock(6), Stroke = null, Fill = P(0) },
            ];
            c.XAxes = [new Axis { Labels = Cat6 }];
        })),
        ("Multi axes — 多Y轴", () => CC(c =>
        {
            c.Series =
            [
                new ColumnSeries<double> { Values = Mock(6), Stroke = null, Fill = P(0), Name = "柱状" },
                new LineSeries<double> { Values = Sine(6, 0.5), Fill = null, Stroke = Stroke(1), GeometrySize = 8, ScalesYAt = 1, Name = "折线" },
            ];
            c.YAxes =
            [
                new Axis { Name = "左轴", MinLimit = 0 },
                new Axis { Name = "右轴", MinLimit = 0, Position = AxisPosition.End },
            ];
            c.LegendPosition = LegendPosition.Right;
        })),
        ("Axis style — 坐标轴样式", () => CC(c =>
        {
            c.Series =
            [
                new LineSeries<double>
                {
                    Values = Sine(10, 0.5),
                    Fill = null,
                    Stroke = Stroke(2, 3),
                    GeometrySize = 8,
                },
            ];
            c.XAxes =
            [
                new Axis
                {
                    Labels = CatMonth,
                    LabelsPaint = new SolidColorPaint(SKColors.DarkSlateGray) { StrokeThickness = 0 },
                    TextSize = 14,
                },
            ];
            c.YAxes =
            [
                new Axis
                {
                    Name = "Value",
                    NamePaint = new SolidColorPaint(SKColors.DarkSlateGray) { StrokeThickness = 0 },
                    LabelsPaint = new SolidColorPaint(SKColors.Gray) { StrokeThickness = 0 },
                },
            ];
        })),
        ("DateTime axis — 时间轴", () => CC(c =>
        {
            var now = DateTime.UtcNow;
            var vs = Mock(10);
            c.Series =
            [
                new LineSeries<DateTimePoint>
                {
                    Values = Enumerable.Range(0, 10).Select(i => new DateTimePoint(now.AddDays(-9 + i), vs[i])).ToArray(),
                    Fill = null,
                    Stroke = Stroke(4),
                    GeometrySize = 8,
                    LineSmoothness = 0,
                },
            ];
            c.XAxes = [new DateTimeAxis(TimeSpan.FromDays(1), v => v.ToString("MM/dd"))];
        })),
        ("Logarithmic scale — 对数坐标", () => CC(c =>
        {
            // Manual approximation: large value range
            c.Series =
            [
                new LineSeries<double>
                {
                    Values = [1, 10, 100, 1000, 10000, 100000],
                    Stroke = Stroke(0),
                    Fill = null,
                    GeometrySize = 8,
                    LineSmoothness = 0,
                },
            ];
            c.YAxes = [new Axis { MinLimit = 0 }];
        })),
        ("Crosshairs — 十字准线", () => CC(c =>
        {
            c.Series =
            [
                new LineSeries<double>
                {
                    Values = [200, 558, 458, 249, 457, 339, 587],
                    Stroke = Stroke(3),
                    Fill = null,
                    GeometrySize = 8,
                },
            ];
            c.TooltipPosition = TooltipPosition.Top;
        })),
    ];

    // ── General Group ────────────────────────────────────────────────────

    private (string, Func<FrameworkElement>)[] BuildGeneralGroup() =>
    [
        ("Sections — 区域高亮", () => CC(c =>
        {
            c.Series =
            [
                new LineSeries<double>
                {
                    Values = Sine(12, 0.5),
                    Fill = null,
                    Stroke = Stroke(0),
                    GeometrySize = 0,
                },
            ];
            c.Sections =
            [
                new RectangularSection { Yi = 60, Yj = 80, Fill = P(3, 40) },
                new RectangularSection { Yi = 30, Yj = 50, Fill = P(1, 40) },
            ];
        })),
        ("Null points — 空值折线", () => CC(c =>
        {
            c.Series =
            [
                new LineSeries<double?>
                {
                    Values = [10, 15, null, 20, 18, null, 25, 22, 28, null, 30],
                    Fill = null,
                    Stroke = Stroke(3),
                    GeometrySize = 8,
                    LineSmoothness = 0,
                },
            ];
        })),
        ("Custom tooltips — 自定义提示", () => CC(c =>
        {
            c.Series =
            [
                new LineSeries<double> { Values = Sine(8, 0.5), Fill = null, Stroke = Stroke(0), GeometrySize = 8, Name = "温度°C" },
                new LineSeries<double> { Values = Sine(8, 0.4).Select(v => v / 2 + 30).ToArray(), Fill = null, Stroke = Stroke(1), GeometrySize = 8, Name = "湿度%" },
            ];
            c.TooltipPosition = TooltipPosition.Top;
            c.LegendPosition = LegendPosition.Right;
        })),
        ("Scrollable — 可滚动视图", () => CC(c =>
        {
            c.Series =
            [
                new LineSeries<double>
                {
                    Values = Mock(50),
                    Fill = null,
                    Stroke = Stroke(0),
                    GeometrySize = 6,
                },
            ];
            c.ZoomMode = ZoomAndPanMode.X;
        })),
        ("Real-time demo — 实时更新", () => CC(c =>
        {
            // Note: for a true real-time chart enable INotifyCollectionChanged
            // on the data source; this is a static preview
            c.Series =
            [
                new LineSeries<double>
                {
                    Values = Sine(20, 0.3, 20, 50),
                    Fill = null,
                    Stroke = Stroke(1),
                    GeometrySize = 6,
                    LineSmoothness = 0.3f,
                },
            ];
            c.ZoomMode = ZoomAndPanMode.X;
            c.TooltipPosition = TooltipPosition.Top;
        })),
        ("Visual elements — 视觉元素叠加", () => CC(c =>
        {
            c.Series =
            [
                new LineSeries<double>
                {
                    Values = Sine(10, 0.5),
                    Fill = null,
                    Stroke = Stroke(0),
                    GeometrySize = 8,
                },
            ];
        })),
        ("Conditional draw — 条件绘制", () => CC(c =>
        {
            c.Series =
            [
                new LineSeries<double>
                {
                    Values = [12, 8, 15, 6, 18, 10, 22, 14, 9, 20],
                    Fill = null,
                    Stroke = Stroke(4),
                    GeometrySize = 8,
                    LineSmoothness = 0,
                },
            ];
        })),
    ];

    // ── Design Group ─────────────────────────────────────────────────────

    private (string, Func<FrameworkElement>)[] BuildDesignGroup() =>
    [
        ("Linear gradient — 线性渐变", () => CC(c =>
        {
            var g = new LinearGradientPaint(
                [new SKColor(64, 158, 255), new SKColor(103, 194, 58)],
                new SKPoint(0, 0),
                new SKPoint(1, 1));
            c.Series =
            [
                new LineSeries<double>
                {
                    Values = Sine(10, 0.5),
                    Fill = null,
                    Stroke = g,
                    GeometrySize = 8,
                    GeometryStroke = g,
                    GeometryFill = null,
                    LineSmoothness = 1,
                },
            ];
        })),
        ("Radial gradient — 径向渐变", () => CC(c =>
        {
            var g = new RadialGradientPaint(
                [new SKColor(245, 108, 108), new SKColor(64, 158, 255)],
                new SKPoint(0.5f, 0.5f),
                1.5f);
            c.Series =
            [
                new ColumnSeries<double>
                {
                    Values = [3, 7, 2, 9, 4],
                    Stroke = null,
                    Fill = g,
                },
            ];
        })),
        ("Dashed stroke — 虚线笔触", () => CC(c =>
        {
            var s = new SolidColorPaint(SKColor.Parse("#F56C6C"), 4) { PathEffect = new DashEffect(new float[] { 6, 4 }) };
            c.Series =
            [
                new LineSeries<double>
                {
                    Values = Sine(10, 0.5),
                    Fill = null,
                    Stroke = s,
                    GeometrySize = 8,
                    GeometryStroke = s,
                    LineSmoothness = 1,
                },
            ];
        })),
        ("Dotted stroke — 点线笔触", () => CC(c =>
        {
            var s = new SolidColorPaint(SKColor.Parse("#67C23A"), 3) { PathEffect = new DashEffect(new float[] { 2, 4 }) };
            c.Series =
            [
                new LineSeries<double>
                {
                    Values = Sine(10, 0.4, 20, 40),
                    Fill = null,
                    Stroke = s,
                    GeometrySize = 6,
                    GeometryStroke = s,
                    LineSmoothness = 0,
                },
            ];
        })),
        ("Dash-dot stroke — 点划线", () => CC(c =>
        {
            var s = new SolidColorPaint(SKColor.Parse("#B37FEB"), 3) { PathEffect = new DashEffect(new float[] { 8, 3, 2, 3 }) };
            c.Series =
            [
                new LineSeries<double>
                {
                    Values = Sine(12, 0.45, 25, 50),
                    Fill = null,
                    Stroke = s,
                    GeometrySize = 6,
                    GeometryStroke = s,
                    LineSmoothness = 0.5f,
                },
            ];
        })),
        ("Thick stroke — 粗线条", () => CC(c =>
        {
            c.Series =
            [
                new LineSeries<double>
                {
                    Values = Sine(8, 0.5),
                    Fill = null,
                    Stroke = Stroke(0, 6),
                    GeometrySize = 12,
                    GeometryStroke = Stroke(0, 6),
                    GeometryFill = Paint(SKColors.White),
                    LineSmoothness = 1,
                },
            ];
        })),
    ];

    // ── Layout ───────────────────────────────────────────────────────────

    private void AddGroup(string title, (string name, Func<FrameworkElement> factory)[] charts)
    {
        var stack = new StackPanel { Margin = new Thickness(8) };
        foreach (var (name, factory) in charts)
        {
            var border = new Border
            {
                BorderBrush = System.Windows.Media.Brushes.LightGray,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Margin = new Thickness(4, 4, 4, 8),
                Height = 350,
            };
            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(28) });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            // Title bar
            var titleBar = new Grid();
            titleBar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            titleBar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var tb = new TextBlock
            {
                Text = name,
                FontWeight = FontWeights.SemiBold,
                FontSize = 14,
                Margin = new Thickness(10, 4, 0, 2),
                VerticalAlignment = VerticalAlignment.Center,
            };
            titleBar.Children.Add(tb);

            grid.Children.Add(titleBar);

            try
            {
                var chart = factory();
                chart.Margin = new Thickness(8, 4, 8, 8);
                Grid.SetRow(chart, 1);
                grid.Children.Add(chart);
            }
            catch (Exception ex)
            {
                var err = new TextBlock
                {
                    Text = "⚠ " + ex.Message,
                    Foreground = System.Windows.Media.Brushes.Red,
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(10),
                };
                Grid.SetRow(err, 1);
                grid.Children.Add(err);
            }
            border.Child = grid;
            stack.Children.Add(border);
        }
        _rootPanel.Children.Add(new GroupBox { Header = title, Margin = new Thickness(0, 0, 0, 12), Content = stack });
    }

    // ── Data Helpers ─────────────────────────────────────────────────────

    private static ObservablePoint[] ScatterPts(int c) =>
        Enumerable.Range(0, c).Select(_ => new ObservablePoint((double)Rng.Next(1, 20), (double)Rng.Next(1, 20))).ToArray();

    private static WeightedPoint[] WeightedPts(int c) =>
        Enumerable.Range(0, c).Select(_ => new WeightedPoint((double)Rng.Next(1, 20), (double)Rng.Next(1, 20), Rng.Next(30, 200))).ToArray();

    private static WeightedPoint[] HeatData(int cols, int rows)
    {
        var d = new WeightedPoint[cols * rows];
        var k = 0;
        for (var r = 0; r < cols; r++)
            for (var c = 0; c < rows; c++)
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

    // ── Chart Builders ───────────────────────────────────────────────────

    private static CartesianChart CC(Action<CartesianChart> setup)
    {
        var c = new CartesianChart
        {
            LegendPosition = LegendPosition.Hidden,
            AnimationsSpeed = TimeSpan.FromMilliseconds(400),
        };
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

    private static ISeries[] PieV(double[] vals) =>
        vals.Select((v, i) => new PieSeries<double> { Values = [v], Fill = P(i), Stroke = null } as ISeries).ToArray();
}
