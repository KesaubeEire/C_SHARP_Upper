using System.Linq;
using System.Windows;
using System.Windows.Media;
using LiveChartsCore;
using LiveChartsCore.Painting;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using LiveChartsCore.SkiaSharpView.WPF;
using SkiaSharp;

namespace Wpf.Ui.Gallery.Controls;

public class CartesianChartKesaTrend : CartesianChartKesa
{
    public CartesianChartKesaTrend()
    {
        Loaded += (_, _) =>
        {
            // XAxes/YAxes 在构造函数阶段可能还未初始化，DP 回调设置画笔会丢失。
            // 控件加载完成后再应用一次，确保画笔生效。
            if (AxisGridBrush is not null)
                ApplyAxisGridBrush(AxisGridBrush);
            if (AxisLabelBrush is not null)
                ApplyAxisLabelBrush(AxisLabelBrush);
            if (TooltipBgBrush is not null)
                ApplyTooltipBgBrush(TooltipBgBrush);
        };
    }

    private void ApplyAxisGridBrush(Brush brush)
    {
        var paint = new SolidColorPaint(BrushToSkColor(brush)) { StrokeThickness = 0.5f };
        if (XAxes?.FirstOrDefault() is { } ax)
            ax.SeparatorsPaint = paint;
        if (YAxes?.FirstOrDefault() is { } ay)
            ay.SeparatorsPaint = paint;
    }

    private void ApplyAxisLabelBrush(Brush brush)
    {
        var paint = new SolidColorPaint(BrushToSkColor(brush));
        if (XAxes?.FirstOrDefault() is Axis ax)
        {
            ax.LabelsPaint = paint;
            ax.NamePaint = paint;
        }
        if (YAxes?.FirstOrDefault() is Axis ay)
        {
            ay.LabelsPaint = paint;
            ay.NamePaint = paint;
        }
    }

    private void ApplyTooltipBgBrush(Brush brush)
    {
        TooltipBackgroundPaint = new SolidColorPaint(BrushToSkColor(brush));
    }

    public static readonly DependencyProperty AxisLabelBrushProperty =
        DependencyProperty.Register(nameof(AxisLabelBrush), typeof(Brush), typeof(CartesianChartKesaTrend),
            new PropertyMetadata(new SolidColorBrush(Colors.Gray), OnAxisLabelBrushChanged));

    public static readonly DependencyProperty AxisGridBrushProperty =
        DependencyProperty.Register(nameof(AxisGridBrush), typeof(Brush), typeof(CartesianChartKesaTrend),
            new PropertyMetadata(new SolidColorBrush(Colors.DimGray), OnAxisGridBrushChanged));

    public static readonly DependencyProperty TooltipBgBrushProperty =
        DependencyProperty.Register(nameof(TooltipBgBrush), typeof(Brush), typeof(CartesianChartKesaTrend),
            new PropertyMetadata(new SolidColorBrush(Color.FromArgb(230, 30, 30, 30)), OnTooltipBgBrushChanged));

    public Brush AxisLabelBrush
    {
        get => (Brush)GetValue(AxisLabelBrushProperty);
        set => SetValue(AxisLabelBrushProperty, value);
    }

    public Brush AxisGridBrush
    {
        get => (Brush)GetValue(AxisGridBrushProperty);
        set => SetValue(AxisGridBrushProperty, value);
    }

    public Brush TooltipBgBrush
    {
        get => (Brush)GetValue(TooltipBgBrushProperty);
        set => SetValue(TooltipBgBrushProperty, value);
    }

    private static SKColor BrushToSkColor(Brush brush)
    {
        if (brush is SolidColorBrush scb)
        {
            var c = scb.Color;
            return new SKColor(c.R, c.G, c.B, c.A);
        }
        return SKColors.Black;
    }

    private static void OnAxisLabelBrushChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is CartesianChartKesaTrend c && e.NewValue is Brush b)
        {
            var paint = new SolidColorPaint(BrushToSkColor(b));
            if (c.XAxes?.FirstOrDefault() is Axis ax)
            {
                ax.LabelsPaint = paint;
                ax.NamePaint = paint;
            }
            if (c.YAxes?.FirstOrDefault() is Axis ay)
            {
                ay.LabelsPaint = paint;
                ay.NamePaint = paint;
            }
        }
    }

    private static void OnAxisGridBrushChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is CartesianChartKesaTrend c && e.NewValue is Brush b)
        {
            var paint = new SolidColorPaint(BrushToSkColor(b)) { StrokeThickness = 0.5f };
            if (c.XAxes?.FirstOrDefault() is { } ax)
                ax.SeparatorsPaint = paint;
            if (c.YAxes?.FirstOrDefault() is { } ay)
                ay.SeparatorsPaint = paint;
        }
    }

    private static void OnTooltipBgBrushChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is CartesianChartKesaTrend c && e.NewValue is Brush b)
        {
            c.TooltipBackgroundPaint = new SolidColorPaint(BrushToSkColor(b));
        }
    }
}
