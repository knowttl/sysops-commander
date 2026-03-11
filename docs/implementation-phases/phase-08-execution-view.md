# Phase 8: Execution View & Script Library

> **Goal:** Build the main execution interface — host target list, script selection, parameter forms, WinRM connection configuration UI, results panel with export — and the Script Library browser with search, filtering, and validation display.
>
> **Prereqs:** Phase 7 complete (AD views, Quick Connect done). Phase 4 execution engine and Phase 5 script loader fully functional.
>
> **Outputs:** Fully functional `ExecutionView`, `ScriptLibraryView` with all controls, parameter forms, WinRM config UI, results panel with export.

---

## Sub-Steps

### 8.1 — Implement `ExecutionViewModel.cs` — Properties and State

**File:** `src/SysOpsCommander.ViewModels/ExecutionViewModel.cs`

**Dependencies (injected):**
- `IHostTargetingService` (singleton — shared target list)
- `IRemoteExecutionService`
- `IScriptLoaderService`
- `ICredentialService`
- `ISettingsService`
- `IDialogService`
- `IExportService`
- `IAuditLogService`
- `Serilog.ILogger`

**Observable properties:**
```csharp
// Targets
[ObservableProperty]
private ObservableCollection<HostTarget> _targets;  // bound from IHostTargetingService.Targets

// Script selection
[ObservableProperty]
private ScriptPlugin? _selectedScript;

[ObservableProperty]
private ObservableCollection<ScriptPlugin> _availableScripts = new();

[ObservableProperty]
private string _adhocScript = string.Empty;

[ObservableProperty]
private bool _isAdhocMode;

// Parameters
[ObservableProperty]
private ObservableCollection<ParameterEntry> _scriptParameters = new();

// WinRM Configuration
[ObservableProperty]
private WinRmAuthMethod _selectedAuthMethod;

[ObservableProperty]
private WinRmTransport _selectedTransport;

[ObservableProperty]
private bool _isCredSspSelected;

[ObservableProperty]
private string _credSspWarning = string.Empty;

// Execution state
[ObservableProperty]
private bool _isExecuting;

[ObservableProperty]
private string _executionStatus = string.Empty;

[ObservableProperty]
private double _executionProgress;

// Results
[ObservableProperty]
private ObservableCollection<HostResult> _executionResults = new();

[ObservableProperty]
private HostResult? _selectedResult;

[ObservableProperty]
private string _selectedResultOutput = string.Empty;
```

**`ParameterEntry` helper class:**
```csharp
public partial class ParameterEntry : ObservableObject
{
    public string Name { get; init; } = string.Empty;
    public string Type { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public bool IsRequired { get; init; }
    public string? DefaultValue { get; init; }
    public IReadOnlyList<string>? Choices { get; init; }

    [ObservableProperty]
    private string _value = string.Empty;
}
```

**Commit:** `feat(viewmodels): implement ExecutionViewModel properties and state`

---

### 8.2 — Implement WinRM Configuration Binding

**On ViewModel initialization:**
```csharp
public async Task InitializeAsync()
{
    // Load defaults from settings
    var authDefault = await _settingsService.GetEffectiveAsync("DefaultWinRmAuthMethod");
    SelectedAuthMethod = Enum.Parse<WinRmAuthMethod>(authDefault);

    var transportDefault = await _settingsService.GetEffectiveAsync("DefaultWinRmTransport");
    SelectedTransport = Enum.Parse<WinRmTransport>(transportDefault);

    // Load available scripts
    var scripts = await _scriptLoaderService.LoadAllScriptsAsync(CancellationToken.None);
    AvailableScripts = new ObservableCollection<ScriptPlugin>(scripts);

    // Bind to shared targets
    Targets = _hostTargetingService.Targets;
}
```

**CredSSP selection handler:**
```csharp
partial void OnSelectedAuthMethodChanged(WinRmAuthMethod value)
{
    IsCredSspSelected = value == WinRmAuthMethod.CredSSP;
    CredSspWarning = IsCredSspSelected
        ? "CredSSP requires GPO configuration on both client and server hosts."
        : string.Empty;
}
```

