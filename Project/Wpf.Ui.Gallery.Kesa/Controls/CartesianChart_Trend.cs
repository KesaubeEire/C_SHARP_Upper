using System.Windows;
using System.Windows.Media;
using LiveChartsCore;
using LiveChartsCore.Painting;
using LiveChartsCore.SkiaSharpView.Painting;
using LiveChartsCore.SkiaSharpView.WPF;
using SkiaSharp;

namespace Wpf.Ui.Gallery.Controls;

/// <summary>
/// 继承自 <see cref="CartesianChartKesa"/>，将图表主题色（轴线文字、网格线、Tooltip 背景）
/// 暴露为 <see cref="DependencyProperty"/>，支持 XAML 中 <c>{DynamicResource}</c> 绑定。
/// </summary>
public class CartesianChartKesaTrend : CartesianChartKesa
{
    /// <summary>轴线标签文字色刷</summary>
    public static readonly DependencyProperty AxisLabelBrushProperty =
        DependencyProperty.Register(nameof(AxisLabelBrush), typeof(Brush), typeof(CartesianChartKesaTrend),
            new PropertyMetadata(new SolidColorBrush(Colors.Gray), OnAxisLabelBrushChanged));

    /// <summary>网格线 / 分隔线色刷</summary>
    public static readonly DependencyProperty AxisGridBrushProperty =
        DependencyProperty.Register(nameof(AxisGridBrush), typeof(Brush), typeof(CartesianChartKesaTrend),
            new PropertyMetadata(new SolidColorBrush(Colors.DimGray), OnAxisGridBrushChanged));

    /// <summary>Tooltip 背景色刷</summary>
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
            if (c.XAxes?.FirstOrDefault() is Axis axisX)
                axisX.LabelsPaint = paint;
            if (c.YAxes?.FirstOrDefault() is Axis axisY)
                axisY.LabelsPaint = paint;
        }
    }

    private static void OnAxisGridBrushChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is CartesianChartKesaTrend c && e.NewValue is Brush b)
        {
            var paint = new SolidColorPaint(BrushToSkColor(b)) { StrokeThickness = 0.5f };
            if (c.XAxes?.FirstOrDefault() is Axis axisX)
                axisX.SeparatorsPaint = paint;
            if (c.YAxes?.FirstOrDefault() is Axis axisY)
                axisY.SeparatorsPaint = paint;
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
