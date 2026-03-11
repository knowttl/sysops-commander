using SysOpsCommander.Core.Models;

namespace SysOpsCommander.Core.Interfaces;

/// <summary>
/// Provides audit logging for execution history with query and purge support.
/// </summary>
public interface IAuditLogService
{
    /// <summary>
    /// Logs an execution event to the audit log.
    /// </summary>
    /// <param name="entry">The audit log entry to persist.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task LogExecutionAsync(AuditLogEntry entry, CancellationToken cancellationToken);

    /// <summary>
    /// Queries the audit log with the specified filter criteria.
    /// </summary>
    /// <param name="filter">The filter criteria for the query.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A list of matching audit log entries.</returns>
    Task<IReadOnlyList<AuditLogEntry>> QueryAsync(AuditLogFilter filter, CancellationToken cancellationToken);

    /// <summary>
    /// Purges audit log entries older than the specified retention period.
    /// </summary>
    /// <param name="retentionDays">The number of days of entries to retain.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The number of entries deleted.</returns>
    Task<int> PurgeOldEntriesAsync(int retentionDays, CancellationToken cancellationToken);
}
