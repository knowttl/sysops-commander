# Phase 9: Audit Log View, Settings & Auto-Update

> **Goal:** Build the audit log browser with filtering and export, the settings page with org-wide vs. per-user layering, and the fully specified auto-update system with network share delivery and bootstrapper updater.
>
> **Prereqs:** Phase 8 complete (execution records audit logs). Phase 1 `AuditLogRepository`, `SettingsRepository` functional.
>
> **Outputs:** `AuditLogView`, `SettingsView`, `AutoUpdateService`, `SysOpsUpdater` bootstrapper — all functional and tested.

---

## Sub-Steps

### 9.1 — Implement `AuditLogViewModel.cs` — Properties and State

**File:** `src/SysOpsCommander.ViewModels/AuditLogViewModel.cs`

**Dependencies:**
- `IAuditLogService`
- `IExportService`
- `IDialogService`
- `Serilog.ILogger`

**Observable properties:**
```csharp
[ObservableProperty]
private ObservableCollection<AuditLogEntry> _auditEntries = new();

[ObservableProperty]
private AuditLogEntry? _selectedEntry;

[ObservableProperty]
private string _selectedEntryDetail = string.Empty;

// Filters
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

[ObservableProperty]
private int _totalEntries;

[ObservableProperty]
private int _currentPage = 1;

[ObservableProperty]
private int _totalPages;
```

**Commit:** `feat(viewmodels): implement AuditLogViewModel properties and state`

---

### 9.2 — Implement Audit Log Query and Filtering

**Commands:**
```csharp
[RelayCommand]
private async Task LoadAuditLogAsync()
{
    var filter = new AuditLogFilter
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

    var result = await _auditLogService.QueryAsync(filter, CancellationToken.None);
    AuditEntries = new ObservableCollection<AuditLogEntry>(result);
    TotalEntries = result.Count;  // or from paged result metadata
}

[RelayCommand]
private void ClearFilters()
{
    FilterStartDate = null;
    FilterEndDate = null;
    FilterScriptName = string.Empty;
    FilterHostname = string.Empty;
    FilterDomain = string.Empty;
    FilterAuthMethod = null;
    FilterUserName = string.Empty;
    _ = LoadAuditLogAsync();
}

[RelayCommand]
private async Task NextPageAsync()
{
    if (CurrentPage < TotalPages) { CurrentPage++; await LoadAuditLogAsync(); }
}

[RelayCommand]
private async Task PreviousPageAsync()
{
    if (CurrentPage > 1) { CurrentPage--; await LoadAuditLogAsync(); }
}
```

**Commit:** `feat(viewmodels): implement audit log query with filtering and pagination`

---

### 9.3 — Implement Audit Log Export

```csharp
[RelayCommand]
private async Task ExportAuditLogAsync()
{
    var filePath = await _dialogService.ShowSaveFileDialogAsync(".csv", "CSV files (*.csv)|*.csv");
    if (filePath != null)
    {
        await _exportService.ExportAuditLogToCsvAsync(AuditEntries, filePath, CancellationToken.None);
        _logger.Information("Exported audit log to CSV: {FilePath}", filePath);
    }
}
```

**Commit:** `feat(viewmodels): implement audit log export to CSV`

---

### 9.4 — Implement Audit Log Purge

```csharp
[RelayCommand]
private async Task PurgeOldEntriesAsync()
{
    var confirmed = await _dialogService.ShowConfirmationAsync(
        "Purge Old Entries",
        $"Delete audit log entries older than {AppConstants.AuditLogRetentionDays} days? This cannot be undone.");
    if (confirmed)
    {
        await _auditLogService.PurgeOldEntriesAsync(AppConstants.AuditLogRetentionDays);
        await LoadAuditLogAsync();
        _logger.Information("Purged audit log entries older than {Days} days", AppConstants.AuditLogRetentionDays);
    }
}
```

**Commit:** `feat(viewmodels): implement audit log purge with confirmation`

---

### 9.5 — Build `AuditLogView.xaml`

