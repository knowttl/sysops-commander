using SysOpsCommander.Core.Enums;

namespace SysOpsCommander.Core.Models;

/// <summary>
/// Represents the result of a DNS-based IP address resolution for an AD computer object.
/// </summary>
public sealed class IpResolutionResult
{
    /// <summary>
    /// Gets the resolution status.
    /// </summary>
    public required IpResolutionStatus Status { get; init; }

    /// <summary>
    /// Gets the first resolved IPv4 address for grid display, or <c>null</c> if unavailable.
    /// </summary>
    public string? PrimaryIPv4 { get; init; }

    /// <summary>
    /// Gets all resolved addresses (IPv4 and IPv6) for the inspector detail view.
    /// </summary>
    public IReadOnlyList<string> AllAddresses { get; init; } = [];

    /// <summary>
    /// Gets the error message when resolution fails, for tooltip display.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Gets the DNS hostname that was resolved.
    /// </summary>
    public string? Hostname { get; init; }
}
