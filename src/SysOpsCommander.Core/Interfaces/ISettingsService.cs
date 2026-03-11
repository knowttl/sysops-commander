namespace SysOpsCommander.Core.Interfaces;

/// <summary>
/// Provides three-tier settings management: per-user (SQLite) > org default (appsettings.json) > hard default (AppConstants).
/// </summary>
public interface ISettingsService
{
    /// <summary>
    /// Gets a setting value, falling back to the provided default if not found.
    /// </summary>
    /// <param name="key">The setting key.</param>
    /// <param name="defaultValue">The default value if the key is not found.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The setting value or the default.</returns>
    Task<string> GetAsync(string key, string defaultValue, CancellationToken cancellationToken);

    /// <summary>
    /// Sets a per-user setting value in the SQLite database.
    /// </summary>
    /// <param name="key">The setting key.</param>
    /// <param name="value">The value to store.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task SetAsync(string key, string value, CancellationToken cancellationToken);

    /// <summary>
    /// Gets a typed setting value, parsing it from the stored string representation.
    /// </summary>
    /// <typeparam name="T">The type to parse the value as. Must implement <see cref="IParsable{T}"/>.</typeparam>
    /// <param name="key">The setting key.</param>
    /// <param name="defaultValue">The default value if the key is not found or cannot be parsed.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The parsed setting value or the default.</returns>
    Task<T> GetTypedAsync<T>(string key, T defaultValue, CancellationToken cancellationToken) where T : IParsable<T>;

    /// <summary>
    /// Gets the organization-wide default value from appsettings.json.
    /// </summary>
    /// <param name="key">The setting key.</param>
    /// <returns>The org default value, or an empty string if not configured.</returns>
    string GetOrgDefault(string key);

    /// <summary>
    /// Gets the effective setting value using three-tier layering:
    /// per-user override > org default (appsettings.json) > hard default (AppConstants).
    /// </summary>
    /// <param name="key">The setting key.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The effective setting value.</returns>
    Task<string> GetEffectiveAsync(string key, CancellationToken cancellationToken);
}
