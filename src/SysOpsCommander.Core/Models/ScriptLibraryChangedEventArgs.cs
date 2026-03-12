namespace SysOpsCommander.Core.Models;

/// <summary>
/// Event arguments raised when the script library changes after a refresh.
/// </summary>
public sealed class ScriptLibraryChangedEventArgs : EventArgs
{
    /// <summary>
    /// Gets the scripts that were added since the last load.
    /// </summary>
    public IReadOnlyList<ScriptPlugin> Added { get; init; } = [];

    /// <summary>
    /// Gets the file paths of scripts that were removed since the last load.
    /// </summary>
    public IReadOnlyList<string> Removed { get; init; } = [];

    /// <summary>
    /// Gets the scripts that were modified since the last load.
    /// </summary>
    public IReadOnlyList<ScriptPlugin> Modified { get; init; } = [];
}
