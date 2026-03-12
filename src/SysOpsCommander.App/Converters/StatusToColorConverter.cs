using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace SysOpsCommander.App.Converters;

/// <summary>
/// Converts a connection status string to a corresponding <see cref="SolidColorBrush"/>.
/// </summary>
public sealed class StatusToColorConverter : IValueConverter
{
    /// <inheritdoc/>
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value?.ToString() switch
        {
            "Connected" => Brushes.Green,
            "Disconnected" => Brushes.Red,
            "Connecting" => Brushes.Orange,
            _ => Brushes.Gray
        };

    /// <inheritdoc/>
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException("StatusToColorConverter is one-way only.");
}
