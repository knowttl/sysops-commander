namespace SysOpsCommander.Core.Enums;

/// <summary>
/// Represents the overall status of a remote execution job.
/// </summary>
public enum ExecutionStatus
{
    /// <summary>
    /// Indicates the job has been created but not yet started.
    /// </summary>
    Pending,

    /// <summary>
    /// Indicates the job is validating target hosts and parameters.
    /// </summary>
    Validating,

    /// <summary>
    /// Indicates the job is actively executing on target hosts.
    /// </summary>
    Running,

    /// <summary>
    /// Indicates the job completed successfully on all hosts.
    /// </summary>
    Completed,

    /// <summary>
    /// Indicates the job completed but some hosts failed.
    /// </summary>
    PartialFailure,

    /// <summary>
    /// Indicates the job failed on all hosts.
    /// </summary>
    Failed,

    /// <summary>
    /// Indicates the job was cancelled by the user.
    /// </summary>
    Cancelled
}
