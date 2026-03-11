# Phase 5: Script Plugin System

> **Goal:** Build the script file discovery, manifest loading, export services, and deliver the 5 sample scripts. The `outputFormat` field is a **rendering hint only** — no output parsing logic.
>
> **Prereqs:** Phase 4 complete (execution engine exists). Phase 2 validators (`ManifestSchemaValidator`, `ScriptValidationService`) available.
>
> **Outputs:** `ScriptFileProvider`, `ScriptLoaderService`, `ExportService`, 5 sample scripts with 4 manifests, manifest schema reference.

---

## Sub-Steps

### 5.1 — Implement `ScriptFileProvider.cs`

**File:** `src/SysOpsCommander.Infrastructure/FileSystem/ScriptFileProvider.cs`

**Dependencies:**
- `ISettingsService` (to resolve script repository paths)
- `Serilog.ILogger`

**Methods:**

**`GetScriptDirectoriesAsync(CancellationToken ct)`:**
1. Resolve the effective script repository path from settings:
   - Per-user override if set
   - Org-wide default from `appsettings.json` if no override
   - Built-in `scripts/examples/` as always-included fallback
2. Return the list of directories to scan

**`ScanForScriptsAsync(CancellationToken ct)`:**
1. Get directories from `GetScriptDirectoriesAsync()`
2. For each directory, find all `.ps1` files recursively
3. Return `IReadOnlyList<ScriptFileInfo>` with:
   - `FullPath` — absolute path to the `.ps1` file
   - `ManifestPath` — corresponding `.json` file path (null if doesn't exist)
   - `LastModified` — file's last write time
4. Handle `DirectoryNotFoundException`, `UnauthorizedAccessException` gracefully — log and skip, don't crash

> **Improvement:** Add a `FileSystemWatcher` for live directory monitoring. When scripts are added/removed/modified in the repository, raise an event so the `ScriptLoaderService` can refresh. For v1, manual refresh via a "Refresh" button is sufficient; the `FileSystemWatcher` can be added later but the infrastructure should support it (event-based pattern).

**Commit:** `feat(infrastructure): implement ScriptFileProvider for script directory scanning`

---

### 5.2 — Implement `IScriptLoaderService` Interface

**File:** `src/SysOpsCommander.Core/Interfaces/IScriptLoaderService.cs`

```csharp
public interface IScriptLoaderService
{
    Task<IReadOnlyList<ScriptPlugin>> LoadAllScriptsAsync(CancellationToken ct);
    Task<ScriptPlugin> LoadScriptAsync(string scriptPath, CancellationToken ct);
    Task RefreshAsync(CancellationToken ct);
    event EventHandler<ScriptLibraryChangedEventArgs>? LibraryChanged;
}
```

**Commit:** `feat(core): define IScriptLoaderService interface`

---

### 5.3 — Implement `ScriptLoaderService.cs`

**File:** `src/SysOpsCommander.Services/ScriptLoaderService.cs`

**Dependencies:**
- `ScriptFileProvider`
- `ScriptValidationService`
- `Serilog.ILogger`

**`LoadAllScriptsAsync()` flow:**
1. Call `ScriptFileProvider.ScanForScriptsAsync()` to get all `.ps1` files
2. For each file, call `LoadScriptAsync()`
3. Return collection of `ScriptPlugin` objects
4. Cache the loaded collection internally for quick access

**`LoadScriptAsync(string scriptPath)` flow:**
1. Read the `.ps1` file content
2. Check for corresponding `.json` manifest (same name, different extension)
3. If manifest exists:
   a. Deserialize JSON into `ScriptManifest`
   b. Validate via `ManifestSchemaValidator.Validate()`
   c. Validate manifest-script pair via `ScriptValidationService.ValidateManifestPairAsync()`
4. If no manifest:
   a. Treat as "simple drop-in" script — no parameters, no structured metadata
   b. Create minimal `ScriptPlugin` with `Name = filename`, `Category = Uncategorized`
5. Run syntax validation via `ScriptValidationService.ValidateSyntaxAsync()`
6. Run dangerous pattern detection via `ScriptValidationService.DetectDangerousPatternsAsync()`
7. Assemble `ScriptPlugin`:
   ```csharp
   new ScriptPlugin
   {
       FilePath = scriptPath,
       Manifest = manifest,  // null if no manifest
       IsValidated = syntaxResult.IsValid,
       ValidationErrors = syntaxResult.Errors,
       DangerousPatterns = dangerousPatterns,
       EffectiveDangerLevel = ComputeEffectiveDangerLevel(manifest, dangerousPatterns),
       Content = scriptContent,
       LastModified = fileInfo.LastWriteTimeUtc
   };
   ```

**`EffectiveDangerLevel` resolution:**
- If manifest specifies `DangerLevel` → use it
- If dangerous patterns detected → use highest detected level
- Use whichever is higher (manifest or detection)
- Default: `ScriptDangerLevel.Safe`

**`RefreshAsync()`:**
1. Re-scan and re-load all scripts
2. Raise `LibraryChanged` event with added/removed/modified scripts

**Commit:** `feat(services): implement ScriptLoaderService with manifest and validation integration`

---

### 5.4 — Implement `IExportService` and `ExportService.cs`

**File:** `src/SysOpsCommander.Services/ExportService.cs`

**Dependencies:**
- `Serilog.ILogger`

**Methods:**

**`ExportToCsvAsync(IEnumerable<HostResult> results, string filePath, CancellationToken ct)`:**
```csharp
using var writer = new StreamWriter(filePath);
using var csv = new CsvWriter(writer, CultureInfo.InvariantCulture);
await csv.WriteRecordsAsync(results.Select(r => new
{
    r.Hostname,
    Status = r.Status.ToString(),
    r.Output,
    r.ErrorOutput,
    DurationMs = r.Duration.TotalMilliseconds
}), ct);
```

**`ExportToExcelAsync(IEnumerable<HostResult> results, string filePath, CancellationToken ct)`:**
```csharp
using var workbook = new XLWorkbook();
var worksheet = workbook.AddWorksheet("Results");
// Headers
worksheet.Cell(1, 1).Value = "Hostname";
worksheet.Cell(1, 2).Value = "Status";
worksheet.Cell(1, 3).Value = "Output";
worksheet.Cell(1, 4).Value = "Errors";
worksheet.Cell(1, 5).Value = "Duration (ms)";
// Data rows...
await Task.Run(() => workbook.SaveAs(filePath), ct);
```

**`ExportAuditLogToCsvAsync(IEnumerable<AuditLogEntry> entries, string filePath, CancellationToken ct)`:**
- Same pattern as host results, all audit log columns included

> **Improvement:** Add `ExportToClipboardAsync()` for quick copy-paste of results. Uses `Clipboard.SetText()` with tab-separated values for easy pasting into Excel/Teams/email.

**Commit:** `feat(services): implement ExportService for CSV and Excel export`

---

### 5.5 — Create Sample Script: `Get-InstalledSoftware.ps1`

**File:** `scripts/examples/Get-InstalledSoftware.ps1`

```powershell
#Requires -Version 5.1

param(
    [string]$NameFilter = '*'
)

Get-ItemProperty -Path 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\*',
                       'HKLM:\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\*' |
    Where-Object { $_.DisplayName -like $NameFilter } |
    Select-Object DisplayName, DisplayVersion, Publisher, InstallDate |
    Sort-Object DisplayName
```

**Commit:** `feat(scripts): add Get-InstalledSoftware sample script`

---

### 5.6 — Create Manifest: `Get-InstalledSoftware.json`

**File:** `scripts/examples/Get-InstalledSoftware.json`

```json
{
    "name": "Get-InstalledSoftware",
    "description": "Lists installed software from the Windows registry. Supports filtering by name.",
    "version": "1.0.0",
    "author": "SysOps Team",
    "category": "Inventory",
    "dangerLevel": "Safe",
    "outputFormat": "table",
    "parameters": [
        {
            "name": "NameFilter",
            "type": "string",
            "description": "Wildcard filter for software names",
            "required": false,
            "defaultValue": "*"
        }
    ]
}
```

**Commit:** `feat(scripts): add Get-InstalledSoftware manifest`

---

### 5.7 — Create Sample Script + Manifest: `Get-LocalAdmins`

**File:** `scripts/examples/Get-LocalAdmins.ps1`

```powershell
#Requires -Version 5.1

Get-LocalGroupMember -Group 'Administrators' |
    Select-Object Name, ObjectClass, PrincipalSource
```

**File:** `scripts/examples/Get-LocalAdmins.json`

```json
{
    "name": "Get-LocalAdmins",
    "description": "Lists members of the local Administrators group.",
    "version": "1.0.0",
    "author": "SysOps Team",
    "category": "Security",
    "dangerLevel": "Safe",
    "outputFormat": "table",
    "parameters": []
}
```

**Commit:** `feat(scripts): add Get-LocalAdmins sample script and manifest`

---

### 5.8 — Create Sample Script + Manifest: `Get-SecurityEventLog`

**File:** `scripts/examples/Get-SecurityEventLog.ps1`

```powershell
#Requires -Version 5.1

param(
    [int]$MaxEvents = 100,
    [string]$EventId = ''
)

$filterHash = @{
    LogName   = 'Security'
    MaxEvents = $MaxEvents
}

if ($EventId -ne '') {
    $filterHash['Id'] = [int]$EventId
}

Get-WinEvent -FilterHashtable $filterHash |
    Select-Object TimeCreated, Id, LevelDisplayName, Message |
    Sort-Object TimeCreated -Descending
```

**File:** `scripts/examples/Get-SecurityEventLog.json`

```json
{
    "name": "Get-SecurityEventLog",
    "description": "Retrieves recent Security event log entries. Optionally filter by Event ID.",
    "version": "1.0.0",
    "author": "SysOps Team",
    "category": "Security",
    "dangerLevel": "Safe",
    "outputFormat": "table",
    "parameters": [
        {
            "name": "MaxEvents",
            "type": "int",
            "description": "Maximum number of events to retrieve",
            "required": false,
            "defaultValue": "100"
        },
        {
            "name": "EventId",
            "type": "string",
            "description": "Filter by specific Event ID (e.g., 4624 for logon events)",
            "required": false,
            "defaultValue": ""
        }
    ]
}
```

**Commit:** `feat(scripts): add Get-SecurityEventLog sample script and manifest`

---

### 5.9 — Create Sample Script + Manifest: `Test-WinRMConnectivity`

**File:** `scripts/examples/Test-WinRMConnectivity.ps1`

```powershell
#Requires -Version 5.1

[PSCustomObject]@{
    Hostname        = $env:COMPUTERNAME
    WinRMRunning    = (Get-Service WinRM).Status -eq 'Running'
    PSVersion       = $PSVersionTable.PSVersion.ToString()
    OSVersion       = [System.Environment]::OSVersion.VersionString
    LastBootUpTime  = (Get-CimInstance Win32_OperatingSystem).LastBootUpTime
} | ConvertTo-Json -Depth 2
```

**File:** `scripts/examples/Test-WinRMConnectivity.json`

```json
{
    "name": "Test-WinRMConnectivity",
    "description": "Verifies WinRM is running and returns basic system info as JSON.",
    "version": "1.0.0",
    "author": "SysOps Team",
    "category": "Diagnostics",
    "dangerLevel": "Safe",
    "outputFormat": "json",
    "parameters": []
}
```

**Commit:** `feat(scripts): add Test-WinRMConnectivity sample script and manifest`

---

### 5.10 — Create Simple Drop-In Script: `Invoke-QuickScan.ps1`

**File:** `scripts/examples/Invoke-QuickScan.ps1`

```powershell
#Requires -Version 5.1

# Quick system info scan — no manifest, runs as a simple drop-in script
Write-Output "=== Quick System Scan ==="
Write-Output "Hostname: $env:COMPUTERNAME"
Write-Output "Domain: $env:USERDOMAIN"
Write-Output "OS: $((Get-CimInstance Win32_OperatingSystem).Caption)"
Write-Output "Uptime: $((Get-Date) - (Get-CimInstance Win32_OperatingSystem).LastBootUpTime)"
Write-Output "CPU: $((Get-CimInstance Win32_Processor).Name)"
Write-Output "RAM (GB): $([math]::Round((Get-CimInstance Win32_ComputerSystem).TotalPhysicalMemory / 1GB, 2))"
Write-Output "Disk Free (C:): $([math]::Round((Get-PSDrive C).Free / 1GB, 2)) GB"
```

> **No manifest:** This script demonstrates the "simple drop-in" pattern. `ScriptLoaderService` should load it with minimal metadata (`Category = Uncategorized`, `DangerLevel = Safe`, `OutputFormat = Text`).

**Commit:** `feat(scripts): add Invoke-QuickScan simple drop-in script (no manifest)`

---

### 5.11 — Create `manifest-schema.json` Reference

**File:** `scripts/manifest-schema.json`

```json
{
    "$schema": "http://json-schema.org/draft-07/schema#",
    "title": "SysOps Commander Script Manifest",
    "type": "object",
    "required": ["name", "description", "version", "author", "category"],
    "properties": {
        "name": { "type": "string", "description": "Display name of the script" },
        "description": { "type": "string", "description": "What the script does" },
        "version": { "type": "string", "pattern": "^\\d+\\.\\d+\\.\\d+$" },
        "author": { "type": "string" },
        "category": {
            "type": "string",
            "enum": ["Security", "Inventory", "Diagnostics", "Remediation", "Compliance", "Network", "Uncategorized"]
        },
        "dangerLevel": {
            "type": "string",
            "enum": ["Safe", "Caution", "Destructive"],
            "default": "Safe"
        },
        "outputFormat": {
            "type": "string",
            "enum": ["text", "table", "json"],
            "default": "text",
            "description": "UI rendering hint only — does not affect script output parsing"
        },
        "parameters": {
            "type": "array",
            "items": {
                "type": "object",
                "required": ["name", "type"],
                "properties": {
                    "name": { "type": "string" },
                    "type": { "type": "string", "enum": ["string", "int", "bool", "choice"] },
                    "description": { "type": "string" },
                    "required": { "type": "boolean", "default": false },
                    "defaultValue": { "type": "string" },
                    "choices": {
                        "type": "array",
                        "items": { "type": "string" },
                        "description": "Required when type is 'choice'"
                    }
                }
            }
        }
    }
}
```

**Commit:** `docs(scripts): add manifest-schema.json reference`

---

### 5.12 — Register Phase 5 Services in DI

**Action:** Update `ServiceCollectionExtensions.cs`:
```csharp
services.AddSingleton<ScriptFileProvider>();
services.AddSingleton<IScriptLoaderService, ScriptLoaderService>();
services.AddSingleton<IExportService, ExportService>();
```

**Commit:** `build(app): register Phase 5 services in DI container`

---

### 5.13 — Write Unit Tests — `ScriptLoaderService`

**File:** `tests/SysOpsCommander.Tests/Services/ScriptLoaderServiceTests.cs`

**Test cases (7+):**

| # | Test Name | Scenario | Verification |
|---|-----------|----------|-------------|
| 1 | `LoadScript_WithManifest_ParsesCorrectly` | Valid .ps1 + .json | `ScriptPlugin.Manifest` not null |
| 2 | `LoadScript_WithoutManifest_SimpleDropIn` | .ps1 only | Category = Uncategorized |
| 3 | `LoadScript_InvalidManifest_ReportsErrors` | Bad JSON schema | Validation errors populated |
| 4 | `LoadScript_SyntaxError_MarkedInvalid` | Bad .ps1 syntax | `IsValidated = false` |
| 5 | `LoadScript_DangerousPattern_SetsLevel` | `Remove-Item -Recurse -Force` | `EffectiveDangerLevel = Destructive` |
| 6 | `LoadAllScripts_MultipleDirectories_CombinesResults` | 2 dirs | Combined unique scripts |
| 7 | `LoadScript_MissingFile_HandlesGracefully` | Deleted file | Error handled, not thrown |

**Commit:** `test(services): add ScriptLoaderService tests`

---

### 5.14 — Write Unit Tests — `ExportService`

**File:** `tests/SysOpsCommander.Tests/Services/ExportServiceTests.cs`

**Test cases (4+):**

| # | Test Name | Scenario | Verification |
|---|-----------|----------|-------------|
| 1 | `ExportToCsv_ValidResults_CreatesFile` | 3 results | CSV file exists with 3 data rows |
| 2 | `ExportToExcel_ValidResults_CreatesWorkbook` | 3 results | Excel file exists with data |
| 3 | `ExportToCsv_EmptyResults_CreatesHeaderOnly` | 0 results | CSV with headers, no data rows |
| 4 | `ExportAuditLog_AllColumns_Included` | Audit entries | All columns present |

**Use temp directories for test file output; clean up in `Dispose()`.**

**Commit:** `test(services): add ExportService tests`

---

### 5.15 — Phase 5 Verification

**Full acceptance criteria check:**
- [ ] Script directories resolved from settings (per-user > org-wide > built-in examples)
- [ ] `.ps1` files discovered recursively in all script directories
- [ ] Manifests loaded and validated when present
- [ ] Drop-in scripts (no manifest) treated as `Uncategorized / Safe / Text`
- [ ] Dangerous patterns detected and reflected in `EffectiveDangerLevel`
- [ ] All 5 sample scripts load correctly:
  - `Get-InstalledSoftware` — with manifest, category: Inventory
  - `Get-LocalAdmins` — with manifest, category: Security
  - `Get-SecurityEventLog` — with manifest, category: Security
  - `Test-WinRMConnectivity` — with manifest, outputFormat: json
  - `Invoke-QuickScan` — no manifest, simple drop-in
- [ ] `outputFormat` is stored but NOT used for parsing (rendering hint only)
- [ ] CSV export produces well-formed files
- [ ] Excel export produces openable `.xlsx` files
- [ ] All unit tests pass (11+ new cases)
- [ ] `dotnet build` — zero warnings
- [ ] `dotnet test` — all tests pass (Phase 0–5)
- [ ] Final commit: `chore: complete Phase 5 — script plugin system`

---

## Improvements & Notes

1. **`FileSystemWatcher` for live refresh (step 5.1):** For v1, the "Refresh" button in the Script Library view triggers `ScriptLoaderService.RefreshAsync()`. For v2, add a `FileSystemWatcher` on each script directory that raises `LibraryChanged` when files are added, removed, or modified. The event-based pattern is already in the interface (`LibraryChanged` event) — the infrastructure just needs the watcher implementation.

2. **Clipboard export (step 5.4):** Add `ExportToClipboardAsync()` that copies results as tab-separated values. This enables quick paste into Excel, Teams, or email without file round-tripping. Uses `Clipboard.SetText()` on the UI thread.

3. **`LastModified` property on `ScriptPlugin` (step 5.1):** Added to `ScriptFileInfo` and `ScriptPlugin` to support sorting scripts by recency and detecting changes during refresh. The plan mentions `ScriptPlugin` properties but doesn't list `LastModified` — it's a natural addition.

4. **`outputFormat` as rendering hint — reiteration:** The plan's Critical Technical Notes section is very clear: `outputFormat` is a UI rendering hint, NOT a parsing directive. The `ScriptLoaderService` stores the value, and the UI (Phase 8) uses it to select the display component. No output parsing logic should be built. If `outputFormat = "json"`, the UI attempts `JSON.parse()` and renders as a tree if successful, otherwise falls back to raw text.

5. **Script content loading strategy:** Loading all script content into memory (`ScriptPlugin.Content`) on startup could be expensive for large repositories. Consider lazy loading — only read the file content when the script is selected for execution or validation. For v1 with a small number of scripts, eager loading is fine.
