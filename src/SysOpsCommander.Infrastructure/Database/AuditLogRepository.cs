using System.Text;
using Dapper;
using Microsoft.Data.Sqlite;
using SysOpsCommander.Core.Enums;
using SysOpsCommander.Core.Interfaces;
using SysOpsCommander.Core.Models;

namespace SysOpsCommander.Infrastructure.Database;

/// <summary>
/// SQLite-backed implementation of <see cref="IAuditLogRepository"/> using Dapper.
/// </summary>
public sealed class AuditLogRepository : IAuditLogRepository
{
    private readonly string _connectionString;

    /// <summary>
    /// Initializes a new instance of the <see cref="AuditLogRepository"/> class.
    /// </summary>
    /// <param name="databaseInitializer">The database initializer providing the connection string.</param>
    public AuditLogRepository(DatabaseInitializer databaseInitializer)
    {
        ArgumentNullException.ThrowIfNull(databaseInitializer);
        _connectionString = databaseInitializer.ConnectionString;
    }

    /// <inheritdoc/>
    public async Task InsertAsync(AuditLogEntry entry, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(entry);
        using SqliteConnection connection = new(_connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        _ = await connection.ExecuteAsync("""
            INSERT INTO AuditLog (
                Timestamp, UserName, MachineName, ScriptName, TargetHosts,
                TargetHostCount, SuccessCount, FailureCount, Status, DurationMs,
                ErrorSummary, AuthMethod, Transport, TargetDomain, CorrelationId
            ) VALUES (
                @Timestamp, @UserName, @MachineName, @ScriptName, @TargetHosts,
                @TargetHostCount, @SuccessCount, @FailureCount, @Status, @DurationMs,
                @ErrorSummary, @AuthMethod, @Transport, @TargetDomain, @CorrelationId
            )
            """,
            new
            {
                Timestamp = entry.Timestamp.ToString("o"),
                entry.UserName,
                entry.MachineName,
                entry.ScriptName,
                entry.TargetHosts,
                entry.TargetHostCount,
                entry.SuccessCount,
                entry.FailureCount,
                Status = entry.Status.ToString(),
                DurationMs = (long)entry.Duration.TotalMilliseconds,
                entry.ErrorSummary,
                AuthMethod = entry.AuthMethod?.ToString(),
                Transport = entry.Transport?.ToString(),
                entry.TargetDomain,
                CorrelationId = entry.CorrelationId.ToString()
            }).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<AuditLogEntry>> QueryAsync(AuditLogFilter filter, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(filter);
        using SqliteConnection connection = new(_connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        StringBuilder sql = new("SELECT * FROM AuditLog WHERE 1=1");
        DynamicParameters parameters = new();

        if (filter.StartDate.HasValue)
        {
            _ = sql.Append(" AND Timestamp >= @StartDate");
            parameters.Add("StartDate", filter.StartDate.Value.ToString("o"));
        }

        if (filter.EndDate.HasValue)
        {
            _ = sql.Append(" AND Timestamp <= @EndDate");
            parameters.Add("EndDate", filter.EndDate.Value.ToString("o"));
        }

        if (!string.IsNullOrWhiteSpace(filter.ScriptName))
        {
            _ = sql.Append(" AND ScriptName = @ScriptName");
            parameters.Add("ScriptName", filter.ScriptName);
        }

        if (!string.IsNullOrWhiteSpace(filter.UserName))
        {
            _ = sql.Append(" AND UserName = @UserName");
            parameters.Add("UserName", filter.UserName);
        }

        if (filter.Status.HasValue)
        {
            _ = sql.Append(" AND Status = @Status");
            parameters.Add("Status", filter.Status.Value.ToString());
        }

        if (filter.AuthMethod.HasValue)
        {
            _ = sql.Append(" AND AuthMethod = @AuthMethod");
            parameters.Add("AuthMethod", filter.AuthMethod.Value.ToString());
        }

        if (!string.IsNullOrWhiteSpace(filter.Hostname))
        {
            _ = sql.Append(" AND TargetHosts LIKE @Hostname");
            parameters.Add("Hostname", $"%{filter.Hostname}%");
        }

        if (!string.IsNullOrWhiteSpace(filter.TargetDomain))
        {
            _ = sql.Append(" AND TargetDomain = @TargetDomain");
            parameters.Add("TargetDomain", filter.TargetDomain);
        }

        _ = sql.Append(" ORDER BY Timestamp DESC");
        _ = sql.Append(" LIMIT @PageSize OFFSET @Offset");
        parameters.Add("PageSize", filter.PageSize);
        parameters.Add("Offset", (filter.PageNumber - 1) * filter.PageSize);

        IEnumerable<AuditLogRow> rows = await connection.QueryAsync<AuditLogRow>(
            sql.ToString(), parameters).ConfigureAwait(false);

        return rows.Select(MapFromRow).ToList().AsReadOnly();
    }

    /// <inheritdoc/>
    public async Task<int> DeleteOlderThanAsync(DateTime cutoffDate, CancellationToken cancellationToken)
    {
        using SqliteConnection connection = new(_connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        return await connection.ExecuteAsync(
            "DELETE FROM AuditLog WHERE Timestamp < @Cutoff",
            new { Cutoff = cutoffDate.ToString("o") }).ConfigureAwait(false);
    }

    private static AuditLogEntry MapFromRow(AuditLogRow row) =>
        new()
        {
            Id = row.Id,
            Timestamp = DateTime.Parse(row.Timestamp, System.Globalization.CultureInfo.InvariantCulture),
            UserName = row.UserName,
            MachineName = row.MachineName,
            ScriptName = row.ScriptName,
            TargetHosts = row.TargetHosts,
            TargetHostCount = row.TargetHostCount,
            SuccessCount = row.SuccessCount,
            FailureCount = row.FailureCount,
            Status = Enum.Parse<ExecutionStatus>(row.Status),
            Duration = TimeSpan.FromMilliseconds(row.DurationMs),
            ErrorSummary = row.ErrorSummary,
            AuthMethod = string.IsNullOrEmpty(row.AuthMethod) ? null : Enum.Parse<WinRmAuthMethod>(row.AuthMethod),
            Transport = string.IsNullOrEmpty(row.Transport) ? null : Enum.Parse<WinRmTransport>(row.Transport),
            TargetDomain = row.TargetDomain,
            CorrelationId = Guid.Parse(row.CorrelationId)
        };

    /// <summary>
    /// Internal row type matching the SQLite AuditLog table schema for Dapper mapping.
    /// </summary>
    private sealed class AuditLogRow
    {
        public long Id { get; set; }
        public string Timestamp { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public string MachineName { get; set; } = string.Empty;
        public string ScriptName { get; set; } = string.Empty;
        public string TargetHosts { get; set; } = string.Empty;
        public int TargetHostCount { get; set; }
        public int SuccessCount { get; set; }
        public int FailureCount { get; set; }
        public string Status { get; set; } = string.Empty;
        public long DurationMs { get; set; }
        public string? ErrorSummary { get; set; }
        public string? AuthMethod { get; set; }
        public string? Transport { get; set; }
        public string? TargetDomain { get; set; }
        public string CorrelationId { get; set; } = string.Empty;
    }
}
