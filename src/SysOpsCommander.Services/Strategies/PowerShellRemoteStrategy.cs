using System.Diagnostics;
using System.Management.Automation;
using System.Management.Automation.Remoting;
using System.Management.Automation.Runspaces;
using System.Runtime.Versioning;
using Serilog;
using SysOpsCommander.Core.Enums;
using SysOpsCommander.Core.Interfaces;
using SysOpsCommander.Core.Models;

namespace SysOpsCommander.Services.Strategies;

/// <summary>
/// Executes PowerShell scripts on remote hosts via WinRM using configurable authentication and transport.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class PowerShellRemoteStrategy : IExecutionStrategy
{
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="PowerShellRemoteStrategy"/> class.
    /// </summary>
    /// <param name="logger">The Serilog logger instance.</param>
    public PowerShellRemoteStrategy(ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        _logger = logger;
    }

    /// <inheritdoc/>
    public ExecutionType Type => ExecutionType.PowerShell;

    /// <inheritdoc/>
    public async Task<HostResult> ExecuteAsync(
        string hostname,
        string scriptContent,
        IDictionary<string, object>? parameters,
        PSCredential? credential,
        WinRmConnectionOptions connectionOptions,
        int timeoutSeconds,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(hostname);
        ArgumentNullException.ThrowIfNull(scriptContent);
        ArgumentNullException.ThrowIfNull(connectionOptions);

        var stopwatch = Stopwatch.StartNew();

        try
        {
            WSManConnectionInfo connInfo = BuildConnectionInfo(hostname, credential, connectionOptions, timeoutSeconds);

            _logger.Information(
                "Connecting to {Hostname} via {AuthMethod}/{Transport} on port {Port}",
                hostname,
                connectionOptions.AuthMethod,
                connectionOptions.Transport,
                connInfo.Port);

            using Runspace runspace = RunspaceFactory.CreateRunspace(connInfo);
            await Task.Run(runspace.Open, cancellationToken).ConfigureAwait(false);

            using var ps = PowerShell.Create();
            ps.Runspace = runspace;
            _ = ps.AddScript(scriptContent);

            if (parameters is not null)
            {
                foreach (KeyValuePair<string, object> kvp in parameters)
                {
                    _ = ps.AddParameter(kvp.Key, kvp.Value);
                }
            }

            System.Collections.ObjectModel.Collection<PSObject> output =
                await Task.Run(() => ps.Invoke(), cancellationToken).ConfigureAwait(false);

            stopwatch.Stop();

            string outputText = string.Join(
                Environment.NewLine,
                output.Select(o => o?.ToString() ?? string.Empty));

            string errorText = string.Join(
                Environment.NewLine,
                ps.Streams.Error.Select(e => e.ToString()));

            string warningText = string.Join(
                Environment.NewLine,
                ps.Streams.Warning.Select(w => w.ToString()));

            _logger.Information(
                "Execution completed on {Hostname} in {Duration}ms — HadErrors={HadErrors}",
                hostname,
                stopwatch.ElapsedMilliseconds,
                ps.HadErrors);

            return new HostResult
            {
                Hostname = hostname,
                Status = ps.HadErrors ? HostStatus.Failed : HostStatus.Success,
                Output = outputText,
                ErrorOutput = string.IsNullOrEmpty(errorText) ? null : errorText,
                WarningOutput = string.IsNullOrEmpty(warningText) ? null : warningText,
                Duration = stopwatch.Elapsed,
                CompletedAt = DateTime.UtcNow
            };
        }
        catch (PSRemotingTransportException ex) when (ex.Message.Contains("Access is denied", StringComparison.OrdinalIgnoreCase))
        {
            stopwatch.Stop();
            _logger.Warning(ex, "Authentication failed on {Hostname} using {AuthMethod}", hostname, connectionOptions.AuthMethod);
            return HostResult.Failure(hostname,
                $"Authentication failed for {hostname} using {connectionOptions.AuthMethod}. " +
                $"Verify credentials and that {connectionOptions.AuthMethod} is enabled on the target.");
        }
        catch (PSRemotingTransportException ex) when (ex.Message.Contains("CredSSP", StringComparison.OrdinalIgnoreCase))
        {
            stopwatch.Stop();
            string credSspMessage = MapCredSspError(hostname, ex);
            _logger.Warning(ex, "CredSSP authentication failed on {Hostname}: {Reason}", hostname, credSspMessage);
            return HostResult.Failure(hostname, credSspMessage);
        }
        catch (PSRemotingTransportException ex)
        {
            stopwatch.Stop();
            int port = connectionOptions.GetEffectivePort();
            _logger.Warning(ex, "WinRM transport error on {Hostname}:{Port}", hostname, port);
            return HostResult.Failure(hostname,
                $"WinRM connection failed to {hostname} on {connectionOptions.Transport}:{port}. " +
                $"Verify WinRM is enabled and the {connectionOptions.Transport} listener is configured. " +
                $"Error: {ex.Message}");
        }
        catch (UnauthorizedAccessException ex)
        {
            stopwatch.Stop();
            _logger.Warning(ex, "Access denied on {Hostname}", hostname);
            return HostResult.Failure(hostname,
                $"Access denied to {hostname}. Check credentials and remote management permissions.");
        }
        catch (OperationCanceledException)
        {
            stopwatch.Stop();
            _logger.Information("Execution cancelled on {Hostname}", hostname);
            return HostResult.Cancelled(hostname);
        }
        catch (Exception ex) when (IsTimeout(ex))
        {
            stopwatch.Stop();
            _logger.Warning("Execution timed out on {Hostname} after {Timeout}s", hostname, timeoutSeconds);
            return HostResult.Timeout(hostname, timeoutSeconds);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.Error(ex, "Unexpected error executing on {Hostname}", hostname);
            return HostResult.Failure(hostname, $"Unexpected error: {ex.Message}");
        }
    }

    /// <summary>
    /// Builds a <see cref="WSManConnectionInfo"/> from the provided connection options.
    /// </summary>
    internal static WSManConnectionInfo BuildConnectionInfo(
        string hostname,
        PSCredential? credential,
        WinRmConnectionOptions connectionOptions,
        int timeoutSeconds)
    {
        bool useSsl = connectionOptions.Transport == WinRmTransport.HTTPS;
        int port = connectionOptions.GetEffectivePort();
        string shellUri = connectionOptions.ShellUri
            ?? "http://schemas.microsoft.com/powershell/Microsoft.PowerShell";

        var connInfo = new WSManConnectionInfo(
            useSsl,
            hostname,
            port,
            "/wsman",
            shellUri,
            credential)
        {
            AuthenticationMechanism = connectionOptions.AuthMethod switch
            {
                WinRmAuthMethod.Kerberos => AuthenticationMechanism.Kerberos,
                WinRmAuthMethod.NTLM => AuthenticationMechanism.Negotiate,
                WinRmAuthMethod.CredSSP => AuthenticationMechanism.Credssp,
                _ => AuthenticationMechanism.Default
            },
            OperationTimeout = timeoutSeconds * 1000,
            OpenTimeout = 30_000
        };

        return connInfo;
    }

    /// <summary>
    /// Maps CredSSP-specific WinRM error messages to actionable user guidance.
    /// </summary>
    private static string MapCredSspError(string hostname, PSRemotingTransportException ex)
    {
        string message = ex.Message;

        return message.Contains("server role is not configured", StringComparison.OrdinalIgnoreCase)
            || message.Contains("not configured to receive", StringComparison.OrdinalIgnoreCase)
            ? $"CredSSP is not configured on target {hostname}. " +
              "Enable via GPO or run `Enable-WSManCredSSP -Role Server` on the target host."
            : message.Contains("client role is not configured", StringComparison.OrdinalIgnoreCase)
              || message.Contains("not configured to allow delegating", StringComparison.OrdinalIgnoreCase)
              || message.Contains("Group Policy", StringComparison.OrdinalIgnoreCase)
            ? "CredSSP Client is not enabled on this machine. " +
              "Run `Enable-WSManCredSSP -Role Client -DelegateComputer *` as administrator."
            : message.Contains("logon failure", StringComparison.OrdinalIgnoreCase)
              || message.Contains("incorrect user name or password", StringComparison.OrdinalIgnoreCase)
              || message.Contains("Access is denied", StringComparison.OrdinalIgnoreCase)
            ? $"CredSSP authentication failed for {hostname}. Verify the username and password."
            : $"CredSSP authentication failed for {hostname}. " +
              "Ensure CredSSP is enabled on both client (this machine) and server (target host). " +
              $"Error: {message}";
    }

    private static bool IsTimeout(Exception ex) =>
        ex.Message.Contains("timed out", StringComparison.OrdinalIgnoreCase) ||
        ex.Message.Contains("timeout", StringComparison.OrdinalIgnoreCase) ||
        ex is TimeoutException;
}
