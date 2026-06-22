using System.Windows;
using System.Windows.Controls;

namespace Wpf.Ui.Gallery.Controls.Plc;

/// <summary>
/// Status bar showing connection state, polling info, and error count.
/// </summary>
public partial class StatusStrip : UserControl
{
    public static readonly DependencyProperty ConnectionQualityProperty =
        DependencyProperty.Register(nameof(ConnectionQuality), typeof(LedQuality), typeof(StatusStrip),
            new PropertyMetadata(LedQuality.Disabled));

    public static readonly DependencyProperty ConnectionTextProperty =
        DependencyProperty.Register(nameof(ConnectionText), typeof(string), typeof(StatusStrip),
            new PropertyMetadata("未连接"));

    public static readonly DependencyProperty PollingTextProperty =
        DependencyProperty.Register(nameof(PollingText), typeof(string), typeof(StatusStrip),
            new PropertyMetadata("--"));

    public static readonly DependencyProperty ErrorCountProperty =
        DependencyProperty.Register(nameof(ErrorCount), typeof(int), typeof(StatusStrip),
            new PropertyMetadata(0));

    public static readonly DependencyProperty ShowErrorsProperty =
        DependencyProperty.Register(nameof(ShowErrors), typeof(bool), typeof(StatusStrip),
            new PropertyMetadata(false));

    public LedQuality ConnectionQuality
    {
        get => (LedQuality)GetValue(ConnectionQualityProperty);
        set => SetValue(ConnectionQualityProperty, value);
    }

    public string ConnectionText
    {
        get => (string)GetValue(ConnectionTextProperty);
        set => SetValue(ConnectionTextProperty, value);
    }

    public string PollingText
    {
        get => (string)GetValue(PollingTextProperty);
        set => SetValue(PollingTextProperty, value);
    }

    public int ErrorCount
    {
        get => (int)GetValue(ErrorCountProperty);
        set => SetValue(ErrorCountProperty, value);
    }

    public bool ShowErrors
    {
        get => (bool)GetValue(ShowErrorsProperty);
        set => SetValue(ShowErrorsProperty, value);
    }

    public StatusStrip()
    {
        InitializeComponent();
    }
}
