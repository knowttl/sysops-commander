using System.Collections.ObjectModel;
using System.Management.Automation;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Serilog;
using SysOpsCommander.Core.Constants;
using SysOpsCommander.Core.Enums;
using SysOpsCommander.Core.Interfaces;
using SysOpsCommander.Core.Models;
using SysOpsCommander.Core.Validation;

namespace SysOpsCommander.ViewModels;

/// <summary>
/// ViewModel for the Execution view. Manages remote PowerShell execution across target hosts.
/// </summary>
public partial class ExecutionViewModel : ObservableObject, IRefreshable, IDisposable
{
    private readonly IRemoteExecutionService _executionService;
    private readonly IHostTargetingService _hostTargetingService;
    private readonly IScriptLoaderService _scriptLoaderService;
    private readonly IExportService _exportService;
    private readonly IDialogService _dialogService;
    private readonly ISettingsService _settingsService;
    private readonly ILogger _logger;
    private readonly CancellationTokenSource _cts = new();

    private ExecutionJob? _currentJob;
    private PSCredential? _currentCredential;

    // --- Script Selection ---

    [ObservableProperty]
    private ObservableCollection<ScriptPlugin> _availableScripts = [];

    [ObservableProperty]
    private ScriptPlugin? _selectedScript;

    [ObservableProperty]
    private bool _isAdHocMode;

    [ObservableProperty]
    private string _adHocScriptContent = string.Empty;

    [ObservableProperty]
    private ObservableCollection<ParameterEntry> _scriptParameters = [];

    [ObservableProperty]
    private string _scriptDangerWarning = string.Empty;

    // --- WinRM Configuration ---

    [ObservableProperty]
    private WinRmAuthMethod _selectedAuthMethod = WinRmAuthMethod.Kerberos;

    [ObservableProperty]
    private WinRmTransport _selectedTransport = WinRmTransport.HTTP;

    [ObservableProperty]
    private string _customPort = string.Empty;

    [ObservableProperty]
    private int _throttleLimit = AppConstants.DefaultThrottle;

    [ObservableProperty]
    private int _timeoutSeconds = AppConstants.DefaultWinRmTimeoutSeconds;

    [ObservableProperty]
    private string _credSspWarning = string.Empty;

    // --- Target Management ---

    [ObservableProperty]
    private string _newHostname = string.Empty;

    /// <summary>
    /// Gets the shared observable collection of target hosts.
    /// </summary>
    public ObservableCollection<HostTarget> Targets => _hostTargetingService.Targets;

    // --- Execution State ---

    [ObservableProperty]
    private bool _isExecuting;

    [ObservableProperty]
    private string _executionProgress = string.Empty;

    [ObservableProperty]
    private int _completedHostCount;

    [ObservableProperty]
    private int _totalHostCount;

    // --- Results ---

    [ObservableProperty]
    private ObservableCollection<HostResult> _results = [];

    [ObservableProperty]
    private HostResult? _selectedResult;

    [ObservableProperty]
    private string _resultDetailOutput = string.Empty;

    [ObservableProperty]
    private string _resultErrors = string.Empty;

    [ObservableProperty]
    private string _resultWarnings = string.Empty;

    // --- Status ---

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    /// <summary>
    /// Initializes a new instance of the <see cref="ExecutionViewModel"/> class.
    /// </summary>
    public ExecutionViewModel(
        IRemoteExecutionService executionService,
        IHostTargetingService hostTargetingService,
        IScriptLoaderService scriptLoaderService,
        IExportService exportService,
        IDialogService dialogService,
        ISettingsService settingsService,
        ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(executionService);
        ArgumentNullException.ThrowIfNull(hostTargetingService);
        ArgumentNullException.ThrowIfNull(scriptLoaderService);
        ArgumentNullException.ThrowIfNull(exportService);
        ArgumentNullException.ThrowIfNull(dialogService);
        ArgumentNullException.ThrowIfNull(settingsService);
        ArgumentNullException.ThrowIfNull(logger);

        _executionService = executionService;
        _hostTargetingService = hostTargetingService;
        _scriptLoaderService = scriptLoaderService;
        _exportService = exportService;
        _dialogService = dialogService;
        _settingsService = settingsService;
        _logger = logger;

        _scriptLoaderService.LibraryChanged += OnLibraryChanged;
    }

