namespace SysOpsCommander.Core.Enums;

/// <summary>
/// Represents the status of an individual host during execution.
/// </summary>
public enum HostStatus
{
    /// <summary>
    /// Indicates the host has not yet been processed.
    /// </summary>
    Pending,

    /// <summary>
    /// Indicates the host responded to a reachability check.
    /// </summary>
    Reachable,

    /// <summary>
    /// Indicates the host did not respond to a reachability check.
    /// </summary>
    Unreachable,

    /// <summary>
    /// Indicates a script is currently executing on the host.
    /// </summary>
    Running,

    /// <summary>
    /// Indicates execution completed successfully on the host.
    /// </summary>
    Success,

    /// <summary>
    /// Indicates execution failed on the host.
    /// </summary>
    Failed,

    /// <summary>
    /// Indicates execution timed out on the host.
    /// </summary>
    Timeout,

    /// <summary>
    /// Indicates execution was cancelled for the host.
    /// </summary>
    Cancelled,

    /// <summary>
    /// Indicates the host was skipped (e.g., unreachable before execution).
    /// </summary>
    Skipped
}
