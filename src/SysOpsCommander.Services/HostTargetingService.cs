using System.Collections.ObjectModel;
using System.Net.Sockets;
using SysOpsCommander.Core.Constants;
using SysOpsCommander.Core.Enums;
using SysOpsCommander.Core.Interfaces;
using SysOpsCommander.Core.Models;

namespace SysOpsCommander.Services;

/// <summary>
/// Manages the observable collection of target hosts shared between views as a singleton.
/// </summary>
public sealed class HostTargetingService : IHostTargetingService
{
    /// <inheritdoc/>
    public ObservableCollection<HostTarget> Targets { get; } = [];

    /// <inheritdoc/>
    public void AddFromHostnames(IEnumerable<string> hostnames)
    {
        ArgumentNullException.ThrowIfNull(hostnames);
        foreach (string hostname in hostnames)
        {
            if (!string.IsNullOrWhiteSpace(hostname) &&
                !Targets.Any(t => string.Equals(t.Hostname, hostname.Trim(), StringComparison.OrdinalIgnoreCase)))
            {
                Targets.Add(new HostTarget
                {
                    Hostname = hostname.Trim(),
                    IsValidated = true
                });
            }
        }
    }

    /// <inheritdoc/>
    public async Task AddFromCsvFileAsync(string filePath, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(filePath);
        string[] lines = await File.ReadAllLinesAsync(filePath, cancellationToken).ConfigureAwait(false);
        IEnumerable<string> hostnames = lines
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

        IEnumerable<HostTarget> pendingTargets = Targets
            .Where(t => t.Status == HostStatus.Pending)
            .ToList();

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
                    target.Status = completedTask == connectTask && connectTask.IsCompletedSuccessfully
                        ? HostStatus.Reachable
                        : HostStatus.Unreachable;
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    throw;
                }
                catch
                {
                    target.Status = HostStatus.Unreachable;
                }
            }).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public void ClearTargets() => Targets.Clear();

    /// <inheritdoc/>
    public void RemoveTarget(string hostname)
    {
        HostTarget? target = Targets.FirstOrDefault(
            t => string.Equals(t.Hostname, hostname, StringComparison.OrdinalIgnoreCase));
        if (target is not null)
        {
            _ = Targets.Remove(target);
        }
    }
}

