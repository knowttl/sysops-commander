using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace SysOpsCommander.App.Converters;

/// <summary>
/// Converts a <see cref="bool"/> to <see cref="Visibility"/>. <see langword="true"/> maps to
/// <see cref="Visibility.Visible"/>; <see langword="false"/> maps to <see cref="Visibility.Collapsed"/>.
/// </summary>
public sealed class BoolToVisibilityConverter : IValueConverter
{
    /// <inheritdoc/>
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is true ? Visibility.Visible : Visibility.Collapsed;

    /// <inheritdoc/>
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is Visibility.Visible;
}
