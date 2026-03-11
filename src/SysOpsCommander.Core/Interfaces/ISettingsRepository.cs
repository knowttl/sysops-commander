using SysOpsCommander.Core.Models;

namespace SysOpsCommander.Core.Interfaces;

/// <summary>
/// Data access layer for per-user settings in the SQLite database.
/// </summary>
public interface ISettingsRepository
{
    /// <summary>
    /// Gets the value for the specified setting key.
    /// </summary>
    /// <param name="key">The setting key.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The setting value, or <see langword="null"/> if the key does not exist.</returns>
    Task<string?> GetValueAsync(string key, CancellationToken cancellationToken);

    /// <summary>
    /// Sets the value for the specified setting key (insert or replace).
    /// </summary>
    /// <param name="key">The setting key.</param>
    /// <param name="value">The value to store.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task SetValueAsync(string key, string value, CancellationToken cancellationToken);

    /// <summary>
    /// Gets all stored user settings.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A list of all user settings.</returns>
    Task<IReadOnlyList<UserSettings>> GetAllAsync(CancellationToken cancellationToken);
}
