using System.Collections.ObjectModel;
using System.Net.Sockets;
using Serilog;
using SysOpsCommander.Core.Constants;
using SysOpsCommander.Core.Enums;
using SysOpsCommander.Core.Interfaces;
using SysOpsCommander.Core.Models;
using SysOpsCommander.Core.Validation;

namespace SysOpsCommander.Services;

/// <summary>
/// Manages the observable collection of target hosts shared between views as a singleton.
/// Validates hostnames, de-duplicates entries, and performs TCP reachability checks.
/// </summary>
public sealed class HostTargetingService : IHostTargetingService
{
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="HostTargetingService"/> class.
    /// </summary>
    /// <param name="logger">The Serilog logger instance.</param>
    public HostTargetingService(ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        _logger = logger;
    }

    /// <inheritdoc/>
    public ObservableCollection<HostTarget> Targets { get; } = [];

    /// <inheritdoc/>
    public void AddFromHostnames(IEnumerable<string> hostnames)
    {
        ArgumentNullException.ThrowIfNull(hostnames);

        IReadOnlyList<(string Hostname, ValidationResult Result)> validationResults =
            HostnameValidator.ValidateMany(hostnames);

        int added = 0;
        int duplicates = 0;
        int invalid = 0;

        foreach ((string hostname, ValidationResult result) in validationResults)
        {
            if (!result.IsValid)
            {
                invalid++;
                _logger.Warning("Skipped invalid hostname: {Hostname} — {Error}", hostname, result.ErrorMessage);
                continue;
            }

            string trimmed = hostname.Trim();
            if (Targets.Any(t => string.Equals(t.Hostname, trimmed, StringComparison.OrdinalIgnoreCase)))
            {
                duplicates++;
                continue;
            }

            Targets.Add(new HostTarget
            {
                Hostname = trimmed,
                IsValidated = true
            });
            added++;
        }

        _logger.Information(
            "Added {Added} targets ({Duplicates} duplicates, {Invalid} invalid)",
            added, duplicates, invalid);
    }

    /// <inheritdoc/>
    public async Task AddFromCsvFileAsync(string filePath, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(filePath);
        string[] lines = await File.ReadAllLinesAsync(filePath, cancellationToken).ConfigureAwait(false);
        IEnumerable<string> hostnames = lines
            .Where(line => !string.IsNullOrWhiteSpace(line) && !line.TrimStart().StartsWith('#'))
            .Select(line => line.Split(',')[0].Trim())
            .Where(h => !string.IsNullOrWhiteSpace(h));
        AddFromHostnames(hostnames);
    }

    /// <inheritdoc/>
    public void AddFromAdSearchResults(IEnumerable<AdObject> computers)
    {
        ArgumentNullException.ThrowIfNull(computers);
        IEnumerable<string> hostnames = computers
            .Where(c => string.Equals(c.ObjectClass, "computer", StringComparison.OrdinalIgnoreCase))
            .Select(c => c.Name);
        AddFromHostnames(hostnames);
    }

    /// <inheritdoc/>
    public async Task CheckReachabilityAsync(WinRmConnectionOptions connectionOptions, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(connectionOptions);
        int port = connectionOptions.GetEffectivePort();

        var pendingTargets = Targets
            .Where(t => t.Status == HostStatus.Pending)
            .ToList();

        _logger.Information(
            "Starting reachability check for {Count} pending targets on port {Port}",
            pendingTargets.Count, port);

        var statusUpdates = new System.Collections.Concurrent.ConcurrentBag<(HostTarget Target, HostStatus Status)>();

        await Parallel.ForEachAsync(
            pendingTargets,
            new ParallelOptions
            {
                MaxDegreeOfParallelism = AppConstants.ReachabilityCheckParallelism,
                CancellationToken = cancellationToken
            },
            async (target, ct) =>
            {
                try
                {
                    using TcpClient client = new();
                    Task connectTask = client.ConnectAsync(target.Hostname, port, ct).AsTask();
                    Task completedTask = await Task.WhenAny(connectTask, Task.Delay(TimeSpan.FromSeconds(5), ct)).ConfigureAwait(false);
                    HostStatus status = completedTask == connectTask && connectTask.IsCompletedSuccessfully
                        ? HostStatus.Reachable
                        : HostStatus.Unreachable;
                    statusUpdates.Add((target, status));
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    throw;
                }
                catch
                {
                    statusUpdates.Add((target, HostStatus.Unreachable));
                }
            }).ConfigureAwait(false);

        // Apply status updates on the caller's thread to avoid cross-thread ObservableObject issues
        foreach ((HostTarget target, HostStatus status) in statusUpdates)
        {
            target.Status = status;
        }

        int reachable = pendingTargets.Count(t => t.Status == HostStatus.Reachable);
        _logger.Information(
            "Reachability check complete: {Reachable}/{Total} hosts reachable on port {Port}",
            reachable, pendingTargets.Count, port);
    }

    /// <inheritdoc/>
    public void ClearTargets()
    {
        int count = Targets.Count;
        Targets.Clear();
        _logger.Information("Cleared {Count} targets", count);
    }

    /// <inheritdoc/>
    public void RemoveTarget(string hostname)
    {
        HostTarget? target = Targets.FirstOrDefault(
            t => string.Equals(t.Hostname, hostname, StringComparison.OrdinalIgnoreCase));
        if (target is not null)
        {
            _ = Targets.Remove(target);
            _logger.Information("Removed target {Hostname}", hostname);
        }
    }
}