**File:** `src/SysOpsCommander.App/Views/AuditLogView.xaml`

**Layout:**
```
┌──────────────────────────────────────────────────────┐
│  Audit Log                                            │
├──────────────────────────────────────────────────────┤
│  Filters:                                             │
│  Date: [Start ▼] to [End ▼]  Script: [________]      │
│  Host: [________]  Domain: [________]                 │
│  Auth: [All ▼]     User: [________]                   │
│  [Apply]  [Clear Filters]                             │
├──────────────────────────────────────────────────────┤
│  ┌────────────────────────────────────────────────┐   │
│  │ Date     │Script│Hosts│Auth │Transport│Domain│User│
│  │ 03/10/26 │Get.. │ 5   │Kerb │HTTP     │corp  │js  │
│  │ 03/09/26 │Test..│ 1   │NTLM │HTTPS    │corp  │js  │
│  │ ...      │...   │ ... │...  │...      │...   │... │
│  └────────────────────────────────────────────────┘   │
│                                                       │
│  Page [1] of [12]  [◁ Prev] [Next ▷]                  │
│  Total: 584 entries                                   │
│                                                       │
│  [Export CSV]  [Purge Old Entries]                     │
├──────────────────────────────────────────────────────┤
│  Entry Detail (when selected):                        │
│  Script: Get-InstalledSoftware                        │
│  Parameters: { "NameFilter": "Chrome" }               │
│  Targets: HOST01, HOST02, HOST03                      │
│  Results: 3/3 succeeded                               │
│  Duration: 8.2s                                       │
└──────────────────────────────────────────────────────┘
```

**Key columns added vs. original plan:** Auth Method, Transport, Target Domain.

**Commit:** `feat(app): build AuditLogView with filters, DataGrid, pagination, and export`

---

### 9.6 — Implement `SettingsViewModel.cs` — Properties

**File:** `src/SysOpsCommander.ViewModels/SettingsViewModel.cs`

**Dependencies:**
- `ISettingsService`
- `IAutoUpdateService`
- `IDialogService`
- `Serilog.ILogger`

**Observable properties organized by section:**

```csharp
// Domain & Connection
[ObservableProperty]
private string _defaultDomain = string.Empty;

[ObservableProperty]
private string _orgDefaultDomain = string.Empty;  // read-only

[ObservableProperty]
private WinRmAuthMethod _defaultAuthMethod;

[ObservableProperty]
private WinRmTransport _defaultTransport;

[ObservableProperty]
private int _staleComputerThresholdDays;

// Execution
[ObservableProperty]
private int _defaultThrottle;

[ObservableProperty]
private int _defaultTimeoutSeconds;

// Repository
[ObservableProperty]
private string _orgScriptRepositoryPath = string.Empty;  // read-only

[ObservableProperty]
private bool _hasUserScriptPathOverride;

[ObservableProperty]
private string _userScriptRepositoryPath = string.Empty;

// Logging
[ObservableProperty]
private string _logLevel = "Information";

// Update
[ObservableProperty]
private string _updateSharePath = string.Empty;

[ObservableProperty]
private string _currentVersion = string.Empty;

[ObservableProperty]
private string _updateStatus = string.Empty;

[ObservableProperty]
private bool _isUpdateAvailable;

// State
[ObservableProperty]
private bool _hasUnsavedChanges;
```

**Commit:** `feat(viewmodels): implement SettingsViewModel properties`

---

### 9.7 — Implement Settings Load/Save

