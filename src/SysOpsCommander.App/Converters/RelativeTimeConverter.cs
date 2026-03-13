using System.Globalization;
using System.Windows.Data;

namespace SysOpsCommander.App.Converters;

/// <summary>
/// Converts a date/time string to a relative human-readable format (e.g., "2 hours ago").
/// Falls back to the original value if parsing fails.
/// </summary>
[ValueConversion(typeof(string), typeof(string))]
public sealed class RelativeTimeConverter : IValueConverter
{
    /// <inheritdoc />
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string dateString || string.IsNullOrWhiteSpace(dateString))
        {
            return value ?? string.Empty;
        }

        if (dateString.Equals("Never", StringComparison.OrdinalIgnoreCase))
        {
            return "Never";
        }

        if (!DateTimeOffset.TryParse(dateString, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTimeOffset parsed))
        {
            return dateString;
        }

        TimeSpan elapsed = DateTimeOffset.UtcNow - parsed;

        return elapsed.TotalSeconds switch
        {
            < 60 => "just now",
            < 3600 => $"{(int)elapsed.TotalMinutes}m ago",
            < 86400 => $"{(int)elapsed.TotalHours}h ago",
            < 2592000 => $"{(int)elapsed.TotalDays}d ago",
            _ => parsed.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
        };
    }

    /// <inheritdoc />
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
