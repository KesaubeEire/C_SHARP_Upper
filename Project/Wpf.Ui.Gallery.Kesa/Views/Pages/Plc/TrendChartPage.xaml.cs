using System.Windows;
using System.Windows.Controls;
using SkiaSharp;
using SkiaSharp.Views.Desktop;
using SkiaSharp.Views.WPF;
using Wpf.Ui.Gallery.Models.Plc;
using Wpf.Ui.Gallery.Services.Plc;

namespace Wpf.Ui.Gallery.Views.Pages.Plc;

public partial class TrendChartPage : Page
{
    private readonly S7Service _s7;
    private readonly List<TrendChannelConfig> _channels = [];
    private readonly Dictionary<string, List<TrendDataPoint>> _buffers = [];
    private readonly object _lock = new();
    private MockTrendService? _mock;
    private int _maxPoints = 600;
    private long _sampleCount;

    public TrendChartPage(S7Service s7)
    {
        _s7 = s7;
        InitializeComponent();
        BuildChannelLegend();
    }

    public void AddChannel(string key, string label, double min, double max, string unit, SKColor color)
    {
        var ch = new TrendChannelConfig
        {
            Key = key, Label = label, Min = min, Max = max,
            Unit = unit, Color = color, ColorHex = color.ToString()
        };
        _channels.Add(ch);
        _buffers[key] = [];
        BuildChannelLegend();
    }

    public void FeedData(string key, double value, DateTime timestamp)
    {
        if (!_buffers.ContainsKey(key)) return;
        lock (_lock)
        {
            var buf = _buffers[key];
            var ch = _channels.FirstOrDefault(c => c.Key == key);
            double range = ch != null ? ch.Max - ch.Min : 100;
            if (range == 0) range = 1;
            buf.Add(new TrendDataPoint
            {
                RawValue = value,
                NormalizedValue = (value - (ch?.Min ?? 0)) / range * 100,
                Timestamp = timestamp
            });
            while (buf.Count > _maxPoints) buf.RemoveAt(0);
            _sampleCount++;
        }
        Dispatcher.InvokeAsync(() => skElement.InvalidateVisual());
    }

    private void BuildChannelLegend()
    {
        channelLegend.Children.Clear();
        foreach (var ch in _channels)
        {
            var tb = new System.Windows.Controls.TextBlock
            {
                Text = $"{ch.Label} ({ch.Unit})",
                Foreground = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromArgb(ch.Color.Alpha, ch.Color.Red, ch.Color.Green, ch.Color.Blue)),
                FontSize = 12
            };
            channelLegend.Children.Add(tb);
        }
    }

    private static SKColor ThemeBg => ReadSKColor("ApplicationBackgroundBrush", 0x0F, 0x0F, 0x1A);
    private static SKColor ThemeGrid => ReadSKColor("ControlElevationBorderBrush", 0x2A, 0x2A, 0x4A);

    private void OnPaintSurface(object sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        int w = e.Info.Width, h = e.Info.Height;
        canvas.Clear(ThemeBg);

        if (_channels.Count == 0 || _buffers.Values.All(b => b.Count == 0))
        {
            using var paint = new SKPaint { Color = SKColors.Gray, TextSize = 16, IsAntialias = true };
            canvas.DrawText("等待数据...", w / 2f - 40, h / 2f, paint);
            return;
        }

        float margin = 50;
        float plotW = w - margin * 2;
        float plotH = h - margin * 2;

        // Grid
        using var gridPaint = new SKPaint { Color = ThemeGrid, StrokeWidth = 0.5f, Style = SKPaintStyle.Stroke };
        for (int i = 0; i <= 4; i++)
        {
            float y = margin + plotH * i / 4f;
            canvas.DrawLine(margin, y, margin + plotW, y, gridPaint);
        }

        lock (_lock)
        {
            foreach (var ch in _channels)
            {
                if (!_buffers.TryGetValue(ch.Key, out var buf) || buf.Count < 2) continue;
                using var paint = new SKPaint
                {
                    Color = ch.Color,
                    StrokeWidth = 2,
                    Style = SKPaintStyle.Stroke,
                    IsAntialias = true
                };
                var path = new SKPath();
                float stepX = plotW / Math.Max(buf.Count - 1, 1);
                for (int i = 0; i < buf.Count; i++)
                {
                    float x = margin + i * stepX;
                    float y = margin + plotH - (float)(buf[i].NormalizedValue / 100.0 * plotH);
                    y = Math.Clamp(y, margin, margin + plotH);
                    if (i == 0) path.MoveTo(x, y);
                    else path.LineTo(x, y);
                }
                canvas.DrawPath(path, paint);
            }
        }
    }

    private void OnToggleMock(object sender, RoutedEventArgs e)
    {
        if (_mock != null && _mock.IsRunning)
        {
            _mock.Stop();
            btnMock.Content = "模拟数据";
            return;
        }

        _mock = new MockTrendService();
        _mock.DataGenerated += (key, val, ts) => FeedData(key, val, ts);
        _mock.Start();
        btnMock.Content = "停止模拟";
    }

    public void Stop()
    {
        _mock?.Stop();
    }

    private static SKColor ReadSKColor(string resourceKey, byte fallbackR, byte fallbackG, byte fallbackB)
    {
        if (Application.Current.TryFindResource(resourceKey) is System.Windows.Media.SolidColorBrush brush)
            return new SKColor(brush.Color.R, brush.Color.G, brush.Color.B, brush.Color.A);
        return new SKColor(fallbackR, fallbackG, fallbackB);
    }
}
