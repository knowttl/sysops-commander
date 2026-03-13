using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Serilog;
using SysOpsCommander.Core.Constants;
using SysOpsCommander.Core.Enums;
using SysOpsCommander.Core.Interfaces;
using SysOpsCommander.Core.Models;

namespace SysOpsCommander.ViewModels;

/// <summary>
/// ViewModel for the Audit Log view. Displays historical execution records with filtering.
/// </summary>
public partial class AuditLogViewModel : ObservableObject, IRefreshable, IDisposable
{
    private readonly IAuditLogService _auditLogService;
    private readonly IExportService _exportService;
    private readonly IDialogService _dialogService;
    private readonly ILogger _logger;
    private readonly CancellationTokenSource _cts = new();

    // --- Entries ---

    [ObservableProperty]
    private ObservableCollection<AuditLogEntry> _auditEntries = [];

    [ObservableProperty]
    private AuditLogEntry? _selectedEntry;

    [ObservableProperty]
    private string _selectedEntryDetail = string.Empty;

    // --- Filters ---

    [ObservableProperty]
    private DateTime? _filterStartDate;

    [ObservableProperty]
    private DateTime? _filterEndDate;

    [ObservableProperty]
    private string _filterScriptName = string.Empty;

    [ObservableProperty]
    private string _filterHostname = string.Empty;

    [ObservableProperty]
    private string _filterDomain = string.Empty;

    [ObservableProperty]
    private WinRmAuthMethod? _filterAuthMethod;

    [ObservableProperty]
    private string _filterUserName = string.Empty;

    // --- Pagination ---

    [ObservableProperty]
    private int _totalEntries;

    [ObservableProperty]
    private int _currentPage = 1;

    [ObservableProperty]
    private int _totalPages = 1;

    // --- Status ---

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    /// <summary>
    /// Initializes a new instance of the <see cref="AuditLogViewModel"/> class.
    /// </summary>
    public AuditLogViewModel(
        IAuditLogService auditLogService,
        IExportService exportService,
        IDialogService dialogService,
        ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(auditLogService);
        ArgumentNullException.ThrowIfNull(exportService);
        ArgumentNullException.ThrowIfNull(dialogService);
        ArgumentNullException.ThrowIfNull(logger);

        _auditLogService = auditLogService;
        _exportService = exportService;
        _dialogService = dialogService;
        _logger = logger;
    }

    /// <summary>
    /// Loads audit log entries with the current filter criteria.
    /// </summary>
    [RelayCommand]
    private async Task LoadAuditLogAsync()
    {
        try
        {
            AuditLogFilter filter = new()
            {
                StartDate = FilterStartDate,
                EndDate = FilterEndDate,
                ScriptName = string.IsNullOrWhiteSpace(FilterScriptName) ? null : FilterScriptName,
                Hostname = string.IsNullOrWhiteSpace(FilterHostname) ? null : FilterHostname,
                TargetDomain = string.IsNullOrWhiteSpace(FilterDomain) ? null : FilterDomain,
                AuthMethod = FilterAuthMethod,
                UserName = string.IsNullOrWhiteSpace(FilterUserName) ? null : FilterUserName,
                PageNumber = CurrentPage,
                PageSize = AppConstants.MaxResultsPerPage
            };

            IReadOnlyList<AuditLogEntry> result = await _auditLogService.QueryAsync(filter, _cts.Token);
            AuditEntries = new ObservableCollection<AuditLogEntry>(result);
            TotalEntries = result.Count;
            TotalPages = Math.Max(1, (int)Math.Ceiling((double)TotalEntries / AppConstants.MaxResultsPerPage));
            StatusMessage = $"{TotalEntries} entries found.";
        }
        catch (OperationCanceledException)
        {
            // Cancelled — no action needed
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to load audit log");
            StatusMessage = "Failed to load audit log.";
        }
    }

    /// <summary>
    /// Clears all filter values and reloads the audit log.
    /// </summary>
    [RelayCommand]
    private async Task ClearFiltersAsync()
    {
        FilterStartDate = null;
        FilterEndDate = null;
        FilterScriptName = string.Empty;
        FilterHostname = string.Empty;
        FilterDomain = string.Empty;
        FilterAuthMethod = null;
        FilterUserName = string.Empty;
        CurrentPage = 1;
        await LoadAuditLogAsync();
    }