```csharp
[RelayCommand]
private async Task LoadSettingsAsync()
{
    // Org-wide defaults (read-only display)
    OrgDefaultDomain = _settingsService.GetOrgDefault("DefaultDomain");
    OrgScriptRepositoryPath = _settingsService.GetOrgDefault("SharedScriptRepositoryPath");

    // Per-user effective values
    DefaultDomain = await _settingsService.GetEffectiveAsync("DefaultDomain", CancellationToken.None) ?? string.Empty;
    DefaultAuthMethod = Enum.Parse<WinRmAuthMethod>(
        await _settingsService.GetEffectiveAsync("DefaultWinRmAuthMethod", CancellationToken.None) ?? "Kerberos");
    DefaultTransport = Enum.Parse<WinRmTransport>(
        await _settingsService.GetEffectiveAsync("DefaultWinRmTransport", CancellationToken.None) ?? "HTTP");
    StaleComputerThresholdDays = await _settingsService.GetTypedAsync("StaleComputerThresholdDays", 90, CancellationToken.None);
    DefaultThrottle = await _settingsService.GetTypedAsync("DefaultThrottle", AppConstants.DefaultThrottle, CancellationToken.None);
    DefaultTimeoutSeconds = await _settingsService.GetTypedAsync("DefaultTimeoutSeconds", AppConstants.DefaultWinRmTimeoutSeconds, CancellationToken.None);

    // User script path override
    var userPath = await _settingsService.GetAsync("UserScriptRepositoryPath", string.Empty, CancellationToken.None);
    HasUserScriptPathOverride = !string.IsNullOrWhiteSpace(userPath);
    UserScriptRepositoryPath = userPath;

    // Version
    CurrentVersion = System.Reflection.Assembly.GetExecutingAssembly()
        .GetName().Version?.ToString() ?? "Unknown";

    HasUnsavedChanges = false;
}

[RelayCommand]
private async Task SaveSettingsAsync()
{
    await _settingsService.SetAsync("DefaultDomain", DefaultDomain, CancellationToken.None);
    await _settingsService.SetAsync("DefaultWinRmAuthMethod", DefaultAuthMethod.ToString(), CancellationToken.None);
    await _settingsService.SetAsync("DefaultWinRmTransport", DefaultTransport.ToString(), CancellationToken.None);
    await _settingsService.SetAsync("StaleComputerThresholdDays", StaleComputerThresholdDays.ToString(), CancellationToken.None);
    await _settingsService.SetAsync("DefaultThrottle", DefaultThrottle.ToString(), CancellationToken.None);
    await _settingsService.SetAsync("DefaultTimeoutSeconds", DefaultTimeoutSeconds.ToString(), CancellationToken.None);
    await _settingsService.SetAsync("LogLevel", LogLevel, CancellationToken.None);

    if (HasUserScriptPathOverride)
        await _settingsService.SetAsync("UserScriptRepositoryPath", UserScriptRepositoryPath, CancellationToken.None);
    else
        await _settingsService.SetAsync("UserScriptRepositoryPath", string.Empty, CancellationToken.None);

    HasUnsavedChanges = false;
    _logger.Information("User settings saved");
}
```

**Commit:** `feat(viewmodels): implement settings load/save with org-wide vs. per-user layering`

---

### 9.8 — Build `SettingsView.xaml`

**File:** `src/SysOpsCommander.App/Views/SettingsView.xaml`

**Layout:**
```
┌──────────────────────────────────────────────────────┐
│  Settings                              [Save] [Reset] │
├──────────────────────────────────────────────────────┤
│                                                       │
│  ── Domain & Connection ──────────────────────────── │
│  Default Domain:  [____________]                      │
│    (org default: corp.contoso.com)                    │
│  WinRM Auth:      [Kerberos ▼]                       │
│  WinRM Transport: [HTTP ▼]                           │
│  Stale Threshold: [90] days                          │
│                                                       │
│  ── Execution ──────────────────────────────────────  │
│  Default Throttle:  [5]                              │
│  Default Timeout:   [60] seconds                     │
│                                                       │
│  ── Script Repository ──────────────────────────────  │
│  Org-wide path: \\server\share\scripts (read-only)    │
│  □ Override with custom path:                        │
│    [____________________] [Browse]                   │
│                                                       │
│  ── Logging ────────────────────────────────────────  │
│  Log Level: [Information ▼]                          │
│                                                       │
│  ── Updates ────────────────────────────────────────  │
│  Current Version: 1.0.0                              │
│  Update Share: [____________________]                │
│  [Check for Updates]  Status: Up to date             │
│                                                       │
└──────────────────────────────────────────────────────┘
```

