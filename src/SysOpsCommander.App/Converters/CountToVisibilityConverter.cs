using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace SysOpsCommander.App.Converters;

/// <summary>
/// Converts an integer count to <see cref="Visibility"/>.
/// Values greater than zero map to Visible; zero maps to Collapsed.
/// </summary>
public sealed class CountToVisibilityConverter : IValueConverter
{
    /// <inheritdoc />
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is int count && count > 0 ? Visibility.Visible : Visibility.Collapsed;

    /// <inheritdoc />
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
