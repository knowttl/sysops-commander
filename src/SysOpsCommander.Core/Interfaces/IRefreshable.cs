namespace SysOpsCommander.Core.Interfaces;

/// <summary>
/// Marks a ViewModel as supporting F5-style refresh so the shell can delegate refresh commands.
/// </summary>
public interface IRefreshable
{
    /// <summary>
    /// Refreshes the current view's data.
    /// </summary>
    /// <returns>A task representing the asynchronous refresh operation.</returns>
    Task RefreshAsync();
}
