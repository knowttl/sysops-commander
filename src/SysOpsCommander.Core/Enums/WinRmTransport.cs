namespace SysOpsCommander.Core.Enums;

/// <summary>
/// Represents the WinRM transport protocol.
/// </summary>
public enum WinRmTransport
{
    /// <summary>
    /// HTTP transport on port 5985 (default).
    /// </summary>
    HTTP,

    /// <summary>
    /// HTTPS transport on port 5986. Requires a certificate on the target host.
    /// </summary>
    HTTPS
}