    /// <summary>
    /// Loads scripts and WinRM defaults from settings.
    /// </summary>
    [RelayCommand]
    private async Task InitializeAsync()
    {
        try
        {
            string authMethod = await _settingsService.GetEffectiveAsync("DefaultWinRmAuthMethod", _cts.Token);
            if (Enum.TryParse<WinRmAuthMethod>(authMethod, ignoreCase: true, out WinRmAuthMethod auth))
            {
                SelectedAuthMethod = auth;
            }

            string transport = await _settingsService.GetEffectiveAsync("DefaultWinRmTransport", _cts.Token);
            if (Enum.TryParse<WinRmTransport>(transport, ignoreCase: true, out WinRmTransport trans))
            {
                SelectedTransport = trans;
            }

            IReadOnlyList<ScriptPlugin> scripts = await _scriptLoaderService.LoadAllScriptsAsync(_cts.Token);
            AvailableScripts = new ObservableCollection<ScriptPlugin>(scripts);

            StatusMessage = $"Loaded {scripts.Count} scripts.";
        }
        catch (OperationCanceledException)
        {
            // Cancelled — no action needed
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to initialize execution view");
            StatusMessage = "Failed to load scripts.";
        }
    }

    /// <inheritdoc />
    public async Task RefreshAsync() =>
        await InitializeAsync();

    // --- Partial property change handlers ---

    partial void OnSelectedAuthMethodChanged(WinRmAuthMethod value)
    {
        CredSspWarning = value == WinRmAuthMethod.CredSSP
            ? "CredSSP requires explicit credentials and must be enabled on both client and server."
            : string.Empty;
    }

    partial void OnSelectedScriptChanged(ScriptPlugin? value)
    {
        ScriptParameters.Clear();
        ScriptDangerWarning = string.Empty;

        if (value is null)
        {
            return;
        }

        if (value.EffectiveDangerLevel >= ScriptDangerLevel.Caution)
        {
            ScriptDangerWarning = value.EffectiveDangerLevel == ScriptDangerLevel.Destructive
                ? "WARNING: This script has DESTRUCTIVE patterns. Review carefully before executing."
                : "CAUTION: This script contains patterns that warrant review.";
        }

        if (value.Manifest?.Parameters is { Count: > 0 } parameters)
        {
            foreach (ScriptParameter param in parameters)
            {
                ScriptParameters.Add(new ParameterEntry
                {
                    Name = param.Name,
                    DisplayName = string.IsNullOrEmpty(param.DisplayName) ? param.Name : param.DisplayName,
                    Description = param.Description,
                    ParameterType = param.Type,
                    IsRequired = param.Required,
                    Choices = param.Choices,
                    Value = param.DefaultValue?.ToString() ?? string.Empty
                });
            }
        }
    }

    partial void OnSelectedResultChanged(HostResult? value)
    {
        if (value is null)
        {
            ResultDetailOutput = string.Empty;
            ResultErrors = string.Empty;
            ResultWarnings = string.Empty;
            return;
        }

        ResultDetailOutput = value.IsFileReference
            ? $"[Large output saved to file]\n{value.Output}"
            : value.Output;
        ResultErrors = value.ErrorOutput ?? string.Empty;
        ResultWarnings = value.WarningOutput ?? string.Empty;
    }

    // --- Script mode ---

    /// <summary>
    /// Toggles between script library selection and ad-hoc script entry.
    /// </summary>
    [RelayCommand]
    private void ToggleAdHocMode()
    {
        IsAdHocMode = !IsAdHocMode;
        if (IsAdHocMode)
        {
            SelectedScript = null;
        }
    }

    // --- Target Management Commands ---

    /// <summary>
    /// Adds a hostname to the target list.
    /// </summary>
    [RelayCommand]
    private void AddHost()
    {
        if (string.IsNullOrWhiteSpace(NewHostname))
        {
            StatusMessage = "Please enter a hostname.";
            return;
        }

        string trimmed = NewHostname.Trim();
        ValidationResult validation = HostnameValidator.Validate(trimmed);
        if (!validation.IsValid)
        {
            StatusMessage = $"Invalid hostname: {validation.ErrorMessage}";
            return;
        }

        _hostTargetingService.AddFromHostnames([trimmed]);
        StatusMessage = $"Added '{trimmed}' to targets.";
        NewHostname = string.Empty;
    }

    /// <summary>
    /// Imports target hosts from a CSV file.
    /// </summary>
    [RelayCommand]
    private async Task ImportCsvAsync()
    {
        string? filePath = await _dialogService.ShowOpenFileDialogAsync("CSV files|*.csv|Text files|*.txt");
        if (filePath is null)
        {
            return;
        }

        try
        {
            await _hostTargetingService.AddFromCsvFileAsync(filePath, _cts.Token);
            StatusMessage = $"Imported hosts from {System.IO.Path.GetFileName(filePath)}.";
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to import CSV file {FilePath}", filePath);
            StatusMessage = "Failed to import CSV file.";
        }
    }

