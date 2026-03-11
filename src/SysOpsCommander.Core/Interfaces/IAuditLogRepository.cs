using SysOpsCommander.Core.Models;

namespace SysOpsCommander.Core.Interfaces;

/// <summary>
/// Data access layer for audit log entries in the SQLite database.
/// </summary>
public interface IAuditLogRepository
{
    /// <summary>
    /// Inserts a new audit log entry.
    /// </summary>
    /// <param name="entry">The audit log entry to persist.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task InsertAsync(AuditLogEntry entry, CancellationToken cancellationToken);

    /// <summary>
    /// Queries audit log entries matching the specified filter.
    /// </summary>
    /// <param name="filter">The filter criteria.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A list of matching audit log entries.</returns>
    Task<IReadOnlyList<AuditLogEntry>> QueryAsync(AuditLogFilter filter, CancellationToken cancellationToken);

    /// <summary>
    /// Deletes audit log entries older than the specified cutoff date.
    /// </summary>
    /// <param name="cutoffDate">Entries before this date will be deleted.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The number of entries deleted.</returns>
    Task<int> DeleteOlderThanAsync(DateTime cutoffDate, CancellationToken cancellationToken);
}
