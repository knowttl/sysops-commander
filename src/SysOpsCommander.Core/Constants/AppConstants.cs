namespace SysOpsCommander.Core.Constants;

/// <summary>
/// Provides application-wide constant values.
/// </summary>
public static class AppConstants
{
    /// <summary>
    /// The display name of the application.
    /// </summary>
    public const string AppName = "SysOps Commander";

    /// <summary>
    /// The folder name used under %LOCALAPPDATA% for application data storage.
    /// </summary>
    public const string AppDataFolder = "SysOpsCommander";

    /// <summary>
    /// The default number of concurrent remote executions.
    /// </summary>
    public const int DefaultThrottle = 5;

    /// <summary>
    /// The default WinRM operation timeout in seconds.
    /// </summary>
    public const int DefaultWinRmTimeoutSeconds = 60;

    /// <summary>
    /// The default Active Directory query timeout in seconds.
    /// </summary>
    public const int DefaultAdQueryTimeoutSeconds = 30;

    /// <summary>
    /// The maximum number of results returned per paginated query.
    /// </summary>
    public const int MaxResultsPerPage = 500;

    /// <summary>
    /// The standard WinRM HTTP port.
    /// </summary>
    public const int WinRmHttpPort = 5985;

    /// <summary>
    /// The standard WinRM HTTPS port.
    /// </summary>
    public const int WinRmHttpsPort = 5986;

    /// <summary>
    /// The default number of days to retain audit log entries.
    /// </summary>
    public const int AuditLogRetentionDays = 365;

    /// <summary>
    /// The default number of days after which a computer is considered stale.
    /// </summary>
    public const int DefaultStaleComputerDays = 90;

    /// <summary>
    /// The maximum number of parallel TCP connect checks during reachability testing.
    /// </summary>
    public const int ReachabilityCheckParallelism = 20;

    /// <summary>
    /// Switches to disk streaming when cumulative output exceeds this threshold (10 MB).
    /// </summary>
    public const long MaxInMemoryResultBytes = 10 * 1024 * 1024;

    /// <summary>
    /// The default category assigned to scripts without a manifest.
    /// </summary>
    public const string DefaultScriptCategory = "Uncategorized";
}
