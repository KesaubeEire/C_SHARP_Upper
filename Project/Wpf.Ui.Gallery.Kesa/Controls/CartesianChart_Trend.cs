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
            if (c.XAxes?.FirstOrDefault() is { } ax)
                ax.LabelsPaint = paint;
            if (c.YAxes?.FirstOrDefault() is { } ay)
                ay.LabelsPaint = paint;
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
