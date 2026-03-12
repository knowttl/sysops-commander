using Serilog;
using SysOpsCommander.Core.Interfaces;
using SysOpsCommander.Core.Models;

namespace SysOpsCommander.Services;

/// <summary>
/// Provides audit logging for execution history, delegating persistence to <see cref="IAuditLogRepository"/>.
/// </summary>
public sealed class AuditLogService : IAuditLogService
{
    private readonly IAuditLogRepository _repository;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="AuditLogService"/> class.
    /// </summary>
    /// <param name="repository">The audit log repository for data persistence.</param>
    /// <param name="logger">The Serilog logger instance.</param>
    public AuditLogService(IAuditLogRepository repository, ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(logger);
        _repository = repository;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task LogExecutionAsync(AuditLogEntry entry, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(entry);

        try
        {
            await _repository.InsertAsync(entry, cancellationToken).ConfigureAwait(false);
            _logger.Information(
                "Audit entry logged: {ScriptName} on {HostCount} hosts — {Status}",
                entry.ScriptName,
                entry.TargetHostCount,
                entry.Status);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to persist audit log entry for {ScriptName}", entry.ScriptName);
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<AuditLogEntry>> QueryAsync(AuditLogFilter filter, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(filter);
        return await _repository.QueryAsync(filter, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<int> PurgeOldEntriesAsync(int retentionDays, CancellationToken cancellationToken)
    {
        if (retentionDays <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(retentionDays), "Retention days must be positive.");
        }

        DateTime cutoffDate = DateTime.UtcNow.AddDays(-retentionDays);
        int deletedCount = await _repository.DeleteOlderThanAsync(cutoffDate, cancellationToken).ConfigureAwait(false);

        _logger.Information("Purged {Count} audit entries older than {CutoffDate:yyyy-MM-dd}", deletedCount, cutoffDate);
        return deletedCount;
    }
}
