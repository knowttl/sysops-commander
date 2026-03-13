using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace SysOpsCommander.App.Converters;

/// <summary>
/// Converts an AD object class string to a Segoe MDL2 Assets glyph character
/// for display as a tree view icon, mimicking the Windows ADUC MMC snap-in style.
/// </summary>
public sealed class ObjectClassToIconConverter : IValueConverter
{
    /// <inheritdoc />
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        string objectClass = (value as string ?? string.Empty).ToLowerInvariant();

        return objectClass switch
        {
            "domaindns" or "domain" => "\uE774",           // Globe
            "organizationalunit" => "\uE8B7",              // Folder (open)
            "container" or "builtindomain" => "\uE7B8",    // Folder
            "user" or "person" => "\uE77B",                // Contact/Person
            "computer" => "\uE7F8",                        // Desktop/Monitor
            "group" => "\uE716",                           // People
            "contact" => "\uE779",                         // ContactInfo
            "grouppolicycontainer" => "\uE7C3",            // Page/Policy
            _ => "\uE81E"                                  // Library/Generic
        };
    }

    /// <inheritdoc />
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>
/// Converts an AD object class string to a <see cref="SolidColorBrush"/> for icon coloring,
/// providing visual differentiation similar to Windows ADUC.
/// </summary>
public sealed class ObjectClassToIconColorConverter : IValueConverter
{
    private static readonly SolidColorBrush DomainBrush = Freeze(new SolidColorBrush(Color.FromRgb(0x5B, 0x9B, 0xD5)));
    private static readonly SolidColorBrush OuBrush = Freeze(new SolidColorBrush(Color.FromRgb(0xFF, 0xC1, 0x07)));
    private static readonly SolidColorBrush ContainerBrush = Freeze(new SolidColorBrush(Color.FromRgb(0xA0, 0xA0, 0xA0)));
    private static readonly SolidColorBrush UserBrush = Freeze(new SolidColorBrush(Color.FromRgb(0x6C, 0xB4, 0xEE)));
    private static readonly SolidColorBrush ComputerBrush = Freeze(new SolidColorBrush(Color.FromRgb(0x6E, 0xCB, 0x63)));
    private static readonly SolidColorBrush GroupBrush = Freeze(new SolidColorBrush(Color.FromRgb(0xF0, 0xA2, 0x02)));
    private static readonly SolidColorBrush ContactBrush = Freeze(new SolidColorBrush(Color.FromRgb(0xB0, 0x8C, 0xD5)));
    private static readonly SolidColorBrush DefaultBrush = Freeze(new SolidColorBrush(Color.FromRgb(0x99, 0x99, 0x99)));

    /// <inheritdoc />
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        string objectClass = (value as string ?? string.Empty).ToLowerInvariant();

        return objectClass switch
        {
            "domaindns" or "domain" => DomainBrush,
            "organizationalunit" => OuBrush,
            "container" or "builtindomain" => ContainerBrush,
            "user" or "person" => UserBrush,
            "computer" => ComputerBrush,
            "group" => GroupBrush,
            "contact" => ContactBrush,
            "grouppolicycontainer" => DefaultBrush,
            _ => DefaultBrush
        };
    }

    /// <inheritdoc />
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();

    private static SolidColorBrush Freeze(SolidColorBrush brush)
    {
        brush.Freeze();
        return brush;
    }
}
