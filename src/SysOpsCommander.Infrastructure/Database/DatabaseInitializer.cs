using Dapper;
using Microsoft.Data.Sqlite;
using SysOpsCommander.Core.Constants;

namespace SysOpsCommander.Infrastructure.Database;

/// <summary>
/// Creates and initializes the SQLite database and schema at the application data path.
/// </summary>
public sealed class DatabaseInitializer
{
    /// <summary>
    /// Gets the connection string for the initialized database.
    /// </summary>
    public string ConnectionString { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="DatabaseInitializer"/> class using the default database path.
    /// </summary>
    public DatabaseInitializer()
    {
        string dbDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            AppConstants.AppDataFolder);

        _ = Directory.CreateDirectory(dbDirectory);

        string dbPath = Path.Combine(dbDirectory, "audit.db");
        ConnectionString = $"Data Source={dbPath}";
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="DatabaseInitializer"/> class with an explicit connection string.
    /// Used for testing with in-memory databases.
    /// </summary>
    /// <param name="connectionString">The SQLite connection string.</param>
    public DatabaseInitializer(string connectionString)
    {
        ConnectionString = connectionString;
    }

    /// <summary>
    /// Creates the database schema if it does not already exist.
    /// This method is idempotent and safe to call multiple times.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous initialization.</returns>
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        using SqliteConnection connection = new(ConnectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        _ = await connection.ExecuteAsync("""
            CREATE TABLE IF NOT EXISTS AuditLog (
                Id              INTEGER PRIMARY KEY AUTOINCREMENT,
                Timestamp       TEXT    NOT NULL,
                UserName        TEXT    NOT NULL,
                MachineName     TEXT    NOT NULL,
                ScriptName      TEXT    NOT NULL,
                TargetHosts     TEXT    NOT NULL,
                TargetHostCount INTEGER NOT NULL,
                SuccessCount    INTEGER NOT NULL,
                FailureCount    INTEGER NOT NULL,
                Status          TEXT    NOT NULL,
                DurationMs      INTEGER NOT NULL,
                ErrorSummary    TEXT,
                AuthMethod      TEXT,
                Transport       TEXT,
                TargetDomain    TEXT,
                CorrelationId   TEXT    NOT NULL
            )
            """).ConfigureAwait(false);

        _ = await connection.ExecuteAsync("""
            CREATE TABLE IF NOT EXISTS UserSettings (
                Key          TEXT PRIMARY KEY,
                Value        TEXT NOT NULL,
                LastModified TEXT NOT NULL
            )
            """).ConfigureAwait(false);

        _ = await connection.ExecuteAsync("""
            CREATE TABLE IF NOT EXISTS SchemaVersion (
                Version   INTEGER PRIMARY KEY,
                AppliedAt TEXT    NOT NULL
            )
            """).ConfigureAwait(false);

        int versionCount = await connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM SchemaVersion").ConfigureAwait(false);

        if (versionCount == 0)
        {
            _ = await connection.ExecuteAsync(
                "INSERT INTO SchemaVersion (Version, AppliedAt) VALUES (@Version, @AppliedAt)",
                new { Version = 1, AppliedAt = DateTime.UtcNow.ToString("o") }).ConfigureAwait(false);
        }
    }
}
