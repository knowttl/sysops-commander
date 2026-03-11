namespace SysOpsCommander.Core.Models;

/// <summary>
/// Represents the org-wide application configuration loaded from appsettings.json.
/// Per-user overrides are stored separately in SQLite.
/// </summary>
public sealed class AppConfiguration
{
    /// <summary>
    /// Gets or sets the UNC path to the shared script repository.
    /// </summary>
    public string SharedScriptRepositoryPath { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the UNC path to the network share used for auto-updates.
    /// </summary>
    public string UpdateNetworkSharePath { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the default Active Directory domain. Empty string means auto-detect.
    /// </summary>
    public string DefaultDomain { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the default WinRM transport protocol ("HTTP" or "HTTPS").
    /// </summary>
    public string DefaultWinRmTransport { get; set; } = "HTTP";

    /// <summary>
    /// Gets or sets the default WinRM authentication method ("Kerberos", "NTLM", or "CredSSP").
    /// </summary>
    public string DefaultWinRmAuthMethod { get; set; } = "Kerberos";

    /// <summary>
    /// Gets or sets the default number of concurrent remote executions.
    /// </summary>
    public int DefaultThrottle { get; set; } = 5;

    /// <summary>
    /// Gets or sets the default execution timeout in seconds.
    /// </summary>
    public int DefaultTimeoutSeconds { get; set; } = 60;

    /// <summary>
    /// Gets or sets the number of days of inactivity after which a computer is considered stale.
    /// </summary>
    public int StaleComputerThresholdDays { get; set; } = 90;

    /// <summary>
    /// Gets or sets the number of days to retain audit log entries.
    /// </summary>
    public int AuditLogRetentionDays { get; set; } = 365;
}
