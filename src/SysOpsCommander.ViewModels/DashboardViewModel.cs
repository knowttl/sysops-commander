using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Serilog;
using SysOpsCommander.Core.Interfaces;
using SysOpsCommander.Core.Models;
using SysOpsCommander.Core.Validation;

namespace SysOpsCommander.ViewModels;

/// <summary>
/// ViewModel for the Dashboard view. Provides Quick Connect, active domain info, and recent execution history.
/// </summary>
public partial class DashboardViewModel : ObservableObject, IRefreshable, IDisposable
{
    private readonly IActiveDirectoryService _adService;
    private readonly IHostTargetingService _hostTargetingService;
    private readonly IAuditLogService _auditLogService;
    private readonly ILogger _logger;
    private readonly CancellationTokenSource _cts = new();

    [ObservableProperty]
    private string _quickConnectHostname = string.Empty;

    [ObservableProperty]
    private string _quickConnectResult = string.Empty;

    [ObservableProperty]
    private ObservableCollection<AuditLogEntry> _recentExecutions = [];

    [ObservableProperty]
    private string _activeDomainName = string.Empty;

    [ObservableProperty]
    private string _currentUserName = Environment.UserName;

    /// <summary>
    /// Initializes a new instance of the <see cref="DashboardViewModel"/> class.
    /// </summary>
    /// <param name="adService">The Active Directory service.</param>
    /// <param name="hostTargetingService">The shared host targeting service.</param>
    /// <param name="auditLogService">The audit log service for recent execution history.</param>
    /// <param name="logger">The Serilog logger.</param>
    public DashboardViewModel(
        IActiveDirectoryService adService,
        IHostTargetingService hostTargetingService,
        IAuditLogService auditLogService,
        ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(adService);
        ArgumentNullException.ThrowIfNull(hostTargetingService);
        ArgumentNullException.ThrowIfNull(auditLogService);
        ArgumentNullException.ThrowIfNull(logger);

        _adService = adService;
        _hostTargetingService = hostTargetingService;
        _auditLogService = auditLogService;
        _logger = logger;
    }

    /// <summary>
    /// Refreshes the dashboard data (domain info and recent executions).
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task RefreshAsync()
    {
        LoadActiveDomain();
        await LoadRecentExecutionsAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Validates and sends the Quick Connect hostname to the execution target list.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [RelayCommand]
    private Task QuickConnectAsync()
    {
        if (string.IsNullOrWhiteSpace(QuickConnectHostname))
        {
            QuickConnectResult = "Please enter a hostname.";
            return Task.CompletedTask;
        }

        string trimmed = QuickConnectHostname.Trim();
        ValidationResult validation = HostnameValidator.Validate(trimmed);

        if (!validation.IsValid)
        {
            QuickConnectResult = $"Invalid hostname: {validation.ErrorMessage}";
            return Task.CompletedTask;
        }

        _hostTargetingService.AddFromHostnames([trimmed]);
        QuickConnectResult = $"Added '{trimmed}' to Execution Targets.";
        _logger.Information("Quick Connect added host {Hostname} to execution targets", trimmed);
        QuickConnectHostname = string.Empty;

        return Task.CompletedTask;
    }

    /// <summary>
    /// Loads the most recent execution entries from the audit log.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [RelayCommand]
    private async Task LoadRecentExecutionsAsync()
    {
        try
        {
            var filter = new AuditLogFilter { PageNumber = 1, PageSize = 5 };
            IReadOnlyList<AuditLogEntry> entries = await _auditLogService
                .QueryAsync(filter, _cts.Token).ConfigureAwait(false);

            RecentExecutions.Clear();
            foreach (AuditLogEntry entry in entries)
            {
                RecentExecutions.Add(entry);
            }
        }
        catch (OperationCanceledException)
        {
            // Cancelled — no action needed
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to load recent executions");
        }
    }

    private void LoadActiveDomain()
    {
        try
        {
            DomainConnection domain = _adService.GetActiveDomain();
            ActiveDomainName = domain.DomainName;
        }
        catch (Exception ex)
        {
            _logger.Warning(ex, "Failed to load active domain for dashboard");
            ActiveDomainName = "No domain connected";
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _cts.Dispose();
        GC.SuppressFinalize(this);
    }
}
