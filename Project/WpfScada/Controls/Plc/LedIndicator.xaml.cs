using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace WpfScada.Controls.Plc;

/// <summary>
/// Multi-color LED indicator with optional blinking animation.
/// Color is driven by <see cref="Quality"/> via XAML DataTrigger style,
/// falling back to <see cref="LedColor"/> when set explicitly.
/// </summary>
public partial class LedIndicator : UserControl
{
    public static readonly DependencyProperty LedColorProperty =
        DependencyProperty.Register(nameof(LedColor), typeof(Brush), typeof(LedIndicator),
            new PropertyMetadata(null, OnLedColorChanged));

    public static readonly DependencyProperty IsBlinkingProperty =
        DependencyProperty.Register(nameof(IsBlinking), typeof(bool), typeof(LedIndicator),
            new PropertyMetadata(false, OnIsBlinkingChanged));

    public static readonly DependencyProperty QualityProperty =
        DependencyProperty.Register(nameof(Quality), typeof(LedQuality), typeof(LedIndicator),
            new PropertyMetadata(LedQuality.Good));

    public static readonly DependencyProperty ToolTipTextProperty =
        DependencyProperty.Register(nameof(ToolTipText), typeof(string), typeof(LedIndicator),
            new PropertyMetadata(null));

    public Brush? LedColor
    {
        get => (Brush?)GetValue(LedColorProperty);
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
    }

    private static void OnLedColorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is LedIndicator led && e.NewValue is Brush brush)
            led.ellipse.Fill = brush;
    }

    private static void OnIsBlinkingChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is LedIndicator led)
            led.ToggleBlink((bool)e.NewValue);
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
