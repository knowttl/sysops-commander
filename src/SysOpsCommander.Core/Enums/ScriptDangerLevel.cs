namespace SysOpsCommander.Core.Enums;

/// <summary>
/// Represents the danger level of a script, used for confirmation prompts and visual indicators.
/// </summary>
public enum ScriptDangerLevel
{
    /// <summary>
    /// Indicates the script performs read-only or non-destructive operations.
    /// </summary>
    Safe,

    /// <summary>
    /// Indicates the script modifies state but is generally reversible.
    /// </summary>
    Caution,

    /// <summary>
    /// Indicates the script performs potentially irreversible or destructive operations.
    /// </summary>
    Destructive
}