**Commit:** `feat(viewmodels): implement WinRM configuration binding with CredSSP warning`

---

### 8.3 — Implement Script Selection → Parameter Form Generation

**When a script is selected:**
```csharp
partial void OnSelectedScriptChanged(ScriptPlugin? value)
{
    ScriptParameters.Clear();
    IsAdhocMode = false;

    if (value?.Manifest?.Parameters == null) return;

    foreach (var param in value.Manifest.Parameters)
    {
        ScriptParameters.Add(new ParameterEntry
        {
            Name = param.Name,
            Type = param.Type,
            Description = param.Description,
            IsRequired = param.Required,
            DefaultValue = param.DefaultValue,
            Choices = param.Choices,
            Value = param.DefaultValue ?? string.Empty
        });
    }
}
```

**UI rendering for parameters:**
- `string` type → `TextBox`
- `int` type → `TextBox` with numeric validation
- `bool` type → `CheckBox`
- `choice` type → `ComboBox` with `Choices` as items

**Commit:** `feat(viewmodels): implement dynamic parameter form generation from manifest`

---

### 8.4 — Implement Ad-Hoc Script Mode

**Toggle between structured script and ad-hoc:**
```csharp
[RelayCommand]
private void SwitchToAdhocMode()
{
    IsAdhocMode = true;
    SelectedScript = null;
    ScriptParameters.Clear();
}

[RelayCommand]
private void SwitchToScriptMode()
{
    IsAdhocMode = false;
    AdhocScript = string.Empty;
}
```

**Ad-hoc mode UI:**
- Large `TextBox` with `AcceptsReturn="True"` for multi-line PowerShell input
- No parameter form (parameters embedded in the script text)
- DangerLevel badge shows based on real-time AST analysis of the ad-hoc text

**Commit:** `feat(viewmodels): implement ad-hoc script mode`

---

### 8.5 — Implement Target Management Controls

**Commands for host target list:**
```csharp
[RelayCommand]
private void AddHostManually()
{
    // Show an input dialog or inline text box
    // Validate via HostnameValidator
    // Add to IHostTargetingService
}

[RelayCommand]
private async Task ImportFromCsvAsync()
{
    var filePath = await _dialogService.ShowOpenFileDialogAsync("CSV files (*.csv)|*.csv");
    if (filePath != null)
        _hostTargetingService.AddFromCsvFile(filePath);
}

[RelayCommand]
private async Task CheckReachabilityAsync()
{
    ExecutionStatus = "Checking reachability...";
    await _hostTargetingService.CheckReachabilityAsync(_cts.Token);
    ExecutionStatus = $"Reachability check complete. {Targets.Count(t => t.Status == HostStatus.Reachable)} reachable.";
}

[RelayCommand]
private void RemoveSelectedTarget(HostTarget target)
{
    _hostTargetingService.RemoveTarget(target.Hostname);
}

[RelayCommand]
private void ClearAllTargets()
{
    _hostTargetingService.ClearTargets();
}
```

**Commit:** `feat(viewmodels): implement target management commands (add, import, reachability, remove)`

---

### 8.6 — Implement Execution Flow

