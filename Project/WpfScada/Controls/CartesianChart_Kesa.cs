using System.Windows;
using LiveChartsCore.SkiaSharpView.WPF;

namespace WpfScada.Controls;

/// <summary>
/// 自定义 CartesianChart，将常用视觉属性暴露为 DependencyProperty，
/// 可在 XAML 中直接设置，修改后无需重新编译。
/// </summary>
public class CartesianChartKesa : CartesianChart
{
    /// <summary>线的粗细，默认 2</summary>
    public static readonly DependencyProperty LineStrokeThicknessProperty = DependencyProperty.Register(
        nameof(LineStrokeThickness),
        typeof(double),
        typeof(CartesianChartKesa),
        new PropertyMetadata(2.0));

    /// <summary>曲线平滑度 0~1，默认 0.3</summary>
    public static readonly DependencyProperty LineSmoothnessProperty = DependencyProperty.Register(
        nameof(LineSmoothness),
        typeof(double),
        typeof(CartesianChartKesa),
        new PropertyMetadata(0.3));

    /// <summary>数据点大小，0=隐藏，默认 0</summary>
    public static readonly DependencyProperty GeometrySizeProperty = DependencyProperty.Register(
        nameof(GeometrySize),
        typeof(double),
        typeof(CartesianChartKesa),
        new PropertyMetadata(0.0));

    /// <summary>线下填充透明度 0~1，0=无填充，默认 0</summary>
    public static readonly DependencyProperty FillOpacityProperty = DependencyProperty.Register(
        nameof(FillOpacity),
        typeof(double),
        typeof(CartesianChartKesa),
        new PropertyMetadata(0.0));

    public double LineStrokeThickness
    {
        get => (double)GetValue(LineStrokeThicknessProperty);
        set => SetValue(LineStrokeThicknessProperty, value);
    }

    public double LineSmoothness
    {
        get => (double)GetValue(LineSmoothnessProperty);
        set => SetValue(LineSmoothnessProperty, value);
    }

    public double GeometrySize
    {
        get => (double)GetValue(GeometrySizeProperty);
        set => SetValue(GeometrySizeProperty, value);
    }

    public double FillOpacity
    {
        get => (double)GetValue(FillOpacityProperty);
        set => SetValue(FillOpacityProperty, value);
    }
}
