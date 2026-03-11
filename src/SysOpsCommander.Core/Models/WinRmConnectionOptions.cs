using SysOpsCommander.Core.Constants;
using SysOpsCommander.Core.Enums;

namespace SysOpsCommander.Core.Models;

/// <summary>
/// Represents WinRM connection configuration for remote PowerShell sessions.
/// </summary>
public sealed class WinRmConnectionOptions
{
    /// <summary>
    /// Gets or sets the authentication method. Defaults to <see cref="WinRmAuthMethod.Kerberos"/>.
    /// </summary>
    public WinRmAuthMethod AuthMethod { get; set; } = WinRmAuthMethod.Kerberos;

    /// <summary>
    /// Gets or sets the transport protocol. Defaults to <see cref="WinRmTransport.HTTP"/>.
    /// </summary>
    public WinRmTransport Transport { get; set; } = WinRmTransport.HTTP;

    /// <summary>
    /// Gets or sets an optional custom port override.
    /// </summary>
    public int? CustomPort { get; set; }

    /// <summary>
    /// Gets or sets an optional custom shell URI.
    /// </summary>
    public string? ShellUri { get; set; }

    /// <summary>
    /// Returns the effective port based on <see cref="CustomPort"/> and <see cref="Transport"/>.
    /// </summary>
    /// <returns>The port number to use for WinRM connections.</returns>
    public int GetEffectivePort() =>
        CustomPort ?? (Transport == WinRmTransport.HTTPS
            ? AppConstants.WinRmHttpsPort
            : AppConstants.WinRmHttpPort);

    /// <summary>
    /// Creates a default <see cref="WinRmConnectionOptions"/> with Kerberos/HTTP configuration.
    /// </summary>
    /// <returns>A new instance with default settings.</returns>
    public static WinRmConnectionOptions CreateDefault() => new();
}
