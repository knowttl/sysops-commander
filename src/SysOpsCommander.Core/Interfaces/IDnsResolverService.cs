using SysOpsCommander.Core.Models;

namespace SysOpsCommander.Core.Interfaces;

/// <summary>
/// Resolves DNS hostnames to IP addresses for AD computer objects.
/// </summary>
public interface IDnsResolverService
{
    /// <summary>
    /// Resolves a single hostname to its IP addresses.
    /// </summary>
    /// <param name="hostname">The DNS hostname to resolve.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The resolution result containing IP addresses or error information.</returns>
    Task<IpResolutionResult> ResolveAsync(string hostname, CancellationToken cancellationToken = default);

    /// <summary>
    /// Resolves multiple hostnames in parallel with throttled concurrency.
    /// Each callback is invoked on the caller's synchronization context when a resolution completes.
    /// </summary>
    /// <param name="requests">The hostnames to resolve, each paired with a completion callback.</param>
    /// <param name="cancellationToken">A token to cancel all pending resolutions.</param>
    /// <returns>A task that completes when all resolutions finish or are cancelled.</returns>
    Task ResolveAllAsync(
        IReadOnlyList<(string Hostname, Action<IpResolutionResult> Callback)> requests,
        CancellationToken cancellationToken = default);
}
