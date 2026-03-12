using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using SysOpsCommander.Core.Constants;
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
    private CancellationTokenSource? _globalCancellationSource;

    [ObservableProperty]
    private string _title = AppConstants.AppName;

    [ObservableProperty]
    private object? _currentView;

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
    public MainWindowViewModel(
        IServiceProvider serviceProvider,
        IActiveDirectoryService adService,
        IDialogService dialogService)
    {
        ArgumentNullException.ThrowIfNull(serviceProvider);
        ArgumentNullException.ThrowIfNull(adService);
        ArgumentNullException.ThrowIfNull(dialogService);

        _serviceProvider = serviceProvider;
        _adService = adService;
        _dialogService = dialogService;
    }

    /// <summary>
    /// Initializes the ViewModel by detecting the current user/domain and navigating to the dashboard.
    /// </summary>
    /// <returns>A task representing the asynchronous initialization.</returns>
    public async Task InitializeAsync()
    {
        CurrentUserName = Environment.UserName;

        try
        {
            DomainConnection activeDomain = _adService.GetActiveDomain();
            CurrentDomainName = activeDomain.DomainName;
            ConnectionStatus = "Connected";
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Could not detect active AD domain on startup");
            CurrentDomainName = "Unknown";
            ConnectionStatus = "Disconnected";
        }

        await LoadAvailableDomainsAsync();
        NavigateToDashboard();
    }

    /// <summary>
    /// Navigates to the specified view by name.
    /// </summary>
    /// <param name="viewName">The name of the view to navigate to.</param>
    [RelayCommand]
    private void Navigate(string? viewName)
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
        if (value is not null)
        {
            _ = SwitchDomainAsync(value);
        }
    }

    private async Task SwitchDomainAsync(DomainConnection domain)
    {
        IsBusy = true;
        BusyMessage = $"Connecting to {domain.DomainName}...";
        ConnectionStatus = "Connecting";

        try
        {
            await _adService.SetActiveDomainAsync(domain, CancellationToken.None);
            CurrentDomainName = domain.DomainName;
            ConnectionStatus = "Connected";
            Log.Information("Switched active domain to {DomainName}", domain.DomainName);
        }
        catch (Exception ex)
        {
            ConnectionStatus = "Disconnected";
            Log.Error(ex, "Failed to switch domain to {DomainName}", domain.DomainName);
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
            IReadOnlyList<DomainConnection> domains = await _adService.GetAvailableDomainsAsync(CancellationToken.None);
            AvailableDomains = new ObservableCollection<DomainConnection>(domains);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Could not load available domains");
        }
    }
}