**Key behaviors:**
- Org-wide defaults shown as read-only with label "(org default: value)"
- Per-user overrides editable
- Unsaved changes indicator (asterisk in title or "Save" button enabled/disabled)

**Commit:** `feat(app): build SettingsView with org-default display and per-user editing`

---

### 9.9 — Implement `IAutoUpdateService` Interface

**File:** `src/SysOpsCommander.Core/Interfaces/IAutoUpdateService.cs`

```csharp
public interface IAutoUpdateService
{
    Task<UpdateCheckResult> CheckForUpdateAsync(CancellationToken ct);
    Task<UpdateDownloadResult> DownloadAndStageAsync(CancellationToken ct);
    bool HasPendingUpdate();
    void LaunchUpdaterAndExit();
}

public record UpdateCheckResult(
    bool IsUpdateAvailable,
    string? LatestVersion,
    string? ReleaseNotes,
    string? ReleaseDate,
    string? MinimumVersion);

public record UpdateDownloadResult(
    bool Success,
    string? StagedPath,
    string? ErrorMessage);
```

**Commit:** `feat(core): define IAutoUpdateService interface with result types`

---

### 9.10 — Implement `AutoUpdateService.cs` — Check for Update

**File:** `src/SysOpsCommander.Services/AutoUpdateService.cs`

**Dependencies:**
- `ISettingsService`
- `Serilog.ILogger`

**`CheckForUpdateAsync()` implementation:**
1. Read update share path from settings: `await _settingsService.GetEffectiveAsync("UpdateNetworkSharePath")`
2. If empty → return `UpdateCheckResult(false, ...)`
3. Attempt to read `version.json` from `\\share\SysOpsCommander\version.json`
4. Deserialize:
   ```json
   {
       "version": "1.1.0",
       "releaseDate": "2026-04-15",
       "releaseNotes": "Added CredSSP support, bug fixes.",
       "minimumVersion": "1.0.0",
       "sha256": "abc123..."
   }
   ```
5. Compare `version.json:version` against current assembly version (semver comparison)
6. Return `UpdateCheckResult` with availability info

**Error handling (critical — never block startup):**
- Network share unreachable → Log at `Debug`, return `UpdateCheckResult(false, ...)`
- `version.json` malformed → Log at `Warning`, return `UpdateCheckResult(false, ...)`
- Any exception → Catch all, log, return false. Update check failure must NEVER crash the app

**Commit:** `feat(services): implement AutoUpdateService update check from network share`

---

### 9.11 — Implement `AutoUpdateService.cs` — Download and Stage

