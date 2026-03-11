using Dapper;
using Microsoft.Data.Sqlite;
using SysOpsCommander.Core.Interfaces;
using SysOpsCommander.Core.Models;

namespace SysOpsCommander.Infrastructure.Database;

/// <summary>
/// SQLite-backed implementation of <see cref="ISettingsRepository"/> using Dapper.
/// </summary>
public sealed class SettingsRepository : ISettingsRepository
{
    private readonly string _connectionString;

    /// <summary>
    /// Initializes a new instance of the <see cref="SettingsRepository"/> class.
    /// </summary>
    /// <param name="databaseInitializer">The database initializer providing the connection string.</param>
    public SettingsRepository(DatabaseInitializer databaseInitializer)
    {
        ArgumentNullException.ThrowIfNull(databaseInitializer);
        _connectionString = databaseInitializer.ConnectionString;
    }

    /// <inheritdoc/>
    public async Task<string?> GetValueAsync(string key, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(key);
        using SqliteConnection connection = new(_connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        return await connection.QuerySingleOrDefaultAsync<string?>(
            "SELECT Value FROM UserSettings WHERE Key = @Key",
            new { Key = key }).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task SetValueAsync(string key, string value, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(value);
        using SqliteConnection connection = new(_connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        _ = await connection.ExecuteAsync(
            """
            INSERT OR REPLACE INTO UserSettings (Key, Value, LastModified)
            VALUES (@Key, @Value, @LastModified)
            """,
            new
            {
                Key = key,
                Value = value,
                LastModified = DateTime.UtcNow.ToString("o")
            }).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<UserSettings>> GetAllAsync(CancellationToken cancellationToken)
    {
        using SqliteConnection connection = new(_connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        IEnumerable<UserSettingsRow> rows = await connection.QueryAsync<UserSettingsRow>(
            "SELECT Key, Value, LastModified FROM UserSettings").ConfigureAwait(false);

        return rows.Select(r => new UserSettings
        {
            Key = r.Key,
            Value = r.Value,
            LastModified = DateTime.Parse(r.LastModified, System.Globalization.CultureInfo.InvariantCulture)
        }).ToList().AsReadOnly();
    }

    /// <summary>
    /// Internal row type matching the SQLite UserSettings table schema for Dapper mapping.
    /// </summary>
    private sealed class UserSettingsRow
    {
        public string Key { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
        public string LastModified { get; set; } = string.Empty;
    }
}
