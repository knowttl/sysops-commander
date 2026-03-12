using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net.Sockets;
using Serilog;
using SysOpsCommander.Core.Constants;
using SysOpsCommander.Core.Enums;
using SysOpsCommander.Core.Interfaces;
using SysOpsCommander.Core.Models;

namespace SysOpsCommander.Services;

/// <summary>
/// Orchestrates remote script execution across multiple hosts with pre-flight reachability checks,
/// parallel throttled execution, progress reporting, large-result streaming, and audit logging.
/// </summary>
public sealed class RemoteExecutionService : IRemoteExecutionService
{
    private readonly IEnumerable<IExecutionStrategy> _strategies;
    private readonly IAuditLogService _auditLogService;
    private readonly INotificationService _notificationService;
    private readonly ILogger _logger;
    private readonly ConcurrentDictionary<Guid, CancellationTokenSource> _runningJobs = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="RemoteExecutionService"/> class.
    /// </summary>
    /// <param name="strategies">All registered execution strategies.</param>
    /// <param name="auditLogService">The audit logging service.</param>
    /// <param name="notificationService">The toast notification service.</param>
    /// <param name="logger">The Serilog logger instance.</param>
    public RemoteExecutionService(
        IEnumerable<IExecutionStrategy> strategies,
        IAuditLogService auditLogService,
        INotificationService notificationService,
        ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(strategies);
        ArgumentNullException.ThrowIfNull(auditLogService);
        ArgumentNullException.ThrowIfNull(notificationService);
        ArgumentNullException.ThrowIfNull(logger);

        _strategies = strategies;
        _auditLogService = auditLogService;
        _notificationService = notificationService;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<ExecutionJob> ExecuteAsync(
        ExecutionJob job,
        IProgress<HostResult> progress,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(job);

        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _ = _runningJobs.TryAdd(job.Id, linkedCts);

        var totalStopwatch = Stopwatch.StartNew();
        job.Status = ExecutionStatus.Running;
        job.StartTime = DateTime.UtcNow;

        try
        {
            IExecutionStrategy strategy = SelectStrategy(job.ExecutionType);

            List<HostTarget> reachableHosts = await RunPreFlightChecksAsync(
                job.TargetHosts,
                job.WinRmConnectionOptions,
                progress,
                linkedCts.Token).ConfigureAwait(false);

            if (reachableHosts.Count == 0)
            {
                _logger.Warning("No reachable hosts for job {JobId}", job.Id);
                job.Status = ExecutionStatus.Failed;
                job.EndTime = DateTime.UtcNow;
                return job;
            }

            IList<HostResult> results = await ExecuteOnHostsAsync(
                strategy,
                reachableHosts,
                job,
                progress,
                linkedCts.Token).ConfigureAwait(false);

            await StreamLargeResultsAsync(results, job.Id).ConfigureAwait(false);

            foreach (HostResult result in results)
            {
                job.Results.Add(result);
            }

            totalStopwatch.Stop();
            job.EndTime = DateTime.UtcNow;

            int successCount = results.Count(r => r.Status == HostStatus.Success);
            int failCount = results.Count(r => r.Status == HostStatus.Failed);

            job.Status = failCount == 0
                ? ExecutionStatus.Completed
                : successCount == 0
                    ? ExecutionStatus.Failed
                    : ExecutionStatus.PartialFailure;

            _logger.Information(
                "Job {JobId} completed: {Success}/{Total} succeeded in {Duration}ms",
                job.Id, successCount, results.Count, totalStopwatch.ElapsedMilliseconds);

            await LogAuditEntryAsync(job, totalStopwatch.Elapsed, linkedCts.Token).ConfigureAwait(false);
            _notificationService.ShowExecutionComplete(job);

            return job;
        }
        catch (OperationCanceledException)
        {
            job.Status = ExecutionStatus.Cancelled;
            job.EndTime = DateTime.UtcNow;
            _logger.Information("Job {JobId} was cancelled", job.Id);
            return job;
        }
        catch (Exception ex)
        {
            job.Status = ExecutionStatus.Failed;
            job.EndTime = DateTime.UtcNow;
            _logger.Error(ex, "Job {JobId} failed with unexpected error", job.Id);
            throw;
        }
        finally
        {
            _ = _runningJobs.TryRemove(job.Id, out _);
        }
    }

    /// <inheritdoc/>
    public Task CancelExecutionAsync(Guid jobId)
    {
        if (_runningJobs.TryGetValue(jobId, out CancellationTokenSource? cts))
        {
            _logger.Information("Cancellation requested for job {JobId}", jobId);
            cts.Cancel();
        }
        else
        {
            _logger.Warning("Cannot cancel job {JobId} — not found in running jobs", jobId);
        }

        return Task.CompletedTask;
    }

    private IExecutionStrategy SelectStrategy(ExecutionType executionType)
    {
        IExecutionStrategy? strategy = _strategies.FirstOrDefault(s => s.Type == executionType);
        return strategy ?? throw new InvalidOperationException(
            $"No execution strategy registered for type {executionType}");
    }

    private async Task<List<HostTarget>> RunPreFlightChecksAsync(
        IReadOnlyList<HostTarget> targets,
        WinRmConnectionOptions connectionOptions,
        IProgress<HostResult> progress,
        CancellationToken cancellationToken)
    {
        int port = connectionOptions.GetEffectivePort();
        _logger.Information("Starting pre-flight reachability check on port {Port} for {Count} hosts", port, targets.Count);

        List<HostTarget> reachableHosts = [];

        await Parallel.ForEachAsync(
            targets,
            new ParallelOptions
            {
                MaxDegreeOfParallelism = AppConstants.ReachabilityCheckParallelism,
                CancellationToken = cancellationToken
            },
            async (target, ct) =>
            {
                bool isReachable = await CheckHostReachabilityAsync(target.Hostname, port, ct).ConfigureAwait(false);

                if (isReachable)
                {
                    target.Status = HostStatus.Reachable;
                    lock (reachableHosts)
                    {
                        reachableHosts.Add(target);
                    }
                }
                else
                {
                    target.Status = HostStatus.Unreachable;
                    var unreachableResult = HostResult.Failure(target.Hostname,
                        $"Host unreachable on port {port}");
                    progress?.Report(unreachableResult);
                }
            }).ConfigureAwait(false);

        _logger.Information(
            "Pre-flight check: {Reachable}/{Total} hosts reachable on port {Port}",
            reachableHosts.Count, targets.Count, port);

        return reachableHosts;
    }

    private static async Task<bool> CheckHostReachabilityAsync(string hostname, int port, CancellationToken cancellationToken)
    {
        try
        {
            using TcpClient client = new();
            Task connectTask = client.ConnectAsync(hostname, port, cancellationToken).AsTask();
            Task completedTask = await Task.WhenAny(
                connectTask,
                Task.Delay(TimeSpan.FromSeconds(2), cancellationToken)).ConfigureAwait(false);

            return completedTask == connectTask && connectTask.IsCompletedSuccessfully;
        }
        catch
        {
            return false;
        }
    }

    private async Task<IList<HostResult>> ExecuteOnHostsAsync(
        IExecutionStrategy strategy,
        List<HostTarget> reachableHosts,
        ExecutionJob job,
        IProgress<HostResult> progress,
        CancellationToken cancellationToken)
    {
        using SemaphoreSlim semaphore = new(job.ThrottleLimit);

        Task<HostResult>[] tasks = reachableHosts.Select(async host =>
        {
            await semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                host.Status = HostStatus.Running;

                HostResult result = await strategy.ExecuteAsync(
                    host.Hostname,
                    job.ScriptContent,
                    job.Parameters,
                    job.Credential,
                    job.WinRmConnectionOptions,
                    job.TimeoutSeconds,
                    cancellationToken).ConfigureAwait(false);

                progress?.Report(result);
                return result;
            }
            catch (OperationCanceledException)
            {
                return HostResult.Cancelled(host.Hostname);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Execution failed on {Hostname}", host.Hostname);
                return HostResult.Failure(host.Hostname, ex.Message);
            }
            finally
            {
                _ = semaphore.Release();
            }
        }).ToArray();

        HostResult[] results = await Task.WhenAll(tasks).ConfigureAwait(false);
        return results;
    }

