using System.Text.RegularExpressions;

namespace SysOpsCommander.Core.Validation;

/// <summary>
/// Provides static methods for validating hostnames in NetBIOS, FQDN, and IPv4 formats.
/// Rejects strings containing injection characters.
/// </summary>
public static partial class HostnameValidator
{
    private static readonly char[] InjectionCharacters = [';', '|', '&', '$', '`', '(', ')'];

    /// <summary>
    /// Validates a hostname string, auto-detecting the format (IPv4, FQDN, or NetBIOS).
    /// </summary>
    /// <param name="hostname">The hostname to validate.</param>
    /// <returns>A <see cref="ValidationResult"/> indicating success or failure with error details.</returns>
    public static ValidationResult Validate(string hostname)
    {
        if (string.IsNullOrWhiteSpace(hostname))
        {
            return ValidationResult.Failure("Hostname cannot be empty.");
        }

        int injectionIndex = hostname.IndexOfAny(InjectionCharacters);
        if (injectionIndex >= 0)
        {
            string found = string.Join(", ",
                hostname.Where(c => InjectionCharacters.Contains(c)).Distinct().Select(c => $"'{c}'"));
            return ValidationResult.Failure($"Hostname contains disallowed characters: {found}");
        }

        return Ipv4Pattern().IsMatch(hostname)
            ? ValidateIpv4(hostname)
            : hostname.Contains('.')
                ? ValidateFqdn(hostname)
                : ValidateNetBios(hostname);
    }

    /// <summary>
    /// Validates a hostname as a NetBIOS name (max 15 chars, alphanumeric + hyphens).
    /// </summary>
    /// <param name="hostname">The NetBIOS hostname to validate.</param>
    /// <returns>A <see cref="ValidationResult"/> indicating success or failure.</returns>
    public static ValidationResult ValidateNetBios(string hostname) =>
        string.IsNullOrWhiteSpace(hostname)
            ? ValidationResult.Failure("Hostname cannot be empty.")
            : hostname.Length > 15
                ? ValidationResult.Failure($"NetBIOS name cannot exceed 15 characters (got {hostname.Length}).")
                : !NetBiosPattern().IsMatch(hostname)
                    ? ValidationResult.Failure("NetBIOS name must contain only alphanumeric characters and hyphens.")
                    : hostname.StartsWith('-') || hostname.EndsWith('-')
                        ? ValidationResult.Failure("NetBIOS name cannot start or end with a hyphen.")
                        : hostname.All(char.IsDigit)
                            ? ValidationResult.Failure("NetBIOS name cannot be all digits.")
                            : ValidationResult.Success();

    /// <summary>
    /// Validates a hostname as a fully qualified domain name.
    /// </summary>
    /// <param name="hostname">The FQDN to validate.</param>
    /// <returns>A <see cref="ValidationResult"/> indicating success or failure.</returns>
    public static ValidationResult ValidateFqdn(string hostname)
    {
        if (string.IsNullOrWhiteSpace(hostname))
        {
            return ValidationResult.Failure("Hostname cannot be empty.");
        }

        if (hostname.Length > 253)
        {
            return ValidationResult.Failure($"FQDN cannot exceed 253 characters (got {hostname.Length}).");
        }

        string[] labels = hostname.Split('.');
        foreach (string label in labels)
        {
            if (label.Length == 0)
            {
                return ValidationResult.Failure("FQDN contains an empty label.");
            }

            if (label.Length > 63)
            {
                return ValidationResult.Failure($"FQDN label '{label}' exceeds 63 characters.");
            }

            if (!FqdnLabelPattern().IsMatch(label))
            {
                return ValidationResult.Failure($"FQDN label '{label}' contains invalid characters.");
            }

            if (label.StartsWith('-') || label.EndsWith('-'))
            {
                return ValidationResult.Failure($"FQDN label '{label}' cannot start or end with a hyphen.");
            }
        }

        return ValidationResult.Success();
    }

    /// <summary>
    /// Validates a hostname as an IPv4 address with four octets (0–255, no leading zeros).
    /// </summary>
    /// <param name="hostname">The IPv4 address to validate.</param>
    /// <returns>A <see cref="ValidationResult"/> indicating success or failure.</returns>
    public static ValidationResult ValidateIpv4(string hostname)
    {
        if (string.IsNullOrWhiteSpace(hostname))
        {
            return ValidationResult.Failure("Hostname cannot be empty.");
        }

        string[] octets = hostname.Split('.');
        if (octets.Length != 4)
        {
            return ValidationResult.Failure("IPv4 address must have exactly four octets.");
        }

        foreach (string octet in octets)
        {
            if (!int.TryParse(octet, out int value) || value < 0 || value > 255)
            {
                return ValidationResult.Failure($"IPv4 octet '{octet}' is out of range (0–255).");
            }

            // Reject leading zeros (e.g., "01", "001") but allow "0"
            if (octet.Length > 1 && octet.StartsWith('0'))
            {
                return ValidationResult.Failure($"IPv4 octet '{octet}' has invalid leading zeros.");
            }
        }

        return ValidationResult.Success();
    }

    /// <summary>
    /// Validates multiple hostnames in a single batch operation.
    /// </summary>
    /// <param name="hostnames">The hostnames to validate.</param>
    /// <returns>A list of hostname-result pairs.</returns>
    public static IReadOnlyList<(string Hostname, ValidationResult Result)> ValidateMany(IEnumerable<string> hostnames)
    {
        ArgumentNullException.ThrowIfNull(hostnames);
        return hostnames.Select(h => (h, Validate(h))).ToList();
    }

    [GeneratedRegex(@"^\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3}$")]
    private static partial Regex Ipv4Pattern();

    [GeneratedRegex(@"^[a-zA-Z0-9\-]+$")]
    private static partial Regex NetBiosPattern();

    [GeneratedRegex(@"^[a-zA-Z0-9\-]+$")]
    private static partial Regex FqdnLabelPattern();
}
