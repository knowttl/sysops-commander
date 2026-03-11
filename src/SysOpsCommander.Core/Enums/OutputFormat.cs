namespace SysOpsCommander.Core.Enums;

/// <summary>
/// Represents the output rendering hint for script results.
/// This is a display directive only — it does not affect how output is parsed.
/// </summary>
public enum OutputFormat
{
    /// <summary>
    /// Renders output as plain text.
    /// </summary>
    Text,

    /// <summary>
    /// Renders output as a tabular grid.
    /// </summary>
    Table,

    /// <summary>
    /// Renders output as formatted JSON.
    /// </summary>
    Json
}
