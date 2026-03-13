using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using SysOpsCommander.Core.Constants;
using SysOpsCommander.Core.Extensions;
using SysOpsCommander.Core.Interfaces;
using SysOpsCommander.Core.Models;

namespace SysOpsCommander.ViewModels;

/// <summary>
/// Provides the main window ViewModel with sidebar navigation, status bar, and keyboard shortcut support.
/// </summary>
public partial class MainWindowViewModel : ObservableObject
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IActiveDirectoryService _adService;
    private readonly IDialogService _dialogService;
    private readonly IAutoUpdateService _autoUpdateService;
    private CancellationTokenSource? _globalCancellationSource;
    private bool _isInitializing;
    private bool _isNavigating;

    [ObservableProperty]
    private string _title = AppConstants.AppName;

    [ObservableProperty]
    private object? _currentView;

    partial void OnCurrentViewChanged(object? oldValue, object? newValue)
    {
        if (oldValue is IDisposable disposable)
        {
            try
            {
                disposable.Dispose();
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Error disposing previous view {ViewType}", oldValue.GetType().Name);
            }
        }

        if (newValue is IRefreshable refreshable)
        {
            refreshable.RefreshAsync().SafeFireAndForget();
        }
    }

    [ObservableProperty]
    private string _currentDomainName = string.Empty;

    [ObservableProperty]
    private string _currentUserName = string.Empty;

    [ObservableProperty]
    private string _connectionStatus = "Connected";

    [ObservableProperty]
    private bool _isUpdateAvailable;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string _busyMessage = string.Empty;

    [ObservableProperty]
    private ObservableCollection<DomainConnection> _availableDomains = [];

    [ObservableProperty]
    private DomainConnection? _selectedDomain;

    /// <summary>
    /// Initializes a new instance of the <see cref="MainWindowViewModel"/> class.
    /// </summary>
    /// <param name="serviceProvider">The service provider for resolving child ViewModels.</param>
    /// <param name="adService">The Active Directory service.</param>
    /// <param name="dialogService">The dialog service for MVVM-safe dialog interactions.</param>
    /// <param name="autoUpdateService">The auto-update service for checking for new versions.</param>
    public MainWindowViewModel(
        IServiceProvider serviceProvider,
        IActiveDirectoryService adService,
        IDialogService dialogService,
        IAutoUpdateService autoUpdateService)
    {
        ArgumentNullException.ThrowIfNull(serviceProvider);
        ArgumentNullException.ThrowIfNull(adService);
        ArgumentNullException.ThrowIfNull(dialogService);
        ArgumentNullException.ThrowIfNull(autoUpdateService);

        _serviceProvider = serviceProvider;
        _adService = adService;
        _dialogService = dialogService;
        _autoUpdateService = autoUpdateService;
    }

    /// <summary>
    /// Initializes the ViewModel by detecting the current user/domain and navigating to the dashboard.
    /// </summary>
    /// <returns>A task representing the asynchronous initialization.</returns>
    public async Task InitializeAsync()
    {
        CurrentUserName = Environment.UserName;

        // Use Environment.UserDomainName as an instant fallback — Domain.GetCurrentDomain() can hang
        CurrentDomainName = Environment.UserDomainName;
        ConnectionStatus = "Connecting";

        try
        {
            using CancellationTokenSource domainCts = new(TimeSpan.FromSeconds(10));
            DomainConnection activeDomain = await Task.Run(
                _adService.GetActiveDomain, domainCts.Token).ConfigureAwait(false);
            CurrentDomainName = activeDomain.DomainName;
            ConnectionStatus = "Connected";
        }
        catch (OperationCanceledException)
        {
            Log.Warning("AD domain detection timed out — using Environment.UserDomainName fallback");
            ConnectionStatus = "Disconnected";
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Could not detect active AD domain on startup");
            ConnectionStatus = "Disconnected";
        }

        NavigateToDashboard();
        await LoadAvailableDomainsAsync();

        CheckForUpdatesInBackgroundAsync().SafeFireAndForget();
    }

    /// <summary>
    /// Navigates to the specified view by name.
    /// </summary>
    /// <param name="viewName">The name of the view to navigate to.</param>
    [RelayCommand]
    private void Navigate(string? viewName)
    {
        if (_isNavigating)
        {
            Log.Debug("Navigation to {ViewName} skipped — already navigating", viewName);
            return;
        }

        _isNavigating = true;
        Log.Debug("Navigating to {ViewName}", viewName);
        try
        {
            switch (viewName)
            {
                case "Dashboard":
                    NavigateToDashboard();
                    break;
                case "ADExplorer":
                    NavigateToAdExplorer();
                    break;
                case "Execution":
                    NavigateToExecution();
                    break;
                case "ScriptLibrary":
                    NavigateToScriptLibrary();
                    break;
                case "AuditLog":
                    NavigateToAuditLog();
                    break;
                case "Settings":
                    NavigateToSettings();
                    break;
                default:
                    break;
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Navigation to {ViewName} failed", viewName);
        }
        finally
        {
            _isNavigating = false;
        }
    }

    /// <summary>
    /// Navigates to the Dashboard view.
    /// </summary>
    [RelayCommand]
    private void NavigateToDashboard() =>
        CurrentView = _serviceProvider.GetRequiredService<DashboardViewModel>();

    /// <summary>
    /// Navigates to the AD Explorer view.
    /// </summary>
    [RelayCommand]
    private void NavigateToAdExplorer() =>
        CurrentView = _serviceProvider.GetRequiredService<AdExplorerViewModel>();

    /// <summary>
    /// Navigates to the Execution view.
    /// </summary>
    [RelayCommand]
    private void NavigateToExecution() =>
        CurrentView = _serviceProvider.GetRequiredService<ExecutionViewModel>();

    /// <summary>
    /// Navigates to the Script Library view.
    /// </summary>
    [RelayCommand]
    private void NavigateToScriptLibrary() =>
        CurrentView = _serviceProvider.GetRequiredService<ScriptLibraryViewModel>();

    /// <summary>
    /// Navigates to the Audit Log view.
    /// </summary>
    [RelayCommand]
    private void NavigateToAuditLog() =>
        CurrentView = _serviceProvider.GetRequiredService<AuditLogViewModel>();

    /// <summary>
    /// Navigates to the Settings view.
    /// </summary>
    [RelayCommand]
    private void NavigateToSettings() =>
        CurrentView = _serviceProvider.GetRequiredService<SettingsViewModel>();

    /// <summary>
    /// Placeholder for focus-search event. Views subscribe to handle focus transfer.
    /// </summary>
    [RelayCommand]
    private static void FocusSearch()
    {
        // View subscribes to a messenger event to handle focus transfer to search box
    }

    /// <summary>
    /// Opens the domain selector dialog to manually enter a domain.
    /// </summary>
    [RelayCommand]
    private async Task OpenDomainSelectorAsync()
    {
        DomainConnection? domain = await _dialogService.ShowDomainSelectorAsync();
        if (domain is not null)
        {
            await SwitchDomainAsync(domain);
        }
    }

    /// <summary>
    /// Refreshes the current view if it supports <see cref="IRefreshable"/>.
    /// </summary>
    [RelayCommand]
    private async Task RefreshCurrentViewAsync()
    {
        if (CurrentView is IRefreshable refreshable)
        {
            await refreshable.RefreshAsync();
        }
    }

    /// <summary>
    /// Cancels the current global operation.
    /// </summary>
    [RelayCommand]
    private void CancelCurrentOperation()
    {
        _globalCancellationSource?.Cancel();
        _globalCancellationSource = null;
    }

    partial void OnSelectedDomainChanged(DomainConnection? value)
    {
        if (value is not null && !_isInitializing)
        {
            SwitchDomainAsync(value).SafeFireAndForget();
        }
    }

    private async Task SwitchDomainAsync(DomainConnection domain)
    {
        IsBusy = true;
        BusyMessage = $"Connecting to {domain.DomainName}...";
        ConnectionStatus = "Connecting";

        DomainConnection previousDomain = _adService.GetActiveDomain();

        try
        {
            await _adService.SetActiveDomainAsync(domain, CancellationToken.None);
            CurrentDomainName = domain.DomainName;
            ConnectionStatus = "Connected";
            Log.Information("Switched active domain to {DomainName}", domain.DomainName);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to switch domain to {DomainName}", domain.DomainName);

            // Restore previous domain in the UI
            _isInitializing = true;
            SelectedDomain = previousDomain;
            CurrentDomainName = previousDomain.DomainName;
            _isInitializing = false;

            ConnectionStatus = "Connected";

            _dialogService.ShowError("Domain Switch Failed", $"Could not connect to {domain.DomainName}: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
            BusyMessage = string.Empty;
        }
    }

    private async Task LoadAvailableDomainsAsync()
    {
        try
        {
            using CancellationTokenSource timeoutCts = new(TimeSpan.FromSeconds(10));
            IReadOnlyList<DomainConnection> domains = await _adService.GetAvailableDomainsAsync(timeoutCts.Token);
            AvailableDomains = new ObservableCollection<DomainConnection>(domains);

            _isInitializing = true;
            DomainConnection? current = null;
            foreach (DomainConnection d in domains)
            {
                if (d.IsCurrentDomain)
                {
                    current = d;
                    break;
                }
            }

            SelectedDomain = current ?? (domains.Count > 0 ? domains[0] : null);
            _isInitializing = false;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Could not load available domains");
        }
    }

    private async Task CheckForUpdatesInBackgroundAsync()
    {
        try
        {
            UpdateCheckResult result = await _autoUpdateService.CheckForUpdateAsync(CancellationToken.None);
            if (result.IsUpdateAvailable)
            {
                IsUpdateAvailable = true;
                Log.Information("Update available: {Version}", result.LatestVersion);
            }
        }
        catch
        {
            // Update check failure is never blocking
        }
    }
}
