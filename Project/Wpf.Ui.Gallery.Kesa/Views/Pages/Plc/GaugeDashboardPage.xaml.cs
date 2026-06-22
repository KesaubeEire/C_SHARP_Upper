using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using SkiaSharp;
using SkiaSharp.Views.Desktop;
using SkiaSharp.Views.WPF;
using Wpf.Ui.Gallery.Models.Plc;
using Wpf.Ui.Gallery.Services.Plc;

namespace Wpf.Ui.Gallery.Views.Pages.Plc;

public partial class GaugeDashboardPage : Page
{
    private double _currentValue;
    private readonly double[] _sectionValues = [85, 8, 28, 60, 45];
    private readonly string[] _sectionLabels = ["温度", "压力", "流量", "液位", "伺服"];
    private readonly SKColor[] _sectionColors = [SKColors.Red, SKColors.Cyan, SKColors.LimeGreen, SKColors.Orange, SKColors.Magenta];
    private readonly double[] _sectionMax = [200, 20, 80, 100, 100];

    public ObservableCollection<GaugeSectionItem> SectionItems { get; } = [];

    private static SKColor ThemeCardBg => ReadSKColor("CardBackground", 0x1A, 0x1A, 0x2E);
    private static SKColor ThemeBorder => ReadSKColor("ControlElevationBorderBrush", 0x3A, 0x3A, 0x5C);
    private static SKColor ThemeText => ReadSKColor("TextFillColorPrimaryBrush", 0xE0, 0xE0, 0xE0);

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
            singleGauge.InvalidateVisual();
        });
    }

    public void UpdateMultiGauge(double[] values)
    {
        for (int i = 0; i < Math.Min(values.Length, _sectionValues.Length); i++)
        {
            _sectionValues[i] = values[i];
            if (i < SectionItems.Count)
                SectionItems[i].Value = values[i];
        }
        Dispatcher.InvokeAsync(() => multiGauge.InvalidateVisual());
    }

    private void InitSectionItems()
    {
        SectionItems.Clear();
        for (int i = 0; i < _sectionValues.Length; i++)
        {
            var color = Color.FromArgb(
                _sectionColors[i].Alpha, _sectionColors[i].Red, _sectionColors[i].Green, _sectionColors[i].Blue);
            SectionItems.Add(new GaugeSectionItem(_sectionLabels[i], new SolidColorBrush(color), _sectionValues[i]));
        }
    }

    private void OnPaintSingleGauge(object sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        int w = e.Info.Width, h = e.Info.Height;
        canvas.Clear(ThemeCardBg);

        float cx = w / 2f, cy = h - 30;
        float radius = Math.Min(w, h * 1.5f) / 2f - 20;

        // Arc background
        using var bgPaint = new SKPaint { Color = ThemeBorder, Style = SKPaintStyle.Stroke, StrokeWidth = 20, IsAntialias = true };
        canvas.DrawArc(new SKRect(cx - radius, cy - radius, cx + radius, cy + radius), -210, 240, false, bgPaint);

        // Value arc
        float sweep = (float)(_currentValue / 100.0 * 240);
        sweep = Math.Clamp(sweep, 0, 240);
        using var valPaint = new SKPaint { Color = SKColors.LimeGreen, Style = SKPaintStyle.Stroke, StrokeWidth = 20, IsAntialias = true, StrokeCap = SKStrokeCap.Round };
        canvas.DrawArc(new SKRect(cx - radius, cy - radius, cx + radius, cy + radius), -210, sweep, false, valPaint);

        // Needle
        float needleAngle = -210 + sweep;
        float rad = needleAngle * MathF.PI / 180f;
        float nx = cx + (radius - 10) * MathF.Cos(rad);
        float ny = cy + (radius - 10) * MathF.Sin(rad);
        using var needlePaint = new SKPaint { Color = ThemeText, Style = SKPaintStyle.Stroke, StrokeWidth = 3, IsAntialias = true };
        canvas.DrawLine(cx, cy, nx, ny, needlePaint);
        canvas.DrawCircle(cx, cy, 6, new SKPaint { Color = ThemeText, Style = SKPaintStyle.Fill, IsAntialias = true });
    }

    private void OnPaintMultiGauge(object sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        int w = e.Info.Width, h = e.Info.Height;
        canvas.Clear(ThemeCardBg);

        float cx = w / 2f, cy = h - 20;
        float baseRadius = Math.Min(w, h) / 2f - 15;

        for (int i = 0; i < _sectionValues.Length; i++)
        {
            float radius = baseRadius - i * 14;
            float sweep = (float)(_sectionValues[i] / _sectionMax[i] * 240);
            sweep = Math.Clamp(sweep, 0, 240);

            using var bgPaint = new SKPaint { Color = ThemeBorder, Style = SKPaintStyle.Stroke, StrokeWidth = 10, IsAntialias = true };
            canvas.DrawArc(new SKRect(cx - radius, cy - radius, cx + radius, cy + radius), -210, 240, false, bgPaint);

            using var valPaint = new SKPaint { Color = _sectionColors[i], Style = SKPaintStyle.Stroke, StrokeWidth = 10, IsAntialias = true, StrokeCap = SKStrokeCap.Round };
            canvas.DrawArc(new SKRect(cx - radius, cy - radius, cx + radius, cy + radius), -210, sweep, false, valPaint);
        }
    }

    private static SKColor ReadSKColor(string resourceKey, byte fallbackR, byte fallbackG, byte fallbackB)
    {
        if (Application.Current.TryFindResource(resourceKey) is System.Windows.Media.SolidColorBrush brush)
            return new SKColor(brush.Color.R, brush.Color.G, brush.Color.B, brush.Color.A);
        return new SKColor(fallbackR, fallbackG, fallbackB);
    }
}