    /// <summary>
    /// Checks reachability of all pending targets.
    /// </summary>
    [RelayCommand]
    private async Task CheckReachabilityAsync()
    {
        if (Targets.Count == 0)
        {
            StatusMessage = "No targets to check.";
            return;
        }

        try
        {
            StatusMessage = "Checking reachability...";
            WinRmConnectionOptions options = BuildConnectionOptions();
            await _hostTargetingService.CheckReachabilityAsync(options, _cts.Token);
            StatusMessage = "Reachability check complete.";
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Reachability check cancelled.";
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Reachability check failed");
            StatusMessage = "Reachability check failed.";
        }
    }

    /// <summary>
    /// Removes a specific host from the target list.
    /// </summary>
    /// <param name="hostname">The hostname to remove.</param>
    [RelayCommand]
    private void RemoveHost(string hostname) =>
        _hostTargetingService.RemoveTarget(hostname);

    /// <summary>
    /// Clears all hosts from the target list.
    /// </summary>
    [RelayCommand]
    private void ClearHosts()
    {
        _hostTargetingService.ClearTargets();
        StatusMessage = "Targets cleared.";
    }

    // --- Execution ---

    /// <summary>
    /// Executes the selected or ad-hoc script across all target hosts.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanExecute))]
    private async Task ExecuteAsync()
    {
        if (Targets.Count == 0)
        {
            StatusMessage = "Add at least one target host before executing.";
            return;
        }

        string? scriptContent = GetScriptContent();
        if (scriptContent is null)
        {
            StatusMessage = "Select a script or enter ad-hoc script content.";
            return;
        }

        string scriptName = IsAdHocMode
            ? "Ad-Hoc Script"
            : SelectedScript?.Manifest?.Name ?? SelectedScript?.FileName ?? "Unknown";

        // CredSSP requires explicit credentials
        if (SelectedAuthMethod == WinRmAuthMethod.CredSSP)
        {
            CredentialDialogResult? credResult = await _dialogService.ShowCredentialDialogAsync(
                null, WinRmAuthMethod.CredSSP);

            if (credResult is null)
            {
                StatusMessage = "Execution cancelled — credentials required for CredSSP.";
                return;
            }

            _currentCredential = new PSCredential(
                $"{credResult.Domain}\\{credResult.Username}",
                credResult.Password);
        }

        try
        {
            IsExecuting = true;
            Results.Clear();
            CompletedHostCount = 0;
            TotalHostCount = Targets.Count;
            StatusMessage = $"Executing '{scriptName}' on {TotalHostCount} host(s)...";

            _currentJob = new ExecutionJob
            {
                ScriptName = scriptName,
                ScriptContent = scriptContent,
                Parameters = BuildParameterDictionary(),
                TargetHosts = [.. Targets],
                ExecutionType = ExecutionType.PowerShell,
                WinRmConnectionOptions = BuildConnectionOptions(),
                Credential = _currentCredential,
                ThrottleLimit = ThrottleLimit,
                TimeoutSeconds = TimeoutSeconds
            };

            var progress = new Progress<HostResult>(OnHostResultReceived);
            ExecutionJob completedJob = await _executionService.ExecuteAsync(
                _currentJob, progress, _cts.Token);

            string status = completedJob.Status switch
            {
                ExecutionStatus.Completed => "Completed successfully",
                ExecutionStatus.PartialFailure => "Completed with some failures",
                ExecutionStatus.Failed => "Failed",
                ExecutionStatus.Cancelled => "Cancelled",
                ExecutionStatus.Pending => completedJob.Status.ToString(),
                ExecutionStatus.Validating => completedJob.Status.ToString(),
                ExecutionStatus.Running => completedJob.Status.ToString(),
                _ => completedJob.Status.ToString(),
            };

            StatusMessage = $"Execution {status}. {Results.Count(r => r.Status == HostStatus.Success)} succeeded, " +
                            $"{Results.Count(r => r.Status == HostStatus.Failed)} failed.";
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Execution cancelled.";
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Execution failed for script {ScriptName}", scriptName);
            StatusMessage = $"Execution failed: {ex.Message}";
        }
        finally
        {
            IsExecuting = false;
            _currentJob = null;

            if (_currentCredential is not null)
            {
                _currentCredential.Password.Dispose();
                _currentCredential = null;
            }
        }
    }

    private bool CanExecute() => !IsExecuting;