**Main execute command:**
```csharp
[RelayCommand]
private async Task ExecuteAsync()
{
    // Step 0: Validate targets exist
    if (!Targets.Any())
    {
        await _dialogService.ShowError("No Targets", "Add at least one target host before executing.");
        return;
    }

    // Step 1: Get script content
    var scriptContent = IsAdhocMode ? AdhocScript : SelectedScript?.Content;
    if (string.IsNullOrWhiteSpace(scriptContent))
    {
        await _dialogService.ShowError("No Script", "Select a script or enter ad-hoc commands.");
        return;
    }

    // Step 1.5: CredSSP credential check
    PSCredential? credential = null;
    if (SelectedAuthMethod == WinRmAuthMethod.CredSSP)
    {
        var confirmed = await _dialogService.ShowConfirmationAsync(
            "CredSSP Credentials Required",
            "CredSSP requires explicit credentials. Would you like to enter credentials now?");
        if (!confirmed) return;
        credential = await _dialogService.ShowCredentialDialogAsync(
            _adService.GetActiveDomain()?.DomainName, SelectedAuthMethod);
        if (credential == null) return;
    }
    else
    {
        // Optional: offer credential prompt
        // For Kerberos, current user context is used by default
    }

    // Step 2: Build ExecutionJob
    var job = new ExecutionJob
    {
        Id = Guid.NewGuid(),
        ScriptContent = scriptContent,
        ScriptName = IsAdhocMode ? "(ad-hoc)" : SelectedScript?.Manifest?.Name ?? SelectedScript?.FilePath ?? "Unknown",
        Parameters = BuildParameterDictionary(),
        TargetHosts = Targets.ToList(),
        ExecutionType = ExecutionType.PowerShell,
        WinRmConnectionOptions = new WinRmConnectionOptions
        {
            AuthMethod = SelectedAuthMethod,
            Transport = SelectedTransport
        },
        Credential = credential,
        ThrottleLimit = await _settingsService.GetTypedAsync("DefaultThrottle", AppConstants.DefaultThrottle, CancellationToken.None),
        TimeoutSeconds = await _settingsService.GetTypedAsync("DefaultTimeoutSeconds", AppConstants.DefaultWinRmTimeoutSeconds, CancellationToken.None)
    };

    // Step 3: Execute
    IsExecuting = true;
    ExecutionResults.Clear();
    _cts = new CancellationTokenSource();

    var progress = new Progress<HostResult>(result =>
    {
        ExecutionResults.Add(result);
        ExecutionProgress = (double)ExecutionResults.Count / job.TargetHosts.Count * 100;
        ExecutionStatus = $"Executing: {ExecutionResults.Count}/{job.TargetHosts.Count} complete";
    });

    try
    {
        await _executionService.ExecuteAsync(job, progress, _cts.Token);
        ExecutionStatus = $"Complete: {ExecutionResults.Count(r => r.Status == HostStatus.Success)} succeeded, " +
                          $"{ExecutionResults.Count(r => r.Status == HostStatus.Failed)} failed";
    }
    catch (OperationCanceledException)
    {
        ExecutionStatus = "Execution cancelled.";
    }
    finally
    {
        IsExecuting = false;
        credential?.Dispose();  // Dispose credentials immediately
    }
}
```

**Commit:** `feat(viewmodels): implement full execution flow with progress and CredSSP handling`

---

### 8.7 — Implement Cancel Execution

```csharp
[RelayCommand]
private void CancelExecution()
{
    _cts?.Cancel();
    ExecutionStatus = "Cancelling...";
}
```

**UI:** "Cancel" button visible only when `IsExecuting` is true.

**Commit:** `feat(viewmodels): implement execution cancellation`

---

### 8.8 — Implement Result Selection → Detail View

```csharp
partial void OnSelectedResultChanged(HostResult? value)
{
    if (value == null)
    {
        SelectedResultOutput = string.Empty;
        return;
    }

    if (value.IsFileReference)
    {
        // Large result stored on disk — read on demand
        try
        {
            SelectedResultOutput = File.ReadAllText(value.Output);
        }
        catch (Exception ex)
        {
            SelectedResultOutput = $"Error reading result file: {ex.Message}";
        }
    }
    else
    {
        SelectedResultOutput = value.Output;
    }
}
```

**Output rendering based on `outputFormat`:**
- `text` or `table` → monospace `TextBox` with `FontFamily="Consolas"`
- `json` → attempt `JsonDocument.Parse()`:
  - If valid JSON → format with indentation and display in monospace (or `TreeView` for v2)
  - If invalid → fall back to raw text display

> **Improvement:** Add a "JSON tree" rendering component for `json` outputFormat. For v1, formatted indented JSON text is sufficient.

**Commit:** `feat(viewmodels): implement result selection and output rendering`

---

### 8.9 — Implement Export Results

