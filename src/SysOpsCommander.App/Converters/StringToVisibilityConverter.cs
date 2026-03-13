using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace SysOpsCommander.App.Converters;

/// <summary>
/// Converts a string value to <see cref="Visibility"/>.
/// Non-null/non-empty strings map to Visible; null/empty map to Collapsed.
/// </summary>
public sealed class StringToVisibilityConverter : IValueConverter
{
    /// <inheritdoc />
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        string.IsNullOrEmpty(value as string) ? Visibility.Collapsed : Visibility.Visible;

    /// <inheritdoc />
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
