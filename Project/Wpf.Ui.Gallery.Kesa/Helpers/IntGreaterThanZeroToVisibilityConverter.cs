using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Wpf.Ui.Gallery.Helpers;

/// <summary>
/// Returns <see cref="Visibility.Visible"/> when <c>int value > 0</c>,
/// <see cref="Visibility.Collapsed"/> otherwise.
/// </summary>
internal sealed class IntGreaterThanZeroToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is int intValue)
            return intValue > 0 ? Visibility.Visible : Visibility.Collapsed;

        return Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