```csharp
[RelayCommand]
private async Task ExportResultsToCsvAsync()
{
    var filePath = await _dialogService.ShowSaveFileDialogAsync(".csv", "CSV files (*.csv)|*.csv");
    if (filePath != null)
    {
        await _exportService.ExportToCsvAsync(ExecutionResults, filePath, CancellationToken.None);
        _logger.Information("Exported results to CSV: {FilePath}", filePath);
    }
}

[RelayCommand]
private async Task ExportResultsToExcelAsync()
{
    var filePath = await _dialogService.ShowSaveFileDialogAsync(".xlsx", "Excel files (*.xlsx)|*.xlsx");
    if (filePath != null)
    {
        await _exportService.ExportToExcelAsync(ExecutionResults, filePath, CancellationToken.None);
        _logger.Information("Exported results to Excel: {FilePath}", filePath);
    }
}
```

> **Improvement:** Add a "Re-run" button that re-executes the last job with the same script, parameters, and targets.

**Commit:** `feat(viewmodels): implement CSV and Excel export from execution results`

---

### 8.10 — Build `ExecutionView.xaml`

**File:** `src/SysOpsCommander.App/Views/ExecutionView.xaml`

**Layout:**
```
┌─────────────────────────────────────────────────────┐
│  Execution Controls                                  │
│ ┌────────────────────────┬─────────────────────────┐ │
│ │ Targets               │ Script                   │ │
│ │ [Add Host] [CSV]      │ [Script Dropdown ▼]     │ │
│ │ [Check Reach.] [Clear]│ [Ad-Hoc Toggle]          │ │
│ │ ┌──────────────────┐  │                          │ │
│ │ │ HOST01  ✓        │  │ Parameters:              │ │
│ │ │ HOST02  ✓        │  │ ┌──────────────────────┐ │ │
│ │ │ HOST03  ✗        │  │ │ Filter: [______]     │ │ │
│ │ │ [x] [x] [x]     │  │ │ MaxEvents: [100]     │ │ │
│ │ └──────────────────┘  │ └──────────────────────┘ │ │
│ └────────────────────────┴─────────────────────────┘ │
│                                                      │
│  WinRM Config: Auth [Kerberos ▼] Transport [HTTP ▼]  │
│  ⚠ CredSSP info banner (when CredSSP selected)       │
│                                                      │
│  [▶ Execute] [⏹ Cancel]                 Progress: ██░ │
├──────────────────────────────────────────────────────┤
│  Results                                             │
│ ┌──────────────────────┬────────────────────────────┐│
│ │ Host    │Status│Time ││ Output Detail             ││
│ │ HOST01  │ ✓    │ 2s  ││ (selected host output)    ││
│ │ HOST02  │ ✓    │ 3s  ││                           ││
│ │ HOST03  │ ✗    │ -   ││ [monospace text area]     ││
│ └──────────────────────┘│                           ││
│                         │                           ││
│ [Export CSV] [Export XLS]│                           ││
│                         └────────────────────────────┘│
└──────────────────────────────────────────────────────┘
```

**Key XAML elements:**
- Host target list: `ListBox` with `DataTemplate` showing hostname + status icon + remove button
- Script selector: `ComboBox` bound to `AvailableScripts`
- Parameter form: `ItemsControl` with `DataTemplate` selecting control by parameter type
- WinRM config: `ComboBox` for auth method, `ToggleButton` or `ComboBox` for transport
- Results: `DataGrid` on left, `TextBox` on right for selected output
- CredSSP banner: `Border` with warning background, visible when `IsCredSspSelected`

**Commit:** `feat(app): build ExecutionView with targets, script, parameters, WinRM config, and results`

---

### 8.11 — Implement `ScriptLibraryViewModel.cs`

**File:** `src/SysOpsCommander.ViewModels/ScriptLibraryViewModel.cs`

**Dependencies:**
- `IScriptLoaderService`
- `IDialogService`
- `Serilog.ILogger`

**Properties:**
```csharp
[ObservableProperty]
private ObservableCollection<ScriptPlugin> _scripts = new();

[ObservableProperty]
private ObservableCollection<ScriptPlugin> _filteredScripts = new();

[ObservableProperty]
private string _searchFilter = string.Empty;

[ObservableProperty]
private string _categoryFilter = "All";

[ObservableProperty]
private ObservableCollection<string> _categories = new();

[ObservableProperty]
private ScriptPlugin? _selectedScript;

[ObservableProperty]
private string _selectedScriptContent = string.Empty;

[ObservableProperty]
private IReadOnlyList<DangerousPatternWarning>? _selectedScriptWarnings;
```

