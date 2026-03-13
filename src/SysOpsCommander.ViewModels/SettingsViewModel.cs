using System.Reflection;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Serilog;
using SysOpsCommander.Core.Constants;
using SysOpsCommander.Core.Enums;
using SysOpsCommander.Core.Interfaces;

namespace SysOpsCommander.ViewModels;

/// <summary>
/// ViewModel for the Settings view. Manages application configuration and preferences.
/// </summary>
public partial class SettingsViewModel : ObservableObject, IRefreshable, IDisposable
{
    private readonly ISettingsService _settingsService;
    private readonly ILogger _logger;
    private readonly CancellationTokenSource _cts = new();
    private bool _isLoading;

    // --- Domain & Connection ---

    [ObservableProperty]
    private string _defaultDomain = string.Empty;

    [ObservableProperty]
    private string _orgDefaultDomain = string.Empty;

    [ObservableProperty]
    private WinRmAuthMethod _defaultAuthMethod;

    [ObservableProperty]
    private WinRmTransport _defaultTransport;

    [ObservableProperty]
    private int _staleComputerThresholdDays;

    // --- Execution ---

    [ObservableProperty]
    private int _defaultThrottle;

    [ObservableProperty]
    private int _defaultTimeoutSeconds;

    // --- Repository ---

    [ObservableProperty]
    private string _orgScriptRepositoryPath = string.Empty;

    [ObservableProperty]
    private bool _hasUserScriptPathOverride;

    [ObservableProperty]
    private string _userScriptRepositoryPath = string.Empty;

    // --- Logging ---

    [ObservableProperty]
    private string _logLevel = "Information";

    // --- Update ---

    [ObservableProperty]
    private string _updateSharePath = string.Empty;

    [ObservableProperty]
    private string _currentVersion = string.Empty;

    [ObservableProperty]
    private string _updateStatus = string.Empty;

    [ObservableProperty]
    private bool _isUpdateAvailable;

    // --- State ---

    [ObservableProperty]
    private bool _hasUnsavedChanges;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    /// <summary>
    /// Initializes a new instance of the <see cref="SettingsViewModel"/> class.
    /// </summary>
    public SettingsViewModel(
        ISettingsService settingsService,
        ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(settingsService);
        ArgumentNullException.ThrowIfNull(logger);

        _settingsService = settingsService;
        _logger = logger;
    }

    /// <summary>
    /// Loads all settings from the settings service.
    /// </summary>
    [RelayCommand]
    private async Task LoadSettingsAsync()
    {
        _isLoading = true;
        try
        {
            OrgDefaultDomain = _settingsService.GetOrgDefault("DefaultDomain");
            OrgScriptRepositoryPath = _settingsService.GetOrgDefault("SharedScriptRepositoryPath");

            string domain = await _settingsService.GetEffectiveAsync("DefaultDomain", _cts.Token);
            DefaultDomain = domain;

            string authMethod = await _settingsService.GetEffectiveAsync("DefaultWinRmAuthMethod", _cts.Token);
            if (Enum.TryParse<WinRmAuthMethod>(authMethod, ignoreCase: true, out WinRmAuthMethod auth))
            {
                DefaultAuthMethod = auth;
            }

            string transport = await _settingsService.GetEffectiveAsync("DefaultWinRmTransport", _cts.Token);
            if (Enum.TryParse<WinRmTransport>(transport, ignoreCase: true, out WinRmTransport trans))
            {
                DefaultTransport = trans;
            }

            StaleComputerThresholdDays = await _settingsService.GetTypedAsync("StaleComputerThresholdDays", AppConstants.DefaultStaleComputerDays, _cts.Token);
            DefaultThrottle = await _settingsService.GetTypedAsync("DefaultThrottle", AppConstants.DefaultThrottle, _cts.Token);
            DefaultTimeoutSeconds = await _settingsService.GetTypedAsync("DefaultTimeoutSeconds", AppConstants.DefaultWinRmTimeoutSeconds, _cts.Token);

            string userScriptPath = await _settingsService.GetAsync("UserScriptRepositoryPath", string.Empty, _cts.Token);
            HasUserScriptPathOverride = !string.IsNullOrWhiteSpace(userScriptPath);
            UserScriptRepositoryPath = userScriptPath;

            LogLevel = await _settingsService.GetAsync("LogLevel", "Information", _cts.Token);

            UpdateSharePath = await _settingsService.GetAsync("UpdateNetworkSharePath", string.Empty, _cts.Token);

            CurrentVersion = Assembly.GetEntryAssembly()?.GetName().Version?.ToString() ?? "1.0.0";

            HasUnsavedChanges = false;
            StatusMessage = "Settings loaded.";
        }
        catch (OperationCanceledException)
        {
            // Cancelled
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to load settings");
            StatusMessage = "Failed to load settings.";
        }
        finally
        {
            _isLoading = false;
        }
    }

