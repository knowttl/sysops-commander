using System.Net;
using System.Net.Sockets;
using SysOpsCommander.Core.Enums;
using SysOpsCommander.Core.Interfaces;
using SysOpsCommander.Core.Models;

namespace SysOpsCommander.Services;

/// <summary>
/// Resolves DNS hostnames to IP addresses with throttled parallel execution.
/// </summary>
public sealed class DnsResolverService : IDnsResolverService
{
    private const int MaxParallelism = 20;
    private const int PerHostTimeoutMs = 3000;

    private readonly IDnsLookup _dnsLookup;

    /// <summary>
    /// Initializes a new instance of the <see cref="DnsResolverService"/> class.
    /// </summary>
    /// <param name="dnsLookup">The DNS lookup abstraction for testability.</param>
    internal DnsResolverService(IDnsLookup dnsLookup)
    {
        ArgumentNullException.ThrowIfNull(dnsLookup);
        _dnsLookup = dnsLookup;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="DnsResolverService"/> class using real DNS resolution.
    /// </summary>
    public DnsResolverService()
        : this(new SystemDnsLookup())
    {
    }

    /// <inheritdoc />
    public async Task<IpResolutionResult> ResolveAsync(string hostname, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(hostname))
        {
            return new IpResolutionResult
            {
                Status = IpResolutionStatus.NotApplicable,
                Hostname = hostname
            };
        }

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(PerHostTimeoutMs);

        try
        {
            IPAddress[] addresses = await _dnsLookup.GetHostAddressesAsync(hostname, timeoutCts.Token);

            string? primaryIpv4 = addresses
                .Where(a => a.AddressFamily == AddressFamily.InterNetwork)
                .Select(a => a.ToString())
                .FirstOrDefault();

            List<string> allAddresses = [.. addresses.Select(a => a.ToString())];

            return new IpResolutionResult
            {
                Status = IpResolutionStatus.Resolved,
                PrimaryIPv4 = primaryIpv4,
                AllAddresses = allAddresses,
                Hostname = hostname
            };
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new IpResolutionResult
            {
                Status = IpResolutionStatus.Failed,
                ErrorMessage = "DNS resolution timed out",
                Hostname = hostname
            };
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (SocketException ex)
        {
            return new IpResolutionResult
            {
                Status = IpResolutionStatus.Failed,
                ErrorMessage = ex.Message,
                Hostname = hostname
            };
        }
    }

    /// <inheritdoc />
    public async Task ResolveAllAsync(
        IReadOnlyList<(string Hostname, Action<IpResolutionResult> Callback)> requests,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(requests);

        if (requests.Count == 0)
        {
            return;
        }

        SynchronizationContext? capturedContext = SynchronizationContext.Current;

        await Parallel.ForEachAsync(
            requests,
            new ParallelOptions
            {
                MaxDegreeOfParallelism = MaxParallelism,
                CancellationToken = cancellationToken
            },
            async (request, ct) =>
            {
                IpResolutionResult result = await ResolveAsync(request.Hostname, ct);
                InvokeCallback(capturedContext, request.Callback, result);
            });
    }

    private static void InvokeCallback(
        SynchronizationContext? context,
        Action<IpResolutionResult> callback,
        IpResolutionResult result)
    {
        if (context is not null)
        {
            context.Post(_ => callback(result), null);
        }
        else
        {
            callback(result);
        }
    }
}

/// <summary>
/// Abstraction over <see cref="Dns.GetHostAddressesAsync(string, CancellationToken)"/> for testability.
/// </summary>
internal interface IDnsLookup
{
    /// <summary>
    /// Resolves a hostname to its IP addresses.
    /// </summary>
    /// <param name="hostname">The hostname to resolve.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>An array of resolved IP addresses.</returns>
    Task<IPAddress[]> GetHostAddressesAsync(string hostname, CancellationToken cancellationToken);
}

/// <summary>
/// Production implementation of <see cref="IDnsLookup"/> using <see cref="Dns"/>.
/// </summary>
internal sealed class SystemDnsLookup : IDnsLookup
{
    /// <inheritdoc />
    public Task<IPAddress[]> GetHostAddressesAsync(string hostname, CancellationToken cancellationToken) =>
        Dns.GetHostAddressesAsync(hostname, cancellationToken);
}
