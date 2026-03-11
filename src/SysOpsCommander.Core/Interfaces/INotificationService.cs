using SysOpsCommander.Core.Enums;
using SysOpsCommander.Core.Models;

namespace SysOpsCommander.Core.Interfaces;

/// <summary>
/// Provides user-facing toast notifications and execution completion alerts.
/// </summary>
public interface INotificationService
{
    /// <summary>
    /// Shows a toast notification with the specified severity.
    /// </summary>
    /// <param name="title">The notification title.</param>
    /// <param name="message">The notification message body.</param>
    /// <param name="severity">The severity level for visual styling.</param>
    void ShowToast(string title, string message, NotificationSeverity severity);

    /// <summary>
    /// Shows a notification summarizing a completed execution job.
    /// </summary>
    /// <param name="job">The completed execution job.</param>
    void ShowExecutionComplete(ExecutionJob job);
}
