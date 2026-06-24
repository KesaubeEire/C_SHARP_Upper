using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Wpf.Ui.Gallery.Models.Plc;
using Wpf.Ui.Gallery.Services.Plc;

namespace Wpf.Ui.Gallery.Views.Pages.Plc;

public partial class GaugeDashboardPage : Page
{
    private double _currentValue;
    private readonly double[] _sectionMax = [200, 20, 80, 100, 100];
    private readonly string[] _sectionLabels = ["温度", "压力", "流量", "液位", "伺服"];

    public ObservableCollection<GaugeSectionItem> SectionItems { get; } = [];

    public GaugeDashboardPage(S7Service s7)
    {
        InitializeComponent();
        InitSectionItems();

        // WPF DP 注册默认值 0 时 XAML 设 CornerRadius="0" 不触发 PropertyChangedCallback，
        // MapChangeToBaseType 不执行，导致核心 _baseType.CornerRadius 始终为默认值 0，
        // 且 WPF SetThemedValue (ReadLocalValue == UnsetValue 时) 会覆盖为非零值。
        // 设一个非零值 "占位"，使 ReadLocalValue 返回非 UnsetValue → 主题跳过覆盖。
        foreach (var gauge in new[] { singleGaugeGreen, singleGaugeYellow, singleGaugeRed })
            gauge.CornerRadius = 0.1;

        Loaded += OnPageLoaded;
    }

    private void OnPageLoaded(object sender, RoutedEventArgs e)
    {
        // 主题/图表初始化完成后，设回目标值 0
        foreach (var gauge in new[] { singleGaugeGreen, singleGaugeYellow, singleGaugeRed })
            gauge.CornerRadius = 0;
    }

    public void UpdateSingleGauge(double value)
    {
        _currentValue = value;
        Dispatcher.InvokeAsync(() =>
        {
            gaugeValueText.Text = value.ToString("F1");
            var clamped = Math.Clamp(value, 0, 100);
            singleNeedle.Value = clamped;

            // 根据当前值动态调整 3 段色的长度
            if (clamped <= 33)
            {
                singleGaugeGreen.GaugeValue = clamped;
                singleGaugeYellow.GaugeValue = 0;
                singleGaugeRed.GaugeValue = 0;
            }
            else if (clamped <= 66)
            {
                singleGaugeGreen.GaugeValue = 33;
                singleGaugeYellow.GaugeValue = clamped - 33;
                singleGaugeRed.GaugeValue = 0;
            }
            else
            {
                singleGaugeGreen.GaugeValue = 33;
                singleGaugeYellow.GaugeValue = 33;
                singleGaugeRed.GaugeValue = clamped - 66;
            }
        });
    }


    private void InitSectionItems()
    {
        SectionItems.Clear();
        var colors = new[]
        {
            Color.FromRgb(0xef, 0x44, 0x44),
            Color.FromRgb(0x22, 0xd3, 0xee),
            Color.FromRgb(0x4a, 0xde, 0x80),
            Color.FromRgb(0xf5, 0x9e, 0x0b),
            Color.FromRgb(0xa7, 0x8b, 0xfa),
        };

        for (var i = 0; i < colors.Length; i++)
        {
            SectionItems.Add(new GaugeSectionItem(
                _sectionLabels[i],
                new SolidColorBrush(colors[i]),
                0));
        }
    }
}
