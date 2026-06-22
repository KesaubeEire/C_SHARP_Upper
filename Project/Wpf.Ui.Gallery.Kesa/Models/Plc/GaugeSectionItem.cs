using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;

namespace Wpf.Ui.Gallery.Models.Plc;

/// <summary>
/// Represents a single gauge section legend entry.
/// </summary>
public class GaugeSectionItem : INotifyPropertyChanged
{
    private double _value;

    public string Label { get; }
    public Brush Color { get; }
    public string DisplayText => $"{Label}: {_value:F1}";

    public double Value
    {
        get => _value;
        set
        {
            if (Math.Abs(_value - value) > 0.001)
            {
                _value = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(DisplayText));
            }
        }
    }

    public GaugeSectionItem(string label, Brush color, double initialValue = 0)
    {
        Label = label;
        Color = color;
        _value = initialValue;
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? n = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}
