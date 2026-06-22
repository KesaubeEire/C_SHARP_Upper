using System.Globalization;
using System.Windows.Data;

namespace Wpf.Ui.Gallery.Helpers;

/// <summary>
/// Converts a boolean quality state to a color brush string.
/// true = Good (#27AE60 green), false = Bad (#E74C3C red).
/// </summary>
[ValueConversion(typeof(bool), typeof(string))]
public class BoolToQualityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is true ? "#27AE60" : "#E74C3C";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