**Filter logic:**
```csharp
partial void OnSearchFilterChanged(string value) => ApplyFilters();
partial void OnCategoryFilterChanged(string value) => ApplyFilters();

private void ApplyFilters()
{
    var filtered = Scripts.AsEnumerable();

    if (!string.IsNullOrWhiteSpace(SearchFilter))
        filtered = filtered.Where(s =>
            (s.Manifest?.Name ?? s.FilePath).Contains(SearchFilter, StringComparison.OrdinalIgnoreCase));

    if (CategoryFilter != "All")
        filtered = filtered.Where(s =>
            (s.Manifest?.Category ?? AppConstants.DefaultScriptCategory) == CategoryFilter);

    FilteredScripts = new ObservableCollection<ScriptPlugin>(filtered);
}
```

**Commands:**
```csharp
[RelayCommand]
private async Task RefreshLibraryAsync()
{
    await _scriptLoaderService.RefreshAsync(_cts.Token);
    Scripts = new ObservableCollection<ScriptPlugin>(
        await _scriptLoaderService.LoadAllScriptsAsync(_cts.Token));
    PopulateCategories();
    ApplyFilters();
}

[RelayCommand]
private void UseScriptInExecution()
{
    // Navigate to Execution view with SelectedScript pre-selected
    // Via MainWindowViewModel navigation + script selection
}
```

**Commit:** `feat(viewmodels): implement ScriptLibraryViewModel with search and category filtering`

---

### 8.12 — Build `ScriptLibraryView.xaml`

**File:** `src/SysOpsCommander.App/Views/ScriptLibraryView.xaml`

**Layout:**
```
┌──────────────────────────────────────────────────┐
│  Script Library                    [↻ Refresh]    │
├──────────────────────────────────────────────────┤
│  Search: [____________]  Category: [All      ▼]  │
├────────────────────────┬─────────────────────────┤
│  Scripts List          │  Script Detail           │
│ ┌────────────────────┐ │  Name: Get-LocalAdmins   │
│ │ 📄 Get-Installed.. │ │  Category: Security      │
│ │ 🔒 Get-LocalAdmins │ │  Version: 1.0.0          │
│ │ 🔒 Get-SecurityEv..│ │  Author: SysOps Team     │
│ │ 🔧 Test-WinRM..    │ │  Danger: ⚪ Safe         │
│ │ 📄 Invoke-Quick..  │ │  Output: table           │
│ └────────────────────┘ │                          │
│                        │  Parameters:             │
│ Icons: 🔒 Security    │  (none)                  │
│        📄 Inventory   │                          │
│        🔧 Diagnostics │  Validation:             │
│                        │  ✓ Syntax OK             │
│                        │  ⚠ 0 warnings            │
│                        │                          │
│                        │  [Use in Execution]      │
│                        │  [View Source]            │
└────────────────────────┴─────────────────────────┘
```

**Key elements:**
- Script list: `ListBox` with category icons and danger level indicators
- Detail panel: displays manifest metadata, validation status, source preview
- "View Source" shows script content in a read-only monospace text box
- Category-colored icons based on manifest category

**Commit:** `feat(app): build ScriptLibraryView with search, filtering, and detail panel`

---

### 8.13 — Write Unit Tests — `ExecutionViewModel`

**File:** `tests/SysOpsCommander.Tests/ViewModels/ExecutionViewModelTests.cs`

**Test cases (8+):**

