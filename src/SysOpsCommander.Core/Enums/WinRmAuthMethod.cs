namespace SysOpsCommander.Core.Enums;

/// <summary>
/// Represents the WinRM authentication method for remote PowerShell sessions.
/// </summary>
public enum WinRmAuthMethod
{
    /// <summary>
    /// Kerberos authentication. Default method; requires domain membership.
    /// </summary>
    Kerberos,

    /// <summary>
    /// NTLM authentication. Fallback for non-domain or cross-forest scenarios.
    /// </summary>
    NTLM,

    /// <summary>
    /// CredSSP authentication. Enables credential delegation; requires GPO configuration on both client and server.
    /// </summary>
    CredSSP
}
