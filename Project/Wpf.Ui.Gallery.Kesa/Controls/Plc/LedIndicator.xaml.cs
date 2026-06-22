using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace Wpf.Ui.Gallery.Controls.Plc;

/// <summary>
/// Multi-color LED indicator with optional blinking animation.
/// Colors: Green (Good), Red (Bad), Yellow (Warning), Blue (Info), Gray (Disabled).
/// </summary>
public partial class LedIndicator : UserControl
{
    public static readonly DependencyProperty LedColorProperty =
        DependencyProperty.Register(nameof(LedColor), typeof(Brush), typeof(LedIndicator),
            new PropertyMetadata(new SolidColorBrush(Color.FromRgb(39, 174, 96)), OnLedColorChanged));

    public static readonly DependencyProperty IsBlinkingProperty =
        DependencyProperty.Register(nameof(IsBlinking), typeof(bool), typeof(LedIndicator),
            new PropertyMetadata(false, OnIsBlinkingChanged));

    public static readonly DependencyProperty QualityProperty =
        DependencyProperty.Register(nameof(Quality), typeof(LedQuality), typeof(LedIndicator),
            new PropertyMetadata(LedQuality.Good, OnQualityChanged));

    public static readonly DependencyProperty ToolTipTextProperty =
        DependencyProperty.Register(nameof(ToolTipText), typeof(string), typeof(LedIndicator),
            new PropertyMetadata(null));

    public Brush LedColor
    {
        get => (Brush)GetValue(LedColorProperty);
        set => SetValue(LedColorProperty, value);
    }

    public bool IsBlinking
    {
        get => (bool)GetValue(IsBlinkingProperty);
        set => SetValue(IsBlinkingProperty, value);
    }

    public LedQuality Quality
    {
        get => (LedQuality)GetValue(QualityProperty);
        set => SetValue(QualityProperty, value);
    }

    public string? ToolTipText
    {
        get => (string?)GetValue(ToolTipTextProperty);
        set => SetValue(ToolTipTextProperty, value);
    }

    private Storyboard? _blinkStory;

    public LedIndicator()
    {
        InitializeComponent();
        _blinkStory = (Storyboard)Resources["BlinkStory"];
        UpdateColorFromQuality();
    }

    private static void OnLedColorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        // Color set explicitly, no quality sync needed
    }

    private static void OnIsBlinkingChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is LedIndicator led)
            led.ToggleBlink((bool)e.NewValue);
    }

    private static void OnQualityChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is LedIndicator led)
            led.UpdateColorFromQuality();
    }

    private void UpdateColorFromQuality()
    {
        LedColor = Quality switch
        {
            LedQuality.Good => new SolidColorBrush(Color.FromRgb(39, 174, 96)),      // #27AE60
            LedQuality.Bad => new SolidColorBrush(Color.FromRgb(231, 76, 60)),       // #E74C3C
            LedQuality.Warning => new SolidColorBrush(Color.FromRgb(243, 156, 18)),   // #F39C12
            LedQuality.Info => new SolidColorBrush(Color.FromRgb(52, 152, 219)),      // #3498DB
            LedQuality.Disabled => new SolidColorBrush(Color.FromRgb(149, 165, 166)), // #95A5A6
            _ => new SolidColorBrush(Color.FromRgb(149, 165, 166)),
        };
    }

    private void ToggleBlink(bool blink)
    {
        if (_blinkStory == null) return;
        if (blink)
            _blinkStory.Begin(this, isControllable: true);
        else
            _blinkStory.Stop(this);
    }
}

/// <summary>
/// Quality states for <see cref="LedIndicator"/>.
/// </summary>
public enum LedQuality
{
    Good,
    Bad,
    Warning,
    Info,
    Disabled,
}
