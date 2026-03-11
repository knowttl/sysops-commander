using SysOpsCommander.Core.Models;

namespace SysOpsCommander.Core.Interfaces;

/// <summary>
/// Executes scripts on remote hosts via PowerShell Remoting or WMI.
/// </summary>
public interface IRemoteExecutionService
{
    /// <summary>
    /// Executes the specified job across all target hosts with progress reporting.
    /// </summary>
    /// <param name="job">The execution job containing script, targets, and connection options.</param>
    /// <param name="progress">Reports per-host results as they complete.</param>
    /// <param name="cancellationToken">A token to cancel the execution.</param>
    /// <returns>The completed execution job with populated results.</returns>
    Task<ExecutionJob> ExecuteAsync(ExecutionJob job, IProgress<HostResult> progress, CancellationToken cancellationToken);

    /// <summary>
    /// Cancels a running execution by job identifier.
    /// </summary>
    /// <param name="jobId">The unique identifier of the job to cancel.</param>
    /// <returns>A task representing the asynchronous cancellation.</returns>
    Task CancelExecutionAsync(Guid jobId);
}
