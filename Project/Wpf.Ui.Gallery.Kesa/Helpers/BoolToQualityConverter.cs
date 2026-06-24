using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace Wpf.Ui.Gallery.Helpers;

/// <summary>
/// Converts a boolean quality state to a theme-aware brush.
/// true = Success (green), false = Critical (red).
/// </summary>
[ValueConversion(typeof(bool), typeof(Brush))]
public class BoolToQualityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var key = value is true ? "SystemFillColorSuccessBrush" : "SystemFillColorCriticalBrush";
        return Application.Current.TryFindResource(key) as Brush
               ?? new SolidColorBrush(value is true
                   ? Color.FromRgb(39, 174, 96)
                   : Color.FromRgb(231, 76, 60));
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
