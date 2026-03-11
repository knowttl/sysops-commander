using System.Management.Automation;
using SysOpsCommander.Core.Enums;

namespace SysOpsCommander.Core.Models;

/// <summary>
/// Represents a remote execution job targeting one or more hosts.
/// </summary>
public sealed class ExecutionJob
{
    /// <summary>
    /// Gets the unique identifier for this execution run.
    /// </summary>
    public Guid Id { get; init; } = Guid.NewGuid();

    /// <summary>
    /// Gets or sets the display name of the script being executed.
    /// </summary>
    public required string ScriptName { get; set; }

    /// <summary>
    /// Gets or sets the raw .ps1 script content.
    /// </summary>
    public required string ScriptContent { get; set; }

    /// <summary>
    /// Gets or sets the script parameters to inject via <c>AddParameter()</c>.
    /// </summary>
    public IDictionary<string, object>? Parameters { get; set; }

    /// <summary>
    /// Gets the list of target hosts for this execution.
    /// </summary>
    public required IReadOnlyList<HostTarget> TargetHosts { get; init; }

    /// <summary>
    /// Gets or sets the overall execution status.
    /// </summary>
    public ExecutionStatus Status { get; set; } = ExecutionStatus.Pending;

    /// <summary>
    /// Gets or sets the execution mechanism type.
    /// </summary>
    public ExecutionType ExecutionType { get; set; } = ExecutionType.PowerShell;

    /// <summary>
    /// Gets or sets the WinRM connection options for this execution.
    /// </summary>
    public WinRmConnectionOptions WinRmConnectionOptions { get; set; } = WinRmConnectionOptions.CreateDefault();

    /// <summary>
    /// Gets or sets the explicit credentials for this execution. Required for CredSSP authentication.
    /// </summary>
    public PSCredential? Credential { get; set; }

    /// <summary>
    /// Gets or sets the UTC timestamp when execution started.
    /// </summary>
    public DateTime StartTime { get; set; }

    /// <summary>
    /// Gets or sets the UTC timestamp when execution completed.
    /// </summary>
    public DateTime? EndTime { get; set; }

    /// <summary>
    /// Gets the per-host results, populated as each host completes.
    /// </summary>
    public IList<HostResult> Results { get; init; } = [];

    /// <summary>
    /// Gets or sets the maximum number of concurrent host executions.
    /// </summary>
    public int ThrottleLimit { get; set; } = Constants.AppConstants.DefaultThrottle;

    /// <summary>
    /// Gets or sets the per-host timeout in seconds.
    /// </summary>
    public int TimeoutSeconds { get; set; } = Constants.AppConstants.DefaultWinRmTimeoutSeconds;

    /// <summary>
    /// Gets or sets the AD domain context for this execution.
    /// </summary>
    public string? TargetDomain { get; set; }
}
