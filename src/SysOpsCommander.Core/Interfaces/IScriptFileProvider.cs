using SysOpsCommander.Core.Models;

namespace SysOpsCommander.Core.Interfaces;

/// <summary>
/// Discovers PowerShell script files from configured directories.
/// </summary>
public interface IScriptFileProvider
{
    /// <summary>
    /// Gets the list of directories to scan for scripts, resolved from settings.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The resolved script directory paths.</returns>
    Task<IReadOnlyList<string>> GetScriptDirectoriesAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Scans all configured directories recursively for .ps1 files.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A list of discovered script file information.</returns>
    Task<IReadOnlyList<ScriptFileInfo>> ScanForScriptsAsync(CancellationToken cancellationToken);
}
