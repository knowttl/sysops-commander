using System.Globalization;
using System.Windows.Data;

namespace SysOpsCommander.App.Converters;

/// <summary>
/// Truncates long strings and appends an ellipsis. Default limit is 120 characters.
/// Use ConverterParameter to set a custom limit.
/// </summary>
[ValueConversion(typeof(string), typeof(string))]
public sealed class TruncatedTextConverter : IValueConverter
{
    private const int DefaultMaxLength = 120;

    /// <inheritdoc />
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string text || string.IsNullOrEmpty(text))
        {
            return value ?? string.Empty;
        }

        int maxLength = parameter is string paramStr && int.TryParse(paramStr, out int parsed)
            ? parsed
            : DefaultMaxLength;

        return text.Length <= maxLength
            ? text
            : string.Concat(text.AsSpan(0, maxLength), "…");
    }

    /// <inheritdoc />
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
