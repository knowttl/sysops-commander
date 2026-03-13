using Serilog;

namespace SysOpsCommander.Core.Extensions;

/// <summary>
/// Provides extension methods for safe fire-and-forget async patterns.
/// </summary>
public static class TaskExtensions
{
    /// <summary>
    /// Observes a fire-and-forget task, logging any unhandled exceptions
    /// instead of letting them surface as unobserved task exceptions.
    /// </summary>
    /// <param name="task">The task to observe.</param>
    /// <param name="logger">Optional Serilog logger for error logging.</param>
    /// <param name="callerName">
    /// Name of the calling method, used in the log message. Automatically populated by the compiler.
    /// </param>
    public static async void SafeFireAndForget(
        this Task task,
        ILogger? logger = null,
        [System.Runtime.CompilerServices.CallerMemberName] string callerName = "")
    {
        try
        {
            await task.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Cancellation is expected — no action needed
        }
        catch (Exception ex)
        {
            if (logger is not null)
            {
                logger.Error(ex, "Unhandled exception in fire-and-forget call from {CallerName}", callerName);
            }
            else
            {
                Log.Error(ex, "Unhandled exception in fire-and-forget call from {CallerName}", callerName);
            }
        }
    }
}
