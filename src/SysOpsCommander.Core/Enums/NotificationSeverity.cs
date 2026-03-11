namespace SysOpsCommander.Core.Enums;

/// <summary>
/// Represents the severity level for user-facing notifications.
/// </summary>
public enum NotificationSeverity
{
    /// <summary>
    /// Informational notification with no action required.
    /// </summary>
    Information,

    /// <summary>
    /// Indicates a successful operation.
    /// </summary>
    Success,

    /// <summary>
    /// Indicates a potential issue that may require attention.
    /// </summary>
    Warning,

    /// <summary>
    /// Indicates an error that requires user attention.
    /// </summary>
    Error
}
