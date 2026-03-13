namespace SysOpsCommander.Core.Validation;

/// <summary>
/// Provides static methods for sanitizing LDAP filter input per RFC 4515.
/// Escapes special characters to prevent LDAP injection attacks.
/// </summary>
/// <example>
/// <code>
/// string safe = LdapFilterSanitizer.SanitizeInput("admin)(objectClass=*");
/// // Returns: "admin\29\28objectClass=\2a"
///
/// string filter = LdapFilterSanitizer.BuildSafeFilter("cn", "j*smith");
/// // Returns: "(cn=j\2asmith)"
/// </code>
/// </example>
public static class LdapFilterSanitizer
{
    /// <summary>
    /// Escapes RFC 4515 special characters in the input string to prevent LDAP injection.
    /// </summary>
    /// <param name="raw">The raw user input to sanitize.</param>
    /// <returns>The sanitized string with special characters escaped. Returns empty string for empty input.</returns>
    public static string SanitizeInput(string raw)
    {
        ArgumentNullException.ThrowIfNull(raw);

        if (raw.Length == 0)
        {
            return string.Empty;
        }

        // Backslash MUST be escaped first to avoid double-escaping
        return raw
            .Replace(@"\", @"\5c")
            .Replace("*", @"\2a")
            .Replace("(", @"\28")
            .Replace(")", @"\29")
            .Replace("\0", @"\00");
    }

    /// <summary>
    /// Escapes RFC 4515 special characters except wildcard (*), allowing user-typed wildcards
    /// to pass through as LDAP wildcard operators.
    /// </summary>
    /// <param name="raw">The raw user input to sanitize.</param>
    /// <returns>The sanitized string with injection characters escaped but wildcards preserved.</returns>
    public static string SanitizePreservingWildcards(string raw)
    {
        ArgumentNullException.ThrowIfNull(raw);

        return raw.Length == 0
            ? string.Empty
            : raw
                .Replace(@"\", @"\5c")
                .Replace("(", @"\28")
                .Replace(")", @"\29")
                .Replace("\0", @"\00");
    }

    /// <summary>
    /// Builds a safe LDAP filter expression by sanitizing the value.
    /// </summary>
    /// <param name="attribute">The LDAP attribute name (e.g., "cn", "sAMAccountName").</param>
    /// <param name="value">The raw value to match, which will be sanitized.</param>
    /// <returns>A properly escaped LDAP filter string in the form "(attribute=escapedValue)".</returns>
    public static string BuildSafeFilter(string attribute, string value)
    {
        ArgumentNullException.ThrowIfNull(attribute);
        ArgumentNullException.ThrowIfNull(value);

        return $"({attribute}={SanitizeInput(value)})";
    }
}
