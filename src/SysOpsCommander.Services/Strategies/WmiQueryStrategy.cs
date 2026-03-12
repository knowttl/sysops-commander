using System.Diagnostics;
using System.Management;
using System.Management.Automation;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;
using Serilog;
using SysOpsCommander.Core.Enums;
using SysOpsCommander.Core.Interfaces;
using SysOpsCommander.Core.Models;

namespace SysOpsCommander.Services.Strategies;

/// <summary>
/// Executes WMI queries on remote hosts using DCOM authentication.
/// WMI uses the Windows authentication stack; the <see cref="WinRmAuthMethod"/> enum has limited applicability.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class WmiQueryStrategy : IExecutionStrategy
{
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="WmiQueryStrategy"/> class.
    /// </summary>
    /// <param name="logger">The Serilog logger instance.</param>
    public WmiQueryStrategy(ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        _logger = logger;
    }

    /// <inheritdoc/>
    public ExecutionType Type => ExecutionType.WMI;

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
            ConnectionOptions connOpts = BuildConnectionOptions(credential, timeoutSeconds);

            _logger.Information("Connecting to {Hostname} via WMI/DCOM", hostname);

            var scope = new ManagementScope($"\\\\{hostname}\\root\\cimv2", connOpts);
            await Task.Run(scope.Connect, cancellationToken).ConfigureAwait(false);

            using ManagementObjectSearcher searcher = new(scope, new ObjectQuery(scriptContent));
            ManagementObjectCollection results =
                await Task.Run(() => searcher.Get(), cancellationToken).ConfigureAwait(false);

            stopwatch.Stop();

            string outputText = FormatWmiResults(results);

            _logger.Information(
                "WMI query completed on {Hostname} in {Duration}ms",
                hostname,
                stopwatch.ElapsedMilliseconds);

            return HostResult.Success(hostname, outputText, stopwatch.Elapsed);
        }
        catch (ManagementException ex) when (ex.ErrorCode == ManagementStatus.AccessDenied)
        {
            stopwatch.Stop();
            _logger.Warning(ex, "WMI access denied on {Hostname}", hostname);
            return HostResult.Failure(hostname,
                $"WMI access denied on {hostname}. Verify credentials and DCOM permissions.");
        }
        catch (ManagementException ex)
        {
            stopwatch.Stop();
            _logger.Warning(ex, "WMI query error on {Hostname}: {ErrorCode}", hostname, ex.ErrorCode);
            return HostResult.Failure(hostname,
                $"WMI query failed on {hostname}. Error code: {ex.ErrorCode}. {ex.Message}");
        }
        catch (COMException ex)
        {
            stopwatch.Stop();
            _logger.Warning(ex, "DCOM error connecting to {Hostname}", hostname);
            return HostResult.Failure(hostname,
                $"DCOM connection failed to {hostname}. Verify the host is reachable and DCOM is enabled. Error: {ex.Message}");
        }
        catch (UnauthorizedAccessException ex)
        {
            stopwatch.Stop();
            _logger.Warning(ex, "Access denied on {Hostname}", hostname);
            return HostResult.Failure(hostname,
                $"Access denied to {hostname}. Check credentials and WMI namespace permissions.");
        }
        catch (OperationCanceledException)
        {
            stopwatch.Stop();
            _logger.Information("WMI query cancelled on {Hostname}", hostname);
            return HostResult.Cancelled(hostname);
        }
        catch (Exception ex) when (IsTimeout(ex))
        {
            stopwatch.Stop();
            _logger.Warning("WMI query timed out on {Hostname} after {Timeout}s", hostname, timeoutSeconds);
            return HostResult.Timeout(hostname, timeoutSeconds);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.Error(ex, "Unexpected WMI error on {Hostname}", hostname);
            return HostResult.Failure(hostname, $"Unexpected WMI error: {ex.Message}");
        }
    }

    /// <summary>
    /// Builds WMI <see cref="ConnectionOptions"/> from credential and timeout settings.
    /// WMI uses DCOM authentication — <see cref="WinRmAuthMethod"/> does not control the auth protocol.
    /// </summary>
    internal static ConnectionOptions BuildConnectionOptions(PSCredential? credential, int timeoutSeconds)
    {
        ConnectionOptions connOpts = new()
        {
            Authentication = AuthenticationLevel.PacketPrivacy,
            Impersonation = ImpersonationLevel.Impersonate,
            Timeout = TimeSpan.FromSeconds(timeoutSeconds)
        };

        if (credential is not null)
        {
            connOpts.Username = credential.UserName;
            connOpts.SecurePassword = credential.Password;
        }

        return connOpts;
    }

    private static string FormatWmiResults(ManagementObjectCollection results)
    {
        var sb = new StringBuilder();
        foreach (ManagementBaseObject obj in results)
        {
            foreach (PropertyData prop in obj.Properties)
            {
                _ = sb.Append(prop.Name).Append(" = ").AppendLine(prop.Value?.ToString());
            }

            _ = sb.AppendLine("---");
        }

        return sb.ToString().TrimEnd();
    }

    private static bool IsTimeout(Exception ex) =>
        ex.Message.Contains("timed out", StringComparison.OrdinalIgnoreCase) ||
        ex.Message.Contains("timeout", StringComparison.OrdinalIgnoreCase) ||
        ex is TimeoutException;
}
