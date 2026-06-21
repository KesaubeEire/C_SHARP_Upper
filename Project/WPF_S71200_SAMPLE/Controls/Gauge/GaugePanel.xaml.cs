using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using LiveChartsCore.SkiaSharpView.WPF;
using SkiaSharp;
using TestWpf.Models;

namespace TestWpf.Controls.Gauge;

/// <summary>
/// 仪表盘面板 — 单值 AngularGauge + 多区段 AngularGauge
/// </summary>
public partial class GaugePanel : UserControl
{
    private bool _linked;
    private double _currentVal;

    private static readonly (string label, SKColor color)[] SectionDefs = [
        ("温度", SKColors.Crimson),
        ("压力", SKColors.Cyan),
        ("流量", SKColors.SeaGreen),
        ("液位", SKColors.Gold),
        ("伺服", SKColors.DodgerBlue),
    ];

    private readonly ObservableCollection<GaugeSectionItem> _sectionItems = [];

    public GaugePanel()
    {
        InitializeComponent();
        Loaded += (_, _) => InitGauge();
    }

    private void InitGauge()
    {
        UpdateSingleGauge(0);

        // 多区段仪表颜色
        var segColors = new SKColor[]
        {
            SKColors.Crimson, SKColors.Cyan, SKColors.SeaGreen,
            SKColors.Gold, SKColors.DodgerBlue
        };
        var segs = new[] { seg0, seg1, seg2, seg3, seg4 };
        for (int i = 0; i < segs.Length && i < segColors.Length; i++)
            segs[i].Fill = new SolidColorPaint(segColors[i]);

        // 多区段列表
        for (int i = 0; i < SectionDefs.Length; i++)
        {
            _sectionItems.Add(new GaugeSectionItem
            {
                Label = SectionDefs[i].label,
                Color = new SolidColorBrush(System.Windows.Media.Color.FromRgb(
                    SectionDefs[i].color.Red, SectionDefs[i].color.Green, SectionDefs[i].color.Blue)),
                Value = 0
            });
        }
        listGaugeSections.ItemsSource = _sectionItems;
    }

    // ===== 单值仪表更新 =====

    public void UpdateSingleGauge(double value)
    {
        // XAML 控件可能还未加载（TextChanged 在构造过程中就会触发）
        if (txtGaugeVal == null || gaugeNeedle == null || txtMin == null || txtMax == null) return;

        _currentVal = value;
        txtGaugeVal.Text = $"{value:F1} mm";

        double min = double.TryParse(txtMin.Text, out var mn) ? mn : 0;
        double max = double.TryParse(txtMax.Text, out var mx) ? mx : 100;
        double range = max - min;
        double normalized = range > 0 ? Math.Clamp((value - min) / range * 100.0, 0, 100) : 0;

        // 更新弧（彩色填充段）
        if (gaugeArc != null)
        {
            gaugeArc.GaugeValue = normalized;
            // 首次设置颜色：随值变化的渐变色（低→绿, 中→黄, 高→红）
            if (gaugeArc.Fill == null)
            {
                gaugeArc.Fill = new SolidColorPaint(SKColors.Cyan);
            }
        }

        // 更新指针
        gaugeNeedle.Value = normalized;
    }

    // ===== 多区段更新 =====

    public void UpdateMultiGauge(double[] values)
    {
        var segs = new[] { seg0, seg1, seg2, seg3, seg4 };
        for (int i = 0; i < segs.Length && i < values.Length; i++)
        {
            segs[i].GaugeValue = values[i];
            if (i < _sectionItems.Count)
                _sectionItems[i].Value = values[i];
        }
    }

    // ===== Mock 联动 =====

    public void OnTrendSample(string key, double val)
    {
        if (!_linked) return;
        if (key == "ch_servo")
            UpdateSingleGauge(val);
    }

    private void OnLinkMock(object sender, RoutedEventArgs e)
    {
        _linked = !_linked;
        btnLinkMock.Content = _linked ? "🔗 已联动" : "🔗 联动 Mock 数据";
        btnLinkMock.Background = _linked
            ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x27, 0xAE, 0x60))
            : new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x29, 0x80, 0xB9));
    }

    // ===== 配置变更 =====

    private void OnRangeChanged(object sender, TextChangedEventArgs e)
    {
        UpdateSingleGauge(_currentVal);
    }

    private void OnThicknessChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        // 弧厚度由 slider 控制，重新构建仪表
        angularGaugeSingle.Series = null; // force rebuild on next update
        UpdateSingleGauge(_currentVal);
    }
}

public class GaugeSectionItem : System.ComponentModel.INotifyPropertyChanged
{
    private string _label = "";
    private double _value;
    private System.Windows.Media.SolidColorBrush? _color;

    public string Label { get => _label; set { _label = value; OnChanged(); } }
    public double Value { get => _value; set { _value = value; OnChanged(); } }
    public System.Windows.Media.SolidColorBrush? Color { get => _color; set { _color = value; OnChanged(); } }

    public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
    private void OnChanged([System.Runtime.CompilerServices.CallerMemberName] string? n = null)
        => PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(n));
}
