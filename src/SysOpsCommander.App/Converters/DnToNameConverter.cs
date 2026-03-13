using System.Globalization;
using System.Windows.Data;

namespace SysOpsCommander.App.Converters;

/// <summary>
/// Extracts the first CN value from a distinguished name string.
/// E.g., "CN=Domain Admins,CN=Users,DC=contoso,DC=com" → "Domain Admins".
/// </summary>
[ValueConversion(typeof(string), typeof(string))]
public sealed class DnToNameConverter : IValueConverter
{
    /// <inheritdoc />
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string dn || string.IsNullOrEmpty(dn))
        {
            return string.Empty;
        }

        // Extract the first RDN value (before the first unescaped comma)
        int equalsIndex = dn.IndexOf('=');
        if (equalsIndex < 0)
        {
            return dn;
        }

        string afterEquals = dn[(equalsIndex + 1)..];

        // Find first unescaped comma
        for (int i = 0; i < afterEquals.Length; i++)
        {
            if (afterEquals[i] == ',' && (i == 0 || afterEquals[i - 1] != '\\'))
            {
                return afterEquals[..i];
            }
        }

        return afterEquals;
    }

    /// <inheritdoc />
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
