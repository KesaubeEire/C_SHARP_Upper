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

    public void UpdateMultiGauge(double[] values)
    {
        var seriesProps = new[] { section0, section1, section2, section3, section4 };
        var count = Math.Min(values.Length, seriesProps.Length);

        for (var i = 0; i < count; i++)
        {
            var normalized = Math.Clamp(values[i] / _sectionMax[i] * 100.0, 0, 100);
            if (i < SectionItems.Count)
                SectionItems[i].Value = values[i];
        }

        Dispatcher.InvokeAsync(() =>
        {
            for (var i = 0; i < count; i++)
            {
                var normalized = Math.Clamp(values[i] / _sectionMax[i] * 100.0, 0, 100);
                seriesProps[i].GaugeValue = normalized;
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