    /// <summary>
    /// Saves all per-user settings.
    /// </summary>
    [RelayCommand]
    private async Task SaveSettingsAsync()
    {
        try
        {
            await _settingsService.SetAsync("DefaultDomain", DefaultDomain, _cts.Token);
            await _settingsService.SetAsync("DefaultWinRmAuthMethod", DefaultAuthMethod.ToString(), _cts.Token);
            await _settingsService.SetAsync("DefaultWinRmTransport", DefaultTransport.ToString(), _cts.Token);
            await _settingsService.SetAsync("StaleComputerThresholdDays", StaleComputerThresholdDays.ToString(System.Globalization.CultureInfo.InvariantCulture), _cts.Token);
            await _settingsService.SetAsync("DefaultThrottle", DefaultThrottle.ToString(System.Globalization.CultureInfo.InvariantCulture), _cts.Token);
            await _settingsService.SetAsync("DefaultTimeoutSeconds", DefaultTimeoutSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture), _cts.Token);
            await _settingsService.SetAsync("LogLevel", LogLevel, _cts.Token);
            await _settingsService.SetAsync("UpdateNetworkSharePath", UpdateSharePath, _cts.Token);

            if (HasUserScriptPathOverride)
            {
                await _settingsService.SetAsync("UserScriptRepositoryPath", UserScriptRepositoryPath, _cts.Token);
            }
            else
            {
                await _settingsService.SetAsync("UserScriptRepositoryPath", string.Empty, _cts.Token);
            }

            HasUnsavedChanges = false;
            _logger.Information("User settings saved");
            StatusMessage = "Settings saved.";
        }
        catch (OperationCanceledException)
        {
            // Cancelled
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to save settings");
            StatusMessage = "Failed to save settings.";
        }
    }

    /// <summary>
    /// Resets all fields to their loaded values.
    /// </summary>
    [RelayCommand]
    private async Task ResetSettingsAsync()
    {
        await LoadSettingsAsync();
        StatusMessage = "Settings reset to last saved values.";
    }

    /// <inheritdoc />
    public async Task RefreshAsync() => await LoadSettingsAsync();

    // Track unsaved changes for all user-editable properties
    partial void OnDefaultDomainChanged(string value) => MarkDirty();
    partial void OnDefaultAuthMethodChanged(WinRmAuthMethod value) => MarkDirty();
    partial void OnDefaultTransportChanged(WinRmTransport value) => MarkDirty();
    partial void OnStaleComputerThresholdDaysChanged(int value) => MarkDirty();
    partial void OnDefaultThrottleChanged(int value) => MarkDirty();
    partial void OnDefaultTimeoutSecondsChanged(int value) => MarkDirty();
    partial void OnLogLevelChanged(string value) => MarkDirty();
    partial void OnUpdateSharePathChanged(string value) => MarkDirty();
    partial void OnHasUserScriptPathOverrideChanged(bool value) => MarkDirty();
    partial void OnUserScriptRepositoryPathChanged(string value) => MarkDirty();

    private void MarkDirty()
    {
        if (!_isLoading)
        {
            HasUnsavedChanges = true;
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _cts.Cancel();
        _cts.Dispose();
        GC.SuppressFinalize(this);
    }
}