**`DownloadAndStageAsync()` implementation:**
1. Read `version.json` from the share (re-read, don't cache)
2. Copy `SysOpsCommander.zip` from the share to `%LOCALAPPDATA%\SysOpsCommander\Updates\SysOpsCommander.zip`
3. Verify SHA256 hash:
   ```csharp
   using var stream = File.OpenRead(downloadedZipPath);
   var hash = await SHA256.HashDataAsync(stream, ct);
   var hashString = Convert.ToHexString(hash);
   if (!hashString.Equals(expectedHash, StringComparison.OrdinalIgnoreCase))
       return UpdateDownloadResult(false, null, "SHA256 hash mismatch — download may be corrupted.");
   ```
4. Extract to `%LOCALAPPDATA%\SysOpsCommander\Updates\staged\`
5. Write `pending-update.json`:
   ```json
   {
       "stagedPath": "C:\\Users\\...\\Updates\\staged",
       "version": "1.1.0",
       "downloadedAt": "2026-04-15T14:30:00Z"
   }
   ```
6. Return success result
7. Prompt user: "Update downloaded. Restart to apply?"

**Commit:** `feat(services): implement AutoUpdateService download, SHA256 verification, and staging`

---

### 9.12 — Implement Pending Update Check on Startup

**In `App.xaml.cs` — early startup:**
```csharp
private void CheckPendingUpdate()
{
    var pendingPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        AppConstants.AppDataFolder, "Updates", "pending-update.json");

    if (!File.Exists(pendingPath)) return;

    try
    {
        var pending = JsonSerializer.Deserialize<PendingUpdate>(File.ReadAllText(pendingPath));
        if (pending?.StagedPath == null || !Directory.Exists(pending.StagedPath))
        {
            // Orphaned pending file — clean up
            File.Delete(pendingPath);
            return;
        }

        // Launch updater bootstrapper
        var updaterPath = Path.Combine(AppContext.BaseDirectory, "SysOpsUpdater.exe");
        Process.Start(new ProcessStartInfo
        {
            FileName = updaterPath,
            Arguments = $"\"{pending.StagedPath}\" \"{AppContext.BaseDirectory}\" {Environment.ProcessId}",
            UseShellExecute = false
        });

        // Exit the main app so files can be replaced
        Application.Current.Shutdown();
    }
    catch (Exception ex)
    {
        _logger.Warning(ex, "Failed to process pending update — continuing with current version");
        File.Delete(pendingPath);
    }
}
```

**Commit:** `feat(app): implement pending update detection on startup`

---

### 9.13 — Create `SysOpsUpdater` Bootstrapper Project

**Project:** `src/SysOpsUpdater/SysOpsUpdater.csproj`
- Console application (.NET 8)
- Minimal project — no DI, no external dependencies
- Output copied to main app's publish directory

> **Improvement:** This should be a **separate console project** in the solution, not part of the main WPF app. It's a tiny (~50 lines) utility.

**`Program.cs` implementation:**
```csharp
// SysOpsUpdater — Bootstrapper for applying staged updates
// Usage: SysOpsUpdater.exe <stagedPath> <appDirectory> <waitForPid>

if (args.Length < 3)
{
    Console.Error.WriteLine("Usage: SysOpsUpdater.exe <stagedPath> <appDirectory> <waitForPid>");
    return 1;
}

var stagedPath = args[0];
var appDirectory = args[1];
var waitForPid = int.Parse(args[2]);

// Step 1: Wait for main app to exit
Console.WriteLine($"Waiting for process {waitForPid} to exit...");
try
{
    var process = Process.GetProcessById(waitForPid);
    process.WaitForExit(TimeSpan.FromSeconds(30));
}
catch (ArgumentException)
{
    // Process already exited
}

// Step 2: Backup current files (optional safety net)
var backupPath = appDirectory + ".backup";
if (Directory.Exists(backupPath))
    Directory.Delete(backupPath, true);

// Step 3: Copy staged files to app directory
Console.WriteLine("Applying update...");
CopyDirectory(stagedPath, appDirectory);

// Step 4: Clean up
Console.WriteLine("Cleaning up...");
Directory.Delete(stagedPath, true);
var pendingPath = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
    "SysOpsCommander", "Updates", "pending-update.json");
if (File.Exists(pendingPath))
    File.Delete(pendingPath);

// Step 5: Re-launch main app
Console.WriteLine("Launching SysOps Commander...");
Process.Start(new ProcessStartInfo
{
    FileName = Path.Combine(appDirectory, "SysOpsCommander.App.exe"),
    UseShellExecute = true
});

return 0;

