using System.Globalization;
using SysOpsCommander.Core.Constants;
using SysOpsCommander.Core.Interfaces;
using SysOpsCommander.Core.Models;

namespace SysOpsCommander.Services;

/// <summary>
/// Implements three-tier settings layering: per-user (SQLite) > org default (appsettings.json) > hard default (AppConstants).
/// </summary>
public sealed class SettingsService : ISettingsService
{
    private readonly ISettingsRepository _repository;
    private readonly AppConfiguration _appConfiguration;

    // Maps setting keys to AppConfiguration property values and hard defaults
    private readonly Dictionary<string, (Func<AppConfiguration, string> OrgDefault, string HardDefault)> _settingsMap;

    /// <summary>
    /// Initializes a new instance of the <see cref="SettingsService"/> class.
    /// </summary>
    /// <param name="repository">The per-user settings repository.</param>
    /// <param name="appConfiguration">The org-wide configuration from appsettings.json.</param>
    public SettingsService(ISettingsRepository repository, AppConfiguration appConfiguration)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(appConfiguration);
        _repository = repository;
        _appConfiguration = appConfiguration;

        _settingsMap = new Dictionary<string, (Func<AppConfiguration, string> OrgDefault, string HardDefault)>(StringComparer.OrdinalIgnoreCase)
        {
            ["SharedScriptRepositoryPath"] = (c => c.SharedScriptRepositoryPath, string.Empty),
            ["DefaultThrottle"] = (c => c.DefaultThrottle.ToString(CultureInfo.InvariantCulture), AppConstants.DefaultThrottle.ToString(CultureInfo.InvariantCulture)),
            ["DefaultTimeoutSeconds"] = (c => c.DefaultTimeoutSeconds.ToString(CultureInfo.InvariantCulture), AppConstants.DefaultWinRmTimeoutSeconds.ToString(CultureInfo.InvariantCulture)),
            ["DefaultWinRmTransport"] = (c => c.DefaultWinRmTransport, "HTTP"),
            ["DefaultWinRmAuthMethod"] = (c => c.DefaultWinRmAuthMethod, "Kerberos"),
            ["StaleComputerThresholdDays"] = (c => c.StaleComputerThresholdDays.ToString(CultureInfo.InvariantCulture), AppConstants.DefaultStaleComputerDays.ToString(CultureInfo.InvariantCulture)),
            ["AuditLogRetentionDays"] = (c => c.AuditLogRetentionDays.ToString(CultureInfo.InvariantCulture), AppConstants.AuditLogRetentionDays.ToString(CultureInfo.InvariantCulture))
        };
    }

    /// <inheritdoc/>
    public async Task<string> GetAsync(string key, string defaultValue, CancellationToken cancellationToken)
    {
        string? value = await _repository.GetValueAsync(key, cancellationToken).ConfigureAwait(false);
        return value ?? defaultValue;
    }

    /// <inheritdoc/>
    public async Task SetAsync(string key, string value, CancellationToken cancellationToken) =>
        await _repository.SetValueAsync(key, value, cancellationToken).ConfigureAwait(false);

    /// <inheritdoc/>
    public async Task<T> GetTypedAsync<T>(string key, T defaultValue, CancellationToken cancellationToken)
        where T : IParsable<T>
    {
        string? value = await _repository.GetValueAsync(key, cancellationToken).ConfigureAwait(false);
        return value is not null && T.TryParse(value, CultureInfo.InvariantCulture, out T? parsed)
            ? parsed
            : defaultValue;
    }

    /// <inheritdoc/>
    public string GetOrgDefault(string key) =>
        _settingsMap.TryGetValue(key, out (Func<AppConfiguration, string> OrgDefault, string HardDefault) mapping)
            ? mapping.OrgDefault(_appConfiguration)
            : string.Empty;

    /// <inheritdoc/>
    public async Task<string> GetEffectiveAsync(string key, CancellationToken cancellationToken)
    {
        // Tier 1: Per-user override from SQLite
        string? userValue = await _repository.GetValueAsync(key, cancellationToken).ConfigureAwait(false);
        if (!string.IsNullOrEmpty(userValue))
        {
            return userValue;
        }

        if (_settingsMap.TryGetValue(key, out (Func<AppConfiguration, string> OrgDefault, string HardDefault) mapping))
        {
            // Tier 2: Org default from appsettings.json
            string orgValue = mapping.OrgDefault(_appConfiguration);
            if (!string.IsNullOrEmpty(orgValue))
            {
                return orgValue;
            }

            // Tier 3: Hard default from AppConstants
            return mapping.HardDefault;
        }

        return string.Empty;
    }
}
