using System.Management.Automation;
using SysOpsCommander.Core.Enums;
using SysOpsCommander.Core.Models;

namespace SysOpsCommander.Core.Interfaces;

/// <summary>
/// Defines the strategy pattern for different remote execution mechanisms (PowerShell, WMI).
/// </summary>
public interface IExecutionStrategy
{
    /// <summary>
    /// Gets the execution type this strategy handles.
    /// </summary>
    ExecutionType Type { get; }

    /// <summary>
    /// Executes a script on a single remote host.
    /// </summary>
    /// <param name="hostname">The target hostname.</param>
    /// <param name="scriptContent">The raw .ps1 script content.</param>
    /// <param name="parameters">The script parameters to inject via <c>AddParameter()</c>.</param>
    /// <param name="credential">Optional credentials for the remote session.</param>
    /// <param name="connectionOptions">The WinRM connection configuration.</param>
    /// <param name="timeoutSeconds">The per-host execution timeout in seconds.</param>
    /// <param name="cancellationToken">A token to cancel the execution.</param>
    /// <returns>The result of the execution on this host.</returns>
    Task<HostResult> ExecuteAsync(
        string hostname,
        string scriptContent,
        IDictionary<string, object>? parameters,
        PSCredential? credential,
        WinRmConnectionOptions connectionOptions,
        int timeoutSeconds,
        CancellationToken cancellationToken);
}