static void CopyDirectory(string source, string destination)
{
    foreach (var file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
    {
        var relativePath = Path.GetRelativePath(source, file);
        var destFile = Path.Combine(destination, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(destFile)!);
        File.Copy(file, destFile, overwrite: true);
    }
}
```

**Failure recovery:**
- If the copy fails mid-way, the original files are partially overwritten but the staged files remain. The user can manually copy from staged or re-download.
- On next startup, if `pending-update.json` exists but staged directory is missing → delete pending file and continue normally (step 9.12 handles this)

**Commit:** `feat(updater): create SysOpsUpdater bootstrapper console application`

---

### 9.14 — Implement Background Update Check

**In `MainWindowViewModel.InitializeAsync()`:**
```csharp
private async Task CheckForUpdatesAsync()
{
    try
    {
        var result = await _autoUpdateService.CheckForUpdateAsync(CancellationToken.None);
        if (result.IsUpdateAvailable)
        {
            IsUpdateAvailable = true;
            _logger.Information("Update available: {Version}", result.LatestVersion);
        }
    }
    catch
    {
        // Silently ignore — update check failure is never blocking
    }
}
```

**Status bar integration:** When `IsUpdateAvailable` is true, show "⬆ Update Available" in status bar.

**Settings "Check for Updates" button:**
```csharp
[RelayCommand]
private async Task CheckForUpdatesManuallyAsync()
{
    UpdateStatus = "Checking...";
    var result = await _autoUpdateService.CheckForUpdateAsync(CancellationToken.None);
    if (result.IsUpdateAvailable)
    {
        UpdateStatus = $"Update available: v{result.LatestVersion} ({result.ReleaseDate})";
        IsUpdateAvailable = true;

        var download = await _dialogService.ShowConfirmationAsync(
            "Update Available",
            $"Version {result.LatestVersion} is available.\n\n{result.ReleaseNotes}\n\nDownload and stage the update?");
        if (download)
        {
            UpdateStatus = "Downloading...";
            var downloadResult = await _autoUpdateService.DownloadAndStageAsync(CancellationToken.None);
            UpdateStatus = downloadResult.Success
                ? "Update staged. Restart to apply."
                : $"Download failed: {downloadResult.ErrorMessage}";
        }
    }
    else
    {
        UpdateStatus = "You are running the latest version.";
    }
}
```

**Commit:** `feat(viewmodels): implement background and manual update check`

---

### 9.15 — Register Phase 9 Services in DI

**Action:** Update `ServiceCollectionExtensions.cs`:
```csharp
services.AddSingleton<IAutoUpdateService, AutoUpdateService>();
```

(`IAuditLogService` and `ISettingsService` should already be registered from earlier phases.)

**Commit:** `build(app): register AutoUpdateService in DI container`

---

### 9.16 — Write Unit Tests — `AuditLogViewModel`

**File:** `tests/SysOpsCommander.Tests/ViewModels/AuditLogViewModelTests.cs`

**Test cases (5+):**

| # | Test Name | Scenario | Verification |
|---|-----------|----------|-------------|
| 1 | `Load_PopulatesEntries` | Load audit log | Entries populated |
| 2 | `Filter_ByScriptName_FiltersCorrectly` | Filter = "Get-*" | Service called with filter |
| 3 | `ClearFilters_ResetsAll` | Clear filters | All filter values reset |
| 4 | `Export_CallsExportService` | Export action | `ExportService` called |
| 5 | `Purge_WithConfirmation_PurgesOldEntries` | Confirm purge | `PurgeOldEntriesAsync` called |
| 6 | `Purge_WithoutConfirmation_DoesNothing` | Cancel purge | Service not called |

**Commit:** `test(viewmodels): add AuditLogViewModel tests`

---

### 9.17 — Write Unit Tests — `SettingsViewModel`

**File:** `tests/SysOpsCommander.Tests/ViewModels/SettingsViewModelTests.cs`

**Test cases (5+):**

| # | Test Name | Scenario | Verification |
|---|-----------|----------|-------------|
| 1 | `Load_PopulatesFromService` | Load settings | All properties populated |
| 2 | `Load_ShowsOrgDefaults` | Load settings | `OrgDefaultDomain` populated |
| 3 | `Save_PersistsAllSettings` | Save | `SetAsync` called for each setting |
| 4 | `Save_ClearsUnsavedFlag` | Save | `HasUnsavedChanges = false` |
| 5 | `PropertyChange_SetsUnsavedFlag` | Change throttle | `HasUnsavedChanges = true` |

**Commit:** `test(viewmodels): add SettingsViewModel tests`

---

### 9.18 — Write Unit Tests — `AutoUpdateService`

**File:** `tests/SysOpsCommander.Tests/Services/AutoUpdateServiceTests.cs`

**Test cases (6+):**

| # | Test Name | Scenario | Verification |
|---|-----------|----------|-------------|
| 1 | `CheckForUpdate_NewerVersion_ReturnsAvailable` | Remote 1.1.0 > local 1.0.0 | `IsUpdateAvailable = true` |
| 2 | `CheckForUpdate_SameVersion_ReturnsUpToDate` | Remote 1.0.0 == local 1.0.0 | `IsUpdateAvailable = false` |
| 3 | `CheckForUpdate_OlderVersion_ReturnsUpToDate` | Remote 0.9.0 < local 1.0.0 | `IsUpdateAvailable = false` |
| 4 | `CheckForUpdate_NetworkError_ReturnsFalse` | Share unreachable | `IsUpdateAvailable = false`, no throw |
| 5 | `DownloadAndStage_Sha256Mismatch_ReturnsFailure` | Bad hash | `Success = false` |
| 6 | `DownloadAndStage_ValidFile_StagesSuccessfully` | Good hash | Staged directory exists |

**Commit:** `test(services): add AutoUpdateService tests`

---

### 9.19 — Phase 9 Verification

**Full acceptance criteria check:**
- [ ] Audit log view displays all entries with Auth Method, Transport, Target Domain columns
- [ ] Audit log filtering by date range, script, hostname, domain, auth method, user all functional
- [ ] Audit log pagination works (next/previous/page numbers)
- [ ] Audit log export to CSV includes all columns
- [ ] Audit log purge with confirmation deletes old entries
- [ ] Settings page shows org-wide defaults as read-only
- [ ] Settings page allows per-user overrides for all configurable values
- [ ] Settings save persists to SQLite and reload works correctly
- [ ] Settings: domain, auth method, transport, stale threshold, throttle, timeout, log level all editable
- [ ] Settings: script repository shows org path (read-only) + per-user override
- [ ] Auto-update reads `version.json` from network share
- [ ] Auto-update compares versions correctly (semver)
- [ ] Auto-update downloads, verifies SHA256, and stages the update
- [ ] Auto-update SHA256 mismatch produces clear error
- [ ] `SysOpsUpdater.exe` bootstrapper applies update on restart without file-locking errors
- [ ] Failed update checks do not block application startup
- [ ] Status bar shows update-available indicator
- [ ] All unit tests pass (16+ new cases)
- [ ] `dotnet build` — zero warnings
- [ ] `dotnet test` — all tests pass (Phase 0–9)
- [ ] Final commit: `chore: complete Phase 9 — Audit Log, Settings, and Auto-Update`

---

## Improvements & Notes

1. **Separate console project for updater (step 9.13):** The bootstrapper must be a separate project because it needs to run independently after the main app exits. Add it as `src/SysOpsUpdater/SysOpsUpdater.csproj` and configure the main project's publish step to include the updater binary.

2. **Rollback mechanism (v2):** The current approach copies staged files over existing files. If the update is broken, the user is stuck. For v2, consider backing up the current installation before applying the update, and adding a `--rollback` flag to the updater that restores the backup.

3. **SHA256 clarification:** The `sha256` field in `version.json` is the hash of `SysOpsCommander.zip`, not of individual files. Compute it during the build/publish process:
   ```powershell
   (Get-FileHash .\SysOpsCommander.zip -Algorithm SHA256).Hash
   ```
   Include this in the `version.json` generation script.

4. **Semver comparison:** Use `System.Version` for comparison or a dedicated semver library. Be careful with `Version.Parse()` — it expects exactly major.minor.build[.revision] format. The `version.json` uses `major.minor.patch` — map patch to build.

5. **Settings unsaved changes tracking:** Each `partial void On{Property}Changed()` handler should set `HasUnsavedChanges = true`. Consider showing a "Discard Changes?" dialog when navigating away from Settings with unsaved changes.

6. **Update notification timing:** Check on startup (background, non-blocking) and allow manual check via Settings. Do NOT show a modal popup on startup — use the subtle status bar indicator. This respects the user's workflow without being intrusive.
