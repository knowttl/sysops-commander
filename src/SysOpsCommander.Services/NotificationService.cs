using Microsoft.Toolkit.Uwp.Notifications;
using Serilog;
using SysOpsCommander.Core.Enums;
using SysOpsCommander.Core.Interfaces;
using SysOpsCommander.Core.Models;

namespace SysOpsCommander.Services;

/// <summary>
/// Provides Windows toast notifications for execution events and general alerts.
/// </summary>
public sealed class NotificationService : INotificationService
{
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="NotificationService"/> class.
    /// </summary>
    /// <param name="logger">The Serilog logger instance.</param>
    public NotificationService(ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        _logger = logger;
    }

    /// <inheritdoc/>
    public void ShowToast(string title, string message, NotificationSeverity severity)
    {
        ArgumentNullException.ThrowIfNull(title);
        ArgumentNullException.ThrowIfNull(message);

        try
        {
            new ToastContentBuilder()
                .AddText(title)
                .AddText(message)
                .Show();

            _logger.Debug("Toast notification shown: {Title} [{Severity}]", title, severity);
        }
        catch (Exception ex)
        {
            _logger.Warning(ex, "Failed to show toast notification: {Title}", title);
        }
    }

    /// <inheritdoc/>
    public void ShowExecutionComplete(ExecutionJob job)
    {
        ArgumentNullException.ThrowIfNull(job);

        int successCount = job.Results.Count(r => r.Status == HostStatus.Success);
        int failCount = job.Results.Count(r => r.Status == HostStatus.Failed);
        int totalCount = job.Results.Count;

        try
        {
            new ToastContentBuilder()
                .AddText($"Execution Complete: {job.ScriptName}")
                .AddText($"{successCount}/{totalCount} succeeded, {failCount} failed")
                .AddArgument("action", "viewResults")
                .AddArgument("jobId", job.Id.ToString())
                .Show();

            _logger.Information(
                "Execution notification shown for job {JobId}: {Success}/{Total} succeeded",
                job.Id,
                successCount,
                totalCount);
        }
        catch (Exception ex)
        {
            _logger.Warning(ex, "Failed to show execution notification for job {JobId}", job.Id);
        }
    }
}
