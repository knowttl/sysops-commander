using SysOpsCommander.Core.Models;

namespace SysOpsCommander.Core.Interfaces;

/// <summary>
/// Loads and caches PowerShell script plugins from script directories.
/// </summary>
public interface IScriptLoaderService
{
    /// <summary>
    /// Loads all scripts from configured script directories.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A list of loaded script plugins.</returns>
    Task<IReadOnlyList<ScriptPlugin>> LoadAllScriptsAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Loads a single script from the specified file path.
    /// </summary>
    /// <param name="filePath">The full path to the .ps1 script file.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The loaded script plugin.</returns>
    Task<ScriptPlugin> LoadScriptAsync(string filePath, CancellationToken cancellationToken);

    /// <summary>
    /// Refreshes the script cache by reloading all scripts from disk.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task RefreshAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Raised when the script library changes after a refresh (scripts added, removed, or modified).
    /// </summary>
    event EventHandler<ScriptLibraryChangedEventArgs>? LibraryChanged;
}
