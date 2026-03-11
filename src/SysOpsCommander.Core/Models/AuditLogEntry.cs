using SysOpsCommander.Core.Enums;

namespace SysOpsCommander.Core.Models;

/// <summary>
/// Represents an entry in the audit log capturing execution history.
/// </summary>
public sealed class AuditLogEntry
{
    /// <summary>
    /// Gets or sets the auto-incremented unique identifier.
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// Gets or sets the UTC timestamp of the execution.
    /// </summary>
    public DateTime Timestamp { get; set; }

    /// <summary>
    /// Gets or sets the username that initiated the execution.
    /// </summary>
    public string UserName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the machine name where the application was running.
    /// </summary>
    public string MachineName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the name of the script that was executed.
    /// </summary>
    public string ScriptName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the comma-separated list of target hostnames.
    /// </summary>
    public string TargetHosts { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the number of target hosts.
    /// </summary>
    public int TargetHostCount { get; set; }

    /// <summary>
    /// Gets or sets the number of hosts that completed successfully.
    /// </summary>
    public int SuccessCount { get; set; }

    /// <summary>
    /// Gets or sets the number of hosts that failed.
    /// </summary>
    public int FailureCount { get; set; }

    /// <summary>
    /// Gets or sets the overall execution status.
    /// </summary>
    public ExecutionStatus Status { get; set; }

    /// <summary>
    /// Gets or sets the total execution duration.
    /// </summary>
    public TimeSpan Duration { get; set; }

    /// <summary>
    /// Gets or sets the first error message summary, if any.
    /// </summary>
    public string? ErrorSummary { get; set; }

    /// <summary>
    /// Gets or sets the WinRM authentication method used.
    /// </summary>
    public WinRmAuthMethod? AuthMethod { get; set; }

    /// <summary>
    /// Gets or sets the WinRM transport protocol used.
    /// </summary>
    public WinRmTransport? Transport { get; set; }

    /// <summary>
    /// Gets or sets the target AD domain for this execution.
    /// </summary>
    public string? TargetDomain { get; set; }

    /// <summary>
    /// Gets or sets the correlation identifier linking to Serilog log entries.
    /// </summary>
    public Guid CorrelationId { get; set; }
}