    /// <summary>
    /// Navigates to the next page of results.
    /// </summary>
    [RelayCommand]
    private async Task NextPageAsync()
    {
        if (CurrentPage < TotalPages)
        {
            CurrentPage++;
            await LoadAuditLogAsync();
        }
    }

    /// <summary>
    /// Navigates to the previous page of results.
    /// </summary>
    [RelayCommand]
    private async Task PreviousPageAsync()
    {
        if (CurrentPage > 1)
        {
            CurrentPage--;
            await LoadAuditLogAsync();
        }
    }

    /// <summary>
    /// Exports the current audit log entries to a CSV file.
    /// </summary>
    [RelayCommand]
    private async Task ExportAuditLogAsync()
    {
        try
        {
            string? filePath = await _dialogService.ShowSaveFileDialogAsync(
                ".csv",
                "CSV files (*.csv)|*.csv|Excel files (*.xlsx)|*.xlsx");

            if (filePath is null)
            {
                return;
            }

            if (filePath.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
            {
                await _exportService.ExportAuditLogToExcelAsync(AuditEntries, filePath, _cts.Token);
            }
            else
            {
                await _exportService.ExportAuditLogToCsvAsync(AuditEntries, filePath, _cts.Token);
            }

            _logger.Information("Exported audit log to {FilePath}", filePath);
            StatusMessage = $"Exported {AuditEntries.Count} entries to {Path.GetFileName(filePath)}.";
        }
        catch (OperationCanceledException)
        {
            // Cancelled — no action needed
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to export audit log");
            StatusMessage = "Export failed.";
        }
    }

    /// <summary>
    /// Purges audit log entries older than the configured retention period after confirmation.
    /// </summary>
    [RelayCommand]
    private async Task PurgeOldEntriesAsync()
    {
        try
        {
            bool confirmed = await _dialogService.ShowConfirmationAsync(
                "Purge Old Entries",
                $"Delete audit log entries older than {AppConstants.AuditLogRetentionDays} days? This cannot be undone.");

            if (!confirmed)
            {
                return;
            }

            int deleted = await _auditLogService.PurgeOldEntriesAsync(AppConstants.AuditLogRetentionDays, _cts.Token);
            await LoadAuditLogAsync();
            _logger.Information("Purged {Count} audit log entries older than {Days} days", deleted, AppConstants.AuditLogRetentionDays);
            StatusMessage = $"Purged {deleted} old entries.";
        }
        catch (OperationCanceledException)
        {
            // Cancelled — no action needed
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to purge audit log entries");
            StatusMessage = "Purge failed.";
        }
    }

    /// <inheritdoc />
    public async Task RefreshAsync() => await LoadAuditLogAsync();

    partial void OnSelectedEntryChanged(AuditLogEntry? value)
    {
        if (value is null)
        {
            SelectedEntryDetail = string.Empty;
            return;
        }

        SelectedEntryDetail = FormatEntryDetail(value);
    }

    private static string FormatEntryDetail(AuditLogEntry entry)
    {
        string hosts = string.IsNullOrWhiteSpace(entry.TargetHosts)
            ? "None"
            : entry.TargetHosts.Replace(",", ", ");

        return $"""
            Script: {entry.ScriptName}
            Status: {entry.Status}
            Targets: {hosts}
            Results: {entry.SuccessCount}/{entry.TargetHostCount} succeeded, {entry.FailureCount} failed
            Duration: {entry.Duration.TotalSeconds:F1}s
            Auth: {entry.AuthMethod?.ToString() ?? "N/A"} / {entry.Transport?.ToString() ?? "N/A"}
            Domain: {entry.TargetDomain ?? "N/A"}
            User: {entry.UserName} on {entry.MachineName}
            Time: {entry.Timestamp:yyyy-MM-dd HH:mm:ss} UTC
            """;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _cts.Cancel();
        _cts.Dispose();
        GC.SuppressFinalize(this);
    }
}
