using System.Globalization;
using System.Windows.Data;

namespace SysOpsCommander.App.Converters;

/// <summary>
/// Inverts a boolean value. True becomes False and vice versa.
/// </summary>
public sealed class InverseBoolConverter : IValueConverter
{
    /// <inheritdoc />
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is bool b ? !b : value!;

    /// <inheritdoc />
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is bool b ? !b : value!;
}
