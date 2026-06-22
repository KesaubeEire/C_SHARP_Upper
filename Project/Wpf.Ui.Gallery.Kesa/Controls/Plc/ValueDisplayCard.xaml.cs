using System.Windows;
using System.Windows.Controls;

namespace Wpf.Ui.Gallery.Controls.Plc;

/// <summary>
/// A card control displaying a title, large value, unit, and quality LED.
/// </summary>
public partial class ValueDisplayCard : UserControl
{
    public static readonly DependencyProperty TitleProperty =
        DependencyProperty.Register(nameof(Title), typeof(string), typeof(ValueDisplayCard),
            new PropertyMetadata("Title"));

    public static readonly DependencyProperty ValueProperty =
        DependencyProperty.Register(nameof(Value), typeof(string), typeof(ValueDisplayCard),
            new PropertyMetadata("--"));

    public static readonly DependencyProperty UnitProperty =
        DependencyProperty.Register(nameof(Unit), typeof(string), typeof(ValueDisplayCard),
            new PropertyMetadata(""));

    public static readonly DependencyProperty QualityProperty =
        DependencyProperty.Register(nameof(Quality), typeof(LedQuality), typeof(ValueDisplayCard),
            new PropertyMetadata(LedQuality.Good));

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public string Value
    {
        get => (string)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public string Unit
    {
        get => (string)GetValue(UnitProperty);
        set => SetValue(UnitProperty, value);
    }

    public LedQuality Quality
    {
        get => (LedQuality)GetValue(QualityProperty);
        set => SetValue(QualityProperty, value);
    }

    public ValueDisplayCard()
    {
        InitializeComponent();
    }
}