    /// <summary>
    /// Cancels the currently running execution.
    /// </summary>
    [RelayCommand]
    private async Task CancelExecutionAsync()
    {
        if (_currentJob is null)
        {
            return;
        }

        try
        {
            await _executionService.CancelExecutionAsync(_currentJob.Id);
            StatusMessage = "Cancellation requested...";
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to cancel execution");
        }
    }

    // --- Export ---

    /// <summary>
    /// Exports execution results to a CSV file.
    /// </summary>
    [RelayCommand]
    private async Task ExportToCsvAsync()
    {
        if (Results.Count == 0)
        {
            StatusMessage = "No results to export.";
            return;
        }

        string? filePath = await _dialogService.ShowSaveFileDialogAsync(".csv", "CSV files|*.csv");
        if (filePath is null)
        {
            return;
        }

        try
        {
            await _exportService.ExportToCsvAsync(Results, filePath, _cts.Token);
            StatusMessage = $"Results exported to {System.IO.Path.GetFileName(filePath)}.";
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "CSV export failed");
            StatusMessage = "Export failed.";
        }
    }

    /// <summary>
    /// Exports execution results to an Excel file.
    /// </summary>
    [RelayCommand]
    private async Task ExportToExcelAsync()
    {
        if (Results.Count == 0)
        {
            StatusMessage = "No results to export.";
            return;
        }

        string? filePath = await _dialogService.ShowSaveFileDialogAsync(".xlsx", "Excel files|*.xlsx");
        if (filePath is null)
        {
            return;
        }

        try
        {
            await _exportService.ExportToExcelAsync(Results, filePath, _cts.Token);
            StatusMessage = $"Results exported to {System.IO.Path.GetFileName(filePath)}.";
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Excel export failed");
            StatusMessage = "Export failed.";
        }
    }

    // --- Private Helpers ---

    private void OnHostResultReceived(HostResult result)
    {
        Results.Add(result);
        CompletedHostCount = Results.Count;
        ExecutionProgress = $"{CompletedHostCount} / {TotalHostCount}";
    }

    private string? GetScriptContent() =>
        IsAdHocMode
            ? (string.IsNullOrWhiteSpace(AdHocScriptContent) ? null : AdHocScriptContent)
            : SelectedScript?.Content;

    private Dictionary<string, object>? BuildParameterDictionary()
    {
        if (ScriptParameters.Count == 0)
        {
            return null;
        }

        var parameters = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

        foreach (ParameterEntry entry in ScriptParameters)
        {
            if (string.IsNullOrEmpty(entry.Value) && !entry.IsRequired)
            {
                continue;
            }

            object value = entry.ParameterType switch
            {
                "bool" => bool.TryParse(entry.Value, out bool b) && b,
                "int" => int.TryParse(entry.Value, out int i) ? i : 0,
                _ => entry.Value ?? string.Empty
            };

            parameters[entry.Name] = value;
        }

        return parameters.Count > 0 ? parameters : null;
    }

    private WinRmConnectionOptions BuildConnectionOptions()
    {
        int? port = int.TryParse(CustomPort, out int p) && p > 0 ? p : null;

        return new WinRmConnectionOptions
        {
            AuthMethod = SelectedAuthMethod,
            Transport = SelectedTransport,
            CustomPort = port
        };
    }

    private void OnLibraryChanged(object? sender, ScriptLibraryChangedEventArgs e)
    {
        _logger.Information("Script library changed — refreshing available scripts");
        _ = RefreshAsync();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _scriptLoaderService.LibraryChanged -= OnLibraryChanged;
        _cts.Dispose();
        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// Represents a single script parameter entry for the dynamic parameter form.
/// </summary>
public partial class ParameterEntry : ObservableObject
{
    /// <summary>
    /// Gets the parameter name matching the PowerShell param block.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets the display-friendly name for the UI.
    /// </summary>
    public required string DisplayName { get; init; }

    /// <summary>
    /// Gets the description of the parameter.
    /// </summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>
    /// Gets the parameter type ("string", "int", "bool", "choice").
    /// </summary>
    public required string ParameterType { get; init; }

    /// <summary>
    /// Gets a value indicating whether the parameter is required.
    /// </summary>
    public bool IsRequired { get; init; }

    /// <summary>
    /// Gets the valid choices when <see cref="ParameterType"/> is "choice".
    /// </summary>
    public IReadOnlyList<string>? Choices { get; init; }

    /// <summary>
    /// Gets or sets the current parameter value.
    /// </summary>
    [ObservableProperty]
    private string _value = string.Empty;
}
