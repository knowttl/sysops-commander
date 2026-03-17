namespace SysOpsCommander.Core.Enums;

/// <summary>
/// Represents the status of a DNS-based IP address resolution for an AD computer object.
/// </summary>
public enum IpResolutionStatus
{
    /// <summary>
    /// Resolution has not yet been triggered.
    /// </summary>
    NotStarted,

    /// <summary>
    /// DNS lookup is currently in progress.
    /// </summary>
    Resolving,

    /// <summary>
    /// Successfully resolved to at least one IP address.
    /// </summary>
    Resolved,

    /// <summary>
    /// DNS lookup failed (timeout, NXDOMAIN, network error, etc.).
    /// </summary>
    Failed,

    /// <summary>
    /// Object is not a computer — no resolution needed.
    /// </summary>
    NotApplicable
}