| # | Test Name | Scenario | Verification |
|---|-----------|----------|-------------|
| 1 | `Execute_NoTargets_ShowsError` | Empty target list | Dialog shown, no execution |
| 2 | `Execute_NoScript_ShowsError` | No script selected | Dialog shown |
| 3 | `Execute_CredSsp_PromptsForCredentials` | CredSSP selected | Credential dialog shown |
| 4 | `Execute_CredSsp_UserCancels_Aborts` | Cancel credential dialog | Execution aborted |
| 5 | `Execute_Success_ReportsProgress` | 3 hosts | Progress reported 3 times |
| 6 | `Cancel_SetsStatus_ToCancelling` | Cancel during execution | Status = "Cancelling..." |
| 7 | `SelectScript_GeneratesParameterForm` | Script with 2 params | 2 `ParameterEntry` items |
| 8 | `SwitchToAdhocMode_ClearsScript` | Toggle ad-hoc | `SelectedScript` is null |
| 9 | `ExportCsv_CallsExportService` | Export action | `ExportService.ExportToCsvAsync` called |

**Commit:** `test(viewmodels): add ExecutionViewModel tests`

---

### 8.14 — Write Unit Tests — `ScriptLibraryViewModel`

**File:** `tests/SysOpsCommander.Tests/ViewModels/ScriptLibraryViewModelTests.cs`

**Test cases (5+):**

| # | Test Name | Scenario | Verification |
|---|-----------|----------|-------------|
| 1 | `Refresh_LoadsScriptsFromService` | Refresh | Scripts populated |
| 2 | `SearchFilter_FiltersScriptsByName` | Search "Admin" | Only matching scripts shown |
| 3 | `CategoryFilter_FiltersScriptsByCategory` | Category = "Security" | Only Security scripts |
| 4 | `SelectScript_ShowsDetail` | Select script | Manifest details populated |
| 5 | `CategoryFilter_All_ShowsAllScripts` | Category = "All" | All scripts shown |

**Commit:** `test(viewmodels): add ScriptLibraryViewModel tests`

---

### 8.15 — Phase 8 Verification

**Full acceptance criteria check:**
- [ ] Execution view displays shared target list from `IHostTargetingService`
- [ ] Target management: manual add, CSV import, reachability check, remove, clear
- [ ] Script selection populates parameter form dynamically based on manifest
- [ ] Ad-hoc mode allows free-form PowerShell input
- [ ] WinRM auth method and transport are selectable in the UI
- [ ] CredSSP selection shows info banner and forces credential prompt
- [ ] Execution fires with progress bar and per-host result streaming
- [ ] Cancellation works mid-execution
- [ ] Results panel shows per-host output with monospace formatting
- [ ] JSON output formatted (indented) when `outputFormat = "json"`
- [ ] Export to CSV and Excel functional
- [ ] Auth/transport choices recorded in audit log
- [ ] Toast notification fires on execution completion
- [ ] Script Library loads all scripts with category icons and danger levels
- [ ] Search and category filtering work
- [ ] Script detail shows manifest, validation, and source preview
- [ ] "Use in Execution" navigates and pre-selects the script
- [ ] All unit tests pass (14+ new cases)
- [ ] `dotnet build` — zero warnings
- [ ] `dotnet test` — all tests pass (Phase 0–8)
- [ ] Final commit: `chore: complete Phase 8 — Execution view and Script Library`

---

## Improvements & Notes

1. **JSON tree rendering (step 8.8):** For v1, formatted indented JSON in a monospace text block is sufficient. For v2, consider a `TreeView`-based JSON renderer that allows expanding/collapsing nodes. Libraries like `Newtonsoft.Json.Linq` (`JToken`) can parse JSON into a tree structure for WPF binding.

2. **Export button placement (step 8.9):** Export buttons are below the results list. Consider also adding a context menu (right-click) on the results DataGrid with "Export All" and "Copy Selected" options.

3. **Re-run button:** Add a "Re-run" command that re-executes the last `ExecutionJob` with the same parameters and targets. Useful for monitoring scenarios where the same check is run periodically.

4. **`outputFormat` reminder:** The `outputFormat` field from the manifest is used ONLY for UI rendering decisions — it does NOT affect how the script's output is parsed. All output is captured as raw text strings from the PowerShell pipeline. The rendering logic simply selects whether to display in a plain text box or attempt JSON formatting.

5. **Parameter validation before execution (step 8.6):** Before executing, validate that all required parameters have non-empty values and that `int` parameters are numeric. Show inline validation errors on the parameter form.