    private async Task StreamLargeResultsAsync(IList<HostResult> results, Guid jobId)
    {
        long totalBytes = results.Sum(r => (long)(r.Output?.Length ?? 0) * sizeof(char));

        if (totalBytes <= AppConstants.MaxInMemoryResultBytes)
        {
            return;
        }

        _logger.Information(
            "Total output size {TotalBytes} bytes exceeds threshold — streaming to disk for job {JobId}",
            totalBytes, jobId);

        string tempDir = GetTempDirectory();
        _ = Directory.CreateDirectory(tempDir);

        for (int i = 0; i < results.Count; i++)
        {
            HostResult result = results[i];
            if (string.IsNullOrEmpty(result.Output))
            {
                continue;
            }

            string filePath = Path.Combine(tempDir, $"{jobId}_{result.Hostname}.txt");
            await File.WriteAllTextAsync(filePath, result.Output).ConfigureAwait(false);

            results[i] = new HostResult
            {
                Hostname = result.Hostname,
                Status = result.Status,
                Output = filePath,
                ErrorOutput = result.ErrorOutput,
                WarningOutput = result.WarningOutput,
                Duration = result.Duration,
                IsFileReference = true,
                CompletedAt = result.CompletedAt
            };
        }
    }

    private async Task LogAuditEntryAsync(ExecutionJob job, TimeSpan duration, CancellationToken cancellationToken)
    {
        try
        {
            AuditLogEntry entry = new()
            {
                Timestamp = job.StartTime,
                UserName = Environment.UserName,
                MachineName = Environment.MachineName,
                ScriptName = job.ScriptName,
                TargetHosts = string.Join(",", job.TargetHosts.Select(t => t.Hostname)),
                TargetHostCount = job.TargetHosts.Count,
                SuccessCount = job.Results.Count(r => r.Status == HostStatus.Success),
                FailureCount = job.Results.Count(r => r.Status == HostStatus.Failed),
                Status = job.Status,
                Duration = duration,
                ErrorSummary = job.Results
                    .FirstOrDefault(r => r.ErrorOutput is not null)?.ErrorOutput,
                AuthMethod = job.WinRmConnectionOptions.AuthMethod,
                Transport = job.WinRmConnectionOptions.Transport,
                TargetDomain = job.TargetDomain,
                CorrelationId = job.Id
            };

            await _auditLogService.LogExecutionAsync(entry, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to log audit entry for job {JobId}", job.Id);
        }
    }

    internal static string GetTempDirectory() =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            AppConstants.AppDataFolder,
            "Temp");

    /// <summary>
    /// Cleans up temp files older than 24 hours from the streaming output directory.
    /// </summary>
    public static void CleanupTempFiles()
    {
        string tempDir = GetTempDirectory();
        if (!Directory.Exists(tempDir))
        {
            return;
        }

        DateTime cutoff = DateTime.UtcNow.AddHours(-24);
        foreach (string file in Directory.GetFiles(tempDir, "*.txt"))
        {
            if (File.GetCreationTimeUtc(file) < cutoff)
            {
                try
                {
                    File.Delete(file);
                }
                catch
                {
                    // Best-effort cleanup — don't fail the application
                }
            }
        }
    }
}
