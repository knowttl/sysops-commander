namespace SysOpsCommander.Core.Enums;

/// <summary>
/// Represents the type of remote execution mechanism.
/// </summary>
public enum ExecutionType
{
    /// <summary>
    /// Executes via PowerShell Remoting (WinRM).
    /// </summary>
    PowerShell,

    /// <summary>
    /// Executes via Windows Management Instrumentation.
    /// </summary>
    WMI
}
