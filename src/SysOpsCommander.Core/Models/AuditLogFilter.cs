using SysOpsCommander.Core.Constants;
using SysOpsCommander.Core.Enums;

namespace SysOpsCommander.Core.Models;

/// <summary>
/// Represents filter criteria for querying the audit log.
/// </summary>
public sealed class AuditLogFilter
{
    /// <summary>
    /// Gets or sets the inclusive start date filter.
    /// </summary>
    public DateTime? StartDate { get; set; }

    /// <summary>
    /// Gets or sets the inclusive end date filter.
    /// </summary>
    public DateTime? EndDate { get; set; }

    /// <summary>
    /// Gets or sets the script name filter (exact match).
    /// </summary>
    public string? ScriptName { get; set; }

    /// <summary>
    /// Gets or sets the username filter (exact match).
    /// </summary>
    public string? UserName { get; set; }

    /// <summary>
    /// Gets or sets the execution status filter.
    /// </summary>
    public ExecutionStatus? Status { get; set; }

    /// <summary>
    /// Gets or sets the WinRM authentication method filter.
    /// </summary>
    public WinRmAuthMethod? AuthMethod { get; set; }

    /// <summary>
    /// Gets or sets the hostname filter (partial match against target hosts).
    /// </summary>
    public string? Hostname { get; set; }

    /// <summary>
    /// Gets or sets the target domain filter.
    /// </summary>
    public string? TargetDomain { get; set; }

    /// <summary>
    /// Gets or sets the page number (1-based). Defaults to 1.
    /// </summary>
    public int PageNumber { get; set; } = 1;

    /// <summary>
    /// Gets or sets the page size. Defaults to <see cref="AppConstants.MaxResultsPerPage"/>.
    /// </summary>
    public int PageSize { get; set; } = AppConstants.MaxResultsPerPage;
}
