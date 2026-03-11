using SysOpsCommander.Core.Enums;

namespace SysOpsCommander.Core.Models;

/// <summary>
/// Represents the execution result for a single host.
/// </summary>
public sealed class HostResult
{
    /// <summary>
    /// Gets the hostname that was targeted.
    /// </summary>
    public required string Hostname { get; init; }

    /// <summary>
    /// Gets the final status of the execution on this host.
    /// </summary>
    public required HostStatus Status { get; init; }

    /// <summary>
    /// Gets the raw output text, or a file path if <see cref="IsFileReference"/> is <see langword="true"/>.
    /// </summary>
    public string Output { get; init; } = string.Empty;

    /// <summary>
    /// Gets the error stream text, if any.
    /// </summary>
    public string? ErrorOutput { get; init; }

    /// <summary>
    /// Gets the warning stream text, if any.
    /// </summary>
    public string? WarningOutput { get; init; }

    /// <summary>
    /// Gets the time taken to execute on this host.
    /// </summary>
    public TimeSpan Duration { get; init; }

    /// <summary>
    /// Gets a value indicating whether <see cref="Output"/> is a temp file path for large result streaming.
    /// </summary>
    public bool IsFileReference { get; init; }

    /// <summary>
    /// Gets the UTC timestamp when execution completed on this host.
    /// </summary>
    public DateTime CompletedAt { get; init; }

    /// <summary>
    /// Creates a successful result.
    /// </summary>
    /// <param name="hostname">The target hostname.</param>
    /// <param name="output">The script output.</param>
    /// <param name="duration">The execution duration.</param>
    /// <returns>A <see cref="HostResult"/> with <see cref="HostStatus.Success"/>.</returns>
    public static HostResult Success(string hostname, string output, TimeSpan duration) =>
        new()
        {
            Hostname = hostname,
            Status = HostStatus.Success,
            Output = output,
            Duration = duration,
            CompletedAt = DateTime.UtcNow
        };

    /// <summary>
    /// Creates a failed result.
    /// </summary>
    /// <param name="hostname">The target hostname.</param>
    /// <param name="errorMessage">The error message.</param>
    /// <returns>A <see cref="HostResult"/> with <see cref="HostStatus.Failed"/>.</returns>
    public static HostResult Failure(string hostname, string errorMessage) =>
        new()
        {
            Hostname = hostname,
            Status = HostStatus.Failed,
            ErrorOutput = errorMessage,
            CompletedAt = DateTime.UtcNow
        };

    /// <summary>
    /// Creates a cancelled result.
    /// </summary>
    /// <param name="hostname">The target hostname.</param>
    /// <returns>A <see cref="HostResult"/> with <see cref="HostStatus.Cancelled"/>.</returns>
    public static HostResult Cancelled(string hostname) =>
        new()
        {
            Hostname = hostname,
            Status = HostStatus.Cancelled,
            CompletedAt = DateTime.UtcNow
        };

    /// <summary>
    /// Creates a timed-out result.
    /// </summary>
    /// <param name="hostname">The target hostname.</param>
    /// <param name="timeoutSeconds">The timeout threshold in seconds.</param>
    /// <returns>A <see cref="HostResult"/> with <see cref="HostStatus.Timeout"/>.</returns>
    public static HostResult Timeout(string hostname, int timeoutSeconds) =>
        new()
        {
            Hostname = hostname,
            Status = HostStatus.Timeout,
            ErrorOutput = $"Execution timed out after {timeoutSeconds} seconds",
            Duration = TimeSpan.FromSeconds(timeoutSeconds),
            CompletedAt = DateTime.UtcNow
        };
}
