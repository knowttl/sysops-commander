# SysOps Commander — Implementation Plan (Rev 2)

> **Purpose:** This document is a phased implementation plan for an AI coding agent to build the SysOps Commander application step by step. Each phase is self-contained with clear inputs, outputs, acceptance criteria, and technical guidance. Phases must be completed in order — each builds on the previous.
>
> **Reference Document:** SysOpsCommander_DesignDocument.docx (v1.0, March 2026)
>
> **Source Control:** Git repository (GitHub or Azure DevOps). Include a `.gitignore` for .NET (bin/, obj/, .vs/, *.user, *.suo, packages/) from Phase 0. All phases should produce atomic, well-described commits.

---

## Revision History

| Rev | Date | Changes |
|-----|------|---------|
| 1 | 2026-03-11 | Initial implementation plan |
| 2 | 2026-03-11 | Multi-domain AD support; WinRM auth/transport configurability (Kerberos, NTLM, CredSSP); parameter injection via AddParameter(); PS SDK version clarification; org-wide config via appsettings.json; configurable stale threshold; toast notifications; auto-update fleshed out; outputFormat rendering hint clarification |

---

## Design Decisions (Resolved)

| Decision | Answer |
|----------|--------|
| Shared script repository path | Org-wide default in `appsettings.json`, per-user override in Settings |
| Max concurrent target hosts | No practical upper limit — must stream results to disk |
| Audit log SIEM export | Deferred to v2 (Splunk integration) |
| SYSTEM context execution | Not needed — user-context + alternate credentials only |
| Application updates | Auto-update from network share (v1) |
| AD domain scope | Default to current user's domain; allow switching to any reachable domain |
| WinRM authentication | User-selectable: Kerberos (default), NTLM, CredSSP. CredSSP validated before use |
| WinRM transport | HTTP (5985) default; HTTPS (5986) configurable per-host or globally |
| Stale computer threshold | Configurable in settings, default 90 days |
| Source control | Git repository (GitHub or Azure DevOps) |
| Org-wide config source | `appsettings.json` bundled with the application |

---

## Critical Technical Notes

> **READ BEFORE STARTING ANY PHASE.** These are cross-cutting concerns that affect the entire codebase.

### PowerShell SDK vs Remote PowerShell Version

The application hosts the **PowerShell 7.x SDK** (`Microsoft.PowerShell.SDK`) locally for two purposes: (1) AST parsing for script validation, and (2) creating `Runspace` and `PowerShell` pipeline objects for remote execution. However, **remote execution via WinRM connects to whatever PowerShell version is installed on the target host** — typically Windows PowerShell 5.1 on most enterprise Windows machines. The SDK version on the app side does NOT determine the remote execution version.

Implications:
- All sample scripts use `#Requires -Version 5.1` for maximum compatibility
- AST parsing may flag PS 7-only syntax that would fail on PS 5.1 targets — this is a feature, not a bug (it catches compatibility issues)
- Do NOT set `PSVersion` on `WSManConnectionInfo` unless the user explicitly requests it
- The manifest schema does not include a `psVersion` field in v1 — add if needed in v2

### Parameter Injection — NEVER Use String Interpolation

When executing a structured script plugin with parameters defined in its manifest, pass parameters using the PowerShell SDK's `AddParameter()` method on the `PowerShell` pipeline object:

```csharp
// CORRECT — safe parameter injection
using var ps = PowerShell.Create();
ps.AddScript(scriptContent);
foreach (var param in parameters)
{
    ps.AddParameter(param.Key, param.Value);
}
var results = await ps.InvokeAsync();
```

```csharp
// WRONG — injection risk, string escaping nightmare
var modified = $"$NameFilter = '{userInput}'\n{scriptContent}";  // NEVER DO THIS
```

This applies to both local AST validation and remote execution via `Invoke-Command -ScriptBlock`.

### outputFormat Is a Rendering Hint Only

The `outputFormat` field in the JSON manifest (`"text"`, `"table"`, `"json"`) is a **UI rendering hint**, not a parsing directive. The app does NOT parse `Format-Table` output back into structured columns. Instead:
- `"text"` → display raw output in a monospace text block
- `"table"` → display raw output in a monospace text block with slightly wider default width (hint that the output is tabular)
- `"json"` → attempt `JSON.parse()` on the output and render as a formatted tree/grid. If parsing fails, fall back to raw text display

If structured output parsing is needed in v2, scripts should use `ConvertTo-Json` and the app should parse the JSON. `Format-Table` output is not machine-parseable over WinRM.

### IHostTargetingService Is a Shared Singleton

`IHostTargetingService` must be registered as a **singleton** in the DI container. It holds the current working set of target hosts and is shared between the AD Explorer (which adds hosts via "Send to Execution Targets") and the Execution view (which consumes them). This is the explicit mechanism for cross-view communication. ViewModels should inject this service and subscribe to its `CollectionChanged` events.

---

## Technology Stack (Locked)

| Component | Package/Version | NuGet Package |
|-----------|----------------|---------------|
| Runtime | .NET 8 (LTS) | — |
| UI Framework | WPF | — |
| Language | C# 12 | — |
| MVVM Toolkit | CommunityToolkit.Mvvm | `CommunityToolkit.Mvvm` (8.x) |
| Dependency Injection | MS DI | `Microsoft.Extensions.DependencyInjection` |
| Configuration | MS Configuration | `Microsoft.Extensions.Configuration.Json` |
| AD Integration | DirectoryServices | `System.DirectoryServices` + `System.DirectoryServices.Protocols` |
| PowerShell SDK | PS 7.x SDK | `Microsoft.PowerShell.SDK` |
| WMI | System.Management | `System.Management` |
| SQLite | Dapper + MS Sqlite | `Microsoft.Data.Sqlite` + `Dapper` |
| Logging | Serilog | `Serilog.Sinks.File` + `Serilog.Sinks.Console` + `Serilog.Formatting.Compact` |
| Toast Notifications | MS Toolkit Notifications | `Microsoft.Toolkit.Uwp.Notifications` |
| Excel Export | ClosedXML | `ClosedXML` |
| CSV Export | CsvHelper | `CsvHelper` |
| Unit Testing | xUnit + NSubstitute | `xUnit` + `NSubstitute` + `FluentAssertions` |
| Roslyn Analyzers | Standard | `Microsoft.CodeAnalysis.NetAnalyzers` |

---

## Solution Structure

```
SysOpsCommander/
├── .gitignore
├── SysOpsCommander.sln
├── src/
│   ├── SysOpsCommander.Core/
│   │   ├── Interfaces/
│   │   │   ├── IActiveDirectoryService.cs
│   │   │   ├── IRemoteExecutionService.cs
│   │   │   ├── IScriptLoaderService.cs
│   │   │   ├── IHostTargetingService.cs       # SINGLETON — shared across views
│   │   │   ├── ICredentialService.cs
│   │   │   ├── IAuditLogService.cs
│   │   │   ├── IExportService.cs
│   │   │   ├── ISettingsService.cs
│   │   │   ├── IExecutionStrategy.cs
│   │   │   ├── IAutoUpdateService.cs
│   │   │   └── INotificationService.cs
│   │   ├── Models/
│   │   │   ├── AdObject.cs
│   │   │   ├── AdSearchResult.cs
│   │   │   ├── ExecutionJob.cs
│   │   │   ├── HostTarget.cs
│   │   │   ├── HostResult.cs
│   │   │   ├── ScriptPlugin.cs
│   │   │   ├── ScriptManifest.cs
│   │   │   ├── AuditLogEntry.cs
│   │   │   ├── UserSettings.cs
│   │   │   ├── DomainConnection.cs            # NEW — multi-domain support
│   │   │   └── WinRmConnectionOptions.cs      # NEW — auth/transport config
│   │   ├── Enums/
│   │   │   ├── ExecutionStatus.cs
│   │   │   ├── HostStatus.cs
│   │   │   ├── ExecutionType.cs
│   │   │   ├── ScriptDangerLevel.cs
│   │   │   ├── OutputFormat.cs
│   │   │   ├── WinRmAuthMethod.cs             # NEW — Kerberos, NTLM, CredSSP
│   │   │   └── WinRmTransport.cs              # NEW — HTTP, HTTPS
│   │   ├── Constants/
│   │   │   └── AppConstants.cs
│   │   └── Validation/
│   │       ├── HostnameValidator.cs
│   │       ├── LdapFilterSanitizer.cs
│   │       └── ManifestSchemaValidator.cs
│   │
│   ├── SysOpsCommander.Services/
│   │   ├── ActiveDirectoryService.cs
│   │   ├── RemoteExecutionService.cs
│   │   ├── Strategies/
│   │   │   ├── PowerShellRemoteStrategy.cs
│   │   │   └── WmiQueryStrategy.cs
│   │   ├── ScriptLoaderService.cs
│   │   ├── ScriptValidationService.cs
│   │   ├── HostTargetingService.cs
│   │   ├── CredentialService.cs
│   │   ├── ExportService.cs
│   │   ├── AutoUpdateService.cs
│   │   └── NotificationService.cs             # NEW — toast notifications
│   │
│   ├── SysOpsCommander.Infrastructure/
│   │   ├── Database/
│   │   │   ├── DatabaseInitializer.cs
│   │   │   ├── AuditLogRepository.cs
│   │   │   └── SettingsRepository.cs
│   │   ├── Logging/
│   │   │   ├── SerilogConfigurator.cs
│   │   │   └── CredentialDestructuringPolicy.cs
│   │   └── FileSystem/
│   │       └── ScriptFileProvider.cs
│   │
│   ├── SysOpsCommander.ViewModels/
│   │   ├── MainWindowViewModel.cs
│   │   ├── DashboardViewModel.cs
│   │   ├── AdExplorerViewModel.cs
│   │   ├── AdSearchViewModel.cs
│   │   ├── ExecutionViewModel.cs
│   │   ├── ScriptLibraryViewModel.cs
│   │   ├── AuditLogViewModel.cs
│   │   ├── SettingsViewModel.cs
│   │   └── Dialogs/
│   │       ├── CredentialDialogViewModel.cs
│   │       └── DomainSelectorViewModel.cs     # NEW
│   │
│   └── SysOpsCommander.App/
│       ├── App.xaml / App.xaml.cs
│       ├── appsettings.json                   # NEW — org-wide defaults
│       ├── MainWindow.xaml / .cs
│       ├── Views/
│       │   ├── DashboardView.xaml
│       │   ├── AdExplorerView.xaml
│       │   ├── ExecutionView.xaml
│       │   ├── ScriptLibraryView.xaml
│       │   ├── AuditLogView.xaml
│       │   └── SettingsView.xaml
│       ├── Dialogs/
│       │   ├── CredentialDialog.xaml
│       │   └── DomainSelectorDialog.xaml      # NEW
│       ├── Converters/
│       │   ├── StatusToColorConverter.cs
│       │   └── BoolToVisibilityConverter.cs
│       ├── Resources/
│       │   └── Styles.xaml
│       └── DependencyInjection/
│           └── ServiceCollectionExtensions.cs
│
├── tests/
│   └── SysOpsCommander.Tests/
│       ├── ViewModels/
│       ├── Services/
│       ├── Validation/
│       ├── Infrastructure/
│       └── Security/
│
└── scripts/
    ├── examples/
    │   ├── Get-InstalledSoftware.ps1
    │   ├── Get-InstalledSoftware.json
    │   ├── Get-LocalAdmins.ps1
    │   ├── Get-LocalAdmins.json
    │   ├── Get-SecurityEventLog.ps1
    │   ├── Get-SecurityEventLog.json
    │   ├── Test-WinRMConnectivity.ps1
    │   ├── Test-WinRMConnectivity.json
    │   └── Invoke-QuickScan.ps1               # Simple drop-in (no manifest)
    └── manifest-schema.json
```

---

## Sample Script Plugins (Reference Implementations)

> Unchanged from Rev 1. See the 5 sample scripts (Get-InstalledSoftware, Get-LocalAdmins, Get-SecurityEventLog, Test-WinRMConnectivity, Invoke-QuickScan) and the manifest-schema.json defined in the original plan. Include all 5 in `scripts/examples/`.

---

## Implementation Phases

### Phase 0: Project Scaffolding & Foundation

**Goal:** Create the solution structure, wire dependency injection, configure logging and application configuration, and verify the build pipeline works end-to-end. No features yet — just a solid skeleton that every subsequent phase builds on.

**Why this is first:** Every subsequent phase depends on DI, logging, configuration, and the project structure being in place.

#### Tasks

1. **Initialize the Git repository** with a `.gitignore` for .NET projects (bin/, obj/, .vs/, *.user, *.suo, packages/, *.db). Create an initial commit with the empty solution structure.

2. **Create the .NET 8 solution and all 6 projects** matching the solution structure above. Set up project references:
   - `Core` has no project references (it's the dependency root)
   - `Services` references `Core`
   - `Infrastructure` references `Core`
   - `ViewModels` references `Core`
   - `App` references `Core`, `ViewModels`, `Services`, `Infrastructure`
   - `Tests` references `Core`, `ViewModels`, `Services`, `Infrastructure`

3. **Install NuGet packages** per the technology stack table. Pin versions explicitly — do not use floating versions.

4. **Create `appsettings.json`** in the `App` project (set to Copy to Output Directory):
   ```json
   {
     "SysOpsCommander": {
       "SharedScriptRepositoryPath": "",
       "UpdateNetworkSharePath": "",
       "DefaultDomain": "",
       "DefaultWinRmTransport": "HTTP",
       "DefaultWinRmAuthMethod": "Kerberos",
       "DefaultThrottle": 5,
       "DefaultTimeoutSeconds": 60,
       "StaleComputerThresholdDays": 90,
       "AuditLogRetentionDays": 365
     }
   }
   ```
   Wire `Microsoft.Extensions.Configuration.Json` to load this file. Bind to a strongly-typed `AppConfiguration` class via `IOptions<AppConfiguration>` or direct binding. This file represents the org-wide defaults that ship with the application. Per-user overrides are stored in SQLite (Phase 1).

5. **Configure the DI composition root** in `App.xaml.cs`:
   - Load `appsettings.json` via `ConfigurationBuilder`
   - Register `AppConfiguration` as a singleton
   - Register all service interfaces → concrete implementations
   - Register `IHostTargetingService` as a **singleton** (shared across views)
   - Register all other services with appropriate lifetimes (most as singletons for desktop app)
   - Register all ViewModels as transient
   - Use `IServiceProvider` to resolve the `MainWindow` and its `DataContext`
   - Create `ServiceCollectionExtensions.cs` with `AddSysOpsServices()`, `AddSysOpsViewModels()`, `AddSysOpsInfrastructure()` extension methods

6. **Configure Serilog** in `SerilogConfigurator.cs`:
   - Rolling file sink → `%LOCALAPPDATA%\SysOpsCommander\Logs\sysops-{Date}.log`
   - Compact JSON format for structured output
   - Console sink for debug builds
   - Default level: `Information` (configurable via settings later)
   - Register the `CredentialDestructuringPolicy` that replaces `SecureString`, `PSCredential`, and `NetworkCredential` properties with `"[REDACTED]"` in all log output
   - Wire into the DI container as `ILogger` (Serilog's interface)
   - **Enrichers:** `Enrich.WithMachineName()`, `Enrich.WithThreadId()`, custom `CorrelationIdEnricher`

7. **Create the `AppConstants.cs`** file with:
   ```csharp
   public static class AppConstants
   {
       public const string AppName = "SysOps Commander";
       public const string AppDataFolder = "SysOpsCommander";
       public const int DefaultThrottle = 5;
       public const int DefaultWinRmTimeoutSeconds = 60;
       public const int DefaultAdQueryTimeoutSeconds = 30;
       public const int MaxResultsPerPage = 500;
       public const int WinRmHttpPort = 5985;
       public const int WinRmHttpsPort = 5986;
       public const int AuditLogRetentionDays = 365;
       public const int DefaultStaleComputerDays = 90;
       public const int ReachabilityCheckParallelism = 20;
       public const long MaxInMemoryResultBytes = 10 * 1024 * 1024; // 10MB
       public const string DefaultScriptCategory = "Uncategorized";
   }
   ```

8. **Create a minimal `MainWindow.xaml`** with:
   - A sidebar (collapsed for now, just a `StackPanel` with placeholder buttons)
   - A `ContentControl` in the main area bound to a `CurrentView` property on `MainWindowViewModel`
   - Verify the app launches, the DI container resolves, and Serilog writes a startup log entry

9. **Wire global exception handlers** in `App.xaml.cs`:
   - `DispatcherUnhandledException` → log to Serilog, show a `MessageBox` with correlation ID, offer "Continue" or "Exit"
   - `AppDomain.CurrentDomain.UnhandledException` → log to Serilog
   - `TaskScheduler.UnobservedTaskException` → log to Serilog

10. **Create a smoke test** in `SysOpsCommander.Tests`:
    - Verify DI container builds without errors
    - Verify `CredentialDestructuringPolicy` replaces `SecureString` values with `"[REDACTED]"`
    - Verify `appsettings.json` loads and binds to `AppConfiguration` correctly

#### Acceptance Criteria
- [ ] Git repository initialized with .gitignore and initial commit
- [ ] Solution builds with zero warnings
- [ ] Application launches and displays a blank window with sidebar skeleton
- [ ] `appsettings.json` loads and binds to `AppConfiguration`
- [ ] Serilog writes a `sysops-{date}.log` file to `%LOCALAPPDATA%\SysOpsCommander\Logs\`
- [ ] Global exception handler catches a test exception and shows a dialog with correlation ID
- [ ] `CredentialDestructuringPolicy` unit test passes
- [ ] `IHostTargetingService` is registered as a singleton (verified by test)
- [ ] All 6 projects compile and reference each other correctly
- [ ] Roslyn analyzers are active and produce no warnings

---

### Phase 1: Core Models, Settings, and Database Infrastructure

**Goal:** Build the data layer — all models/DTOs, the SQLite database, settings persistence, and the repository pattern. Includes the new models for multi-domain and WinRM configurability.

#### Tasks

1. **Define all Core models and enums** including the new connection models:
   - `AdObject` — generic AD object representation (DN, ObjectClass, Name, dictionary of attributes)
   - `AdSearchResult` — wraps a list of `AdObject` with metadata (query, execution time, result count)
   - `ExecutionJob` — represents a single execution run (ID, script, parameters, target hosts, status, start/end time, results collection, `WinRmConnectionOptions`)
   - `HostTarget` — hostname + reachability status + validation status
   - `HostResult` — per-host execution result (hostname, status enum, output text, error text, duration)
   - `ScriptPlugin` — represents a loaded script (file path, manifest if present, is-validated, validation errors)
   - `ScriptManifest` — strongly-typed deserialization of the JSON manifest schema
   - `AuditLogEntry` — maps to the SQLite AuditLog table columns. Includes `WinRmAuthMethod` and `WinRmTransport` fields
   - `UserSettings` — key-value setting with typed accessor methods
   - **NEW: `DomainConnection`** — represents a target AD domain: domain name, domain controller FQDN (optional, for explicit DC targeting), `DirectoryEntry` root path, IsCurrentDomain flag
   - **NEW: `WinRmConnectionOptions`** — per-execution connection config: `WinRmAuthMethod` enum, `WinRmTransport` enum, custom port (optional), shell URI (optional). Defaults loaded from `AppConfiguration`, overridable per-run in the Execution view

2. **Define all Core enums:**
   - `ExecutionStatus`: Pending, Validating, Running, Completed, PartialFailure, Failed, Cancelled
   - `HostStatus`: Pending, Reachable, Unreachable, Running, Success, Failed, Timeout, Cancelled, Skipped
   - `ExecutionType`: PowerShell, WMI
   - `ScriptDangerLevel`: Safe, Caution, Destructive
   - `OutputFormat`: Text, Table, Json
   - **NEW: `WinRmAuthMethod`**: Kerberos, NTLM, CredSSP
   - **NEW: `WinRmTransport`**: HTTP, HTTPS

3. **Define all Core service interfaces** with full method signatures. Key changes:
   ```
   IActiveDirectoryService:
     - Task<IReadOnlyList<DomainConnection>> GetAvailableDomainsAsync(CancellationToken ct)
     - Task SetActiveDomainAsync(DomainConnection domain, CancellationToken ct)
     - DomainConnection GetActiveDomain()
     // ... plus all search/browse methods from original plan

   ISettingsService:
     - Task<string> GetAsync(string key, string defaultValue)
     - Task SetAsync(string key, string value)
     - Task<T> GetTypedAsync<T>(string key, T defaultValue)
     - string GetOrgDefault(string key)  // reads from AppConfiguration
     - Task<string> GetEffectiveAsync(string key)  // returns per-user override if set, otherwise org default

   INotificationService:  // NEW
     - void ShowToast(string title, string message, NotificationSeverity severity)
     - void ShowExecutionComplete(ExecutionJob job)  // rich notification with result summary

   IAuditLogService:
     - Task LogExecutionAsync(AuditLogEntry entry)
     - Task<IReadOnlyList<AuditLogEntry>> QueryAsync(AuditLogFilter filter)
     - Task PurgeOldEntriesAsync(int retentionDays)
   ```

4. **Implement `DatabaseInitializer.cs`:**
   - Create SQLite database at `%LOCALAPPDATA%\SysOpsCommander\audit.db`
   - Create `AuditLog` table with the additional columns: `WinRmAuthMethod TEXT`, `WinRmTransport TEXT`, `TargetDomain TEXT`
   - Create `UserSettings` table
   - Run on app startup, use `IF NOT EXISTS` for idempotency
   - Add a `SchemaVersion` table for future migrations

5. **Implement `SettingsRepository.cs` and `AuditLogRepository.cs`** using Dapper.

6. **Implement `ISettingsService`** concrete class:
   - Org-wide defaults read from `AppConfiguration` (sourced from `appsettings.json`)
   - Per-user overrides stored in SQLite `UserSettings` table
   - `GetEffectiveAsync(key)` returns: per-user override if it exists, otherwise org default from `appsettings.json`
   - Settings keys include: `SharedScriptRepositoryPath`, `DefaultThrottle`, `DefaultTimeoutSeconds`, `DefaultWinRmTransport`, `DefaultWinRmAuthMethod`, `StaleComputerThresholdDays`, `UpdateNetworkSharePath`, `LogLevel`

7. **Write unit tests:**
   - Model serialization/deserialization (especially `ScriptManifest`, `WinRmConnectionOptions`)
   - `AuditLogRepository` CRUD against in-memory SQLite (including new columns)
   - `SettingsRepository` read/write/update
   - Settings layering: per-user override takes precedence over org default; absent override falls through to org default; absent org default falls through to `AppConstants` hard default

#### Acceptance Criteria
- [ ] All models, enums, and interfaces compile with XML documentation
- [ ] `DomainConnection` and `WinRmConnectionOptions` models correctly represent multi-domain and auth config
- [ ] `WinRmAuthMethod` and `WinRmTransport` enums include all three auth methods and both transports
- [ ] SQLite database is created on first run with correct schema (including new columns)
- [ ] Settings layering works: per-user override > appsettings.json org default > AppConstants hard default
- [ ] Audit log entries include WinRM auth/transport/domain fields
- [ ] `ScriptManifest` correctly deserializes all 5 example JSON manifests
- [ ] All unit tests pass

---

### Phase 2: Validation Framework

**Goal:** Build all validation logic before the features that depend on it.

#### Tasks

1. **Implement `HostnameValidator.cs`** in `Core/Validation/`:
   - Validate NetBIOS names (max 15 chars, allowed: alphanumeric + hyphens, cannot start/end with hyphen)
   - Validate FQDN format (proper dot-separated labels, each label 1-63 chars, total max 253)
   - Validate IPv4 address format (four octets 0-255)
   - Return `ValidationResult` with success/failure and error message
   - Reject empty strings, whitespace, strings with injection characters (`;`, `|`, `&`, `$`, `` ` ``, `(`, `)`)

2. **Implement `LdapFilterSanitizer.cs`** in `Core/Validation/`:
   - Escape special LDAP characters per RFC 4515: `*` → `\2a`, `(` → `\28`, `)` → `\29`, `\` → `\5c`, NUL → `\00`
   - `SanitizeInput(string raw)` → `string sanitized`
   - `BuildSafeFilter(string attribute, string value)` → `string ldapFilter`

3. **Implement `ManifestSchemaValidator.cs`** in `Core/Validation/`:
   - Required fields present: name, description, version, author, category
   - Version matches semver pattern `^\d+\.\d+\.\d+$`
   - Category is one of the allowed enum values
   - Parameter types are valid enum values
   - Choice parameters have non-empty choices array
   - No duplicate parameter names
   - Return `ManifestValidationResult` with list of errors/warnings

4. **Implement `ScriptValidationService.cs`** in `Services/`:
   - `ValidateSyntax(string scriptPath)` → PowerShell AST parsing (`System.Management.Automation.Language.Parser.ParseFile`). Return list of parse errors with line/column/message
   - `DetectDangerousPatterns(string scriptPath)` → AST walker scanning for: `Remove-Item` with `-Recurse` + `-Force`, `Format-Volume`, `Stop-Computer`, `Restart-Computer`, `Clear-EventLog`, `Set-ExecutionPolicy`, `Disable-NetAdapter`, `Stop-Service` on critical services. Return warnings with line numbers
   - `ValidateManifestPair(string ps1Path)` → check JSON manifest exists and is valid; check that parameter names in manifest match the `param()` block in the script (warning if mismatched — the script is authoritative, the manifest is documentation)
   - **NEW: `ValidateCredSspAvailability(string hostname)`** → test whether CredSSP is configured on the target host. Return clear error message if not: "CredSSP authentication is not configured on {host}. This requires GPO configuration on both client and server. See: https://learn.microsoft.com/en-us/powershell/module/microsoft.wsman.management/enable-wsmancredssp"

5. **Write comprehensive unit tests:**
   - `HostnameValidator`: valid NetBIOS, valid FQDN, valid IP, injection characters rejected, boundary cases (empty, max length, leading/trailing hyphens)
   - `LdapFilterSanitizer`: all 5 special characters escaped, nested injection attempts blocked, empty input handled
   - `ManifestSchemaValidator`: valid manifests pass, missing required fields caught, invalid parameter types caught, duplicate param names caught
   - `ScriptValidationService`: valid .ps1 passes, syntax errors with line numbers, dangerous patterns detected, manifest-script parameter mismatch
   - All validators: aim for 25+ test cases total

#### Acceptance Criteria
- [ ] Hostname validation correctly accepts/rejects all expected patterns including injection characters
- [ ] LDAP sanitizer escapes all RFC 4515 special characters
- [ ] Manifest validator catches all schema violations
- [ ] Script syntax validation returns parse errors with line numbers
- [ ] Dangerous pattern detection identifies all specified cmdlets
- [ ] CredSSP availability check returns clear error when not configured
- [ ] All validation unit tests pass (25+ cases)

---

### Phase 3: Active Directory Service Layer

**Goal:** Build the full AD integration with **multi-domain support** — quick search, tree browsing, attribute viewing, pre-built security filters, and the ability to switch between reachable domains.

#### Tasks

1. **Implement `IActiveDirectoryService`** interface:
   ```
   // Domain management
   Task<IReadOnlyList<DomainConnection>> GetAvailableDomainsAsync(CancellationToken ct)
   Task SetActiveDomainAsync(DomainConnection domain, CancellationToken ct)
   DomainConnection GetActiveDomain()

   // Search
   Task<AdSearchResult> SearchAsync(string searchTerm, CancellationToken ct)
   Task<AdSearchResult> SearchWithFilterAsync(string ldapFilter, CancellationToken ct)

   // Browse
   Task<IReadOnlyList<AdObject>> BrowseChildrenAsync(string parentDn, CancellationToken ct)
   Task<AdObject> GetObjectDetailAsync(string distinguishedName, CancellationToken ct)

   // Group membership
   Task<IReadOnlyList<string>> GetGroupMembershipAsync(string objectDn, bool recursive, CancellationToken ct)

   // Pre-built security filters
   Task<AdSearchResult> GetLockedAccountsAsync(CancellationToken ct)
   Task<AdSearchResult> GetDisabledComputersAsync(CancellationToken ct)
   Task<AdSearchResult> GetStaleComputersAsync(int daysInactive, CancellationToken ct)
   Task<IReadOnlyList<string>> GetDomainControllersAsync(CancellationToken ct)
   ```

2. **Implement `ActiveDirectoryService.cs`:**
   - **Multi-domain support:**
     - On initialization, detect the current user's domain from `Environment.UserDomainName` and `Domain.GetCurrentDomain()`
     - `GetAvailableDomainsAsync()` → use `Forest.GetCurrentForest().Domains` to enumerate trusted domains. Also allow manual domain entry for cross-forest scenarios
     - `SetActiveDomainAsync()` → update the internal `DirectoryEntry` root to the selected domain's root DN. All subsequent queries target this domain. Store as `DomainConnection` object
     - **Default behavior:** app starts connected to the current user's domain. A domain selector (dropdown or dialog) in the UI allows switching
   - **Quick Search:** LDAP filter searching `sAMAccountName`, `cn`, `displayName`, `mail`, `dNSHostName`. All user input sanitized via `LdapFilterSanitizer`
   - **Tree Browse:** `SearchScope.OneLevel` from the parent DN, lazy loading on expand
   - **Attribute Detail:** load all attributes via `DirectoryEntry.Properties`
   - **Pre-built Filters:**
     - Locked accounts: `(&(objectClass=user)(lockoutTime>=1))`
     - Disabled computers: `(&(objectClass=computer)(userAccountControl:1.2.840.113556.1.4.803:=2))`
     - Stale computers: `(&(objectClass=computer)(lastLogonTimestamp<={daysAgoFileTime}))` — days threshold loaded from `ISettingsService.GetEffectiveAsync("StaleComputerThresholdDays")`, default 90
   - **Group Membership:** `tokenGroups` attribute for recursive, `memberOf` for direct
   - All methods accept `CancellationToken` and enforce configurable timeout
   - Results paginated with `AppConstants.MaxResultsPerPage`

3. **Implement key AD attribute mapping** (unchanged from Rev 1 — users, computers, groups).

4. **Write unit tests:**
   - Quick search returns expected results for partial match
   - Pre-built filters generate correct LDAP filter strings
   - Stale computer filter uses configurable threshold (not hardcoded)
   - Domain switching updates the search root correctly
   - LDAP injection in search terms is sanitized
   - Cancellation and timeout enforced

#### Acceptance Criteria
- [ ] App detects and connects to the current user's domain on startup
- [ ] Available domains are enumerable (from forest trusts)
- [ ] Domain switching updates all subsequent queries to target the new domain
- [ ] Quick search works for users, computers, and groups with partial matching
- [ ] Pre-built security filters work, with configurable stale threshold
- [ ] Tree browsing loads lazily
- [ ] Full attribute detail loads for selected objects
- [ ] All user input is sanitized
- [ ] All unit tests pass

---

### Phase 4: Remote Execution Engine

**Goal:** Build the core execution engine with **configurable WinRM authentication (Kerberos, NTLM, CredSSP) and transport (HTTP/HTTPS)**. This is the highest-risk technical component.

#### Tasks

1. **Define the `IExecutionStrategy` interface:**
   ```csharp
   public interface IExecutionStrategy
   {
       ExecutionType Type { get; }
       Task<HostResult> ExecuteAsync(
           string hostname,
           string scriptContent,
           IDictionary<string, object>? parameters,
           PSCredential? credential,
           WinRmConnectionOptions connectionOptions,  // NEW — auth + transport config
           int timeoutSeconds,
           CancellationToken ct);
   }
   ```

2. **Implement `PowerShellRemoteStrategy.cs`:**
   - Create `WSManConnectionInfo` for the target host with **configurable auth and transport:**
     ```csharp
     var connInfo = new WSManConnectionInfo(
         useSsl: options.Transport == WinRmTransport.HTTPS,
         hostname,
         options.Transport == WinRmTransport.HTTPS ? AppConstants.WinRmHttpsPort : AppConstants.WinRmHttpPort,
         "/wsman",
         "http://schemas.microsoft.com/powershell/Microsoft.PowerShell",
         credential);

     connInfo.AuthenticationMechanism = options.AuthMethod switch
     {
         WinRmAuthMethod.Kerberos => AuthenticationMechanism.Kerberos,
         WinRmAuthMethod.NTLM => AuthenticationMechanism.Negotiate,  // Negotiate allows NTLM fallback
         WinRmAuthMethod.CredSSP => AuthenticationMechanism.Credssp,
         _ => AuthenticationMechanism.Default
     };
     ```
   - **CredSSP validation:** If `CredSSP` is selected, the execution engine should log a warning that CredSSP requires prior GPO configuration. If the connection fails with an auth error and CredSSP was selected, the error message should include remediation steps
   - **Parameter injection via `AddParameter()`** — NEVER string interpolation:
     ```csharp
     using var ps = PowerShell.Create();
     ps.AddScript(scriptContent);
     if (parameters != null)
     {
         foreach (var kvp in parameters)
             ps.AddParameter(kvp.Key, kvp.Value);
     }
     ```
   - Capture output streams: Output, Error, Warning, Verbose
   - Enforce timeout using `CancellationToken` combined with `Task.WhenAny` and a delay task
   - **Error mapping** (expanded for auth-specific errors):
     - `PSRemotingTransportException` with "Access is denied" → "Authentication failed for {host} using {authMethod}. Verify credentials and that {authMethod} is enabled on the target."
     - `PSRemotingTransportException` with "CredSSP" → "CredSSP authentication failed for {host}. Ensure CredSSP is enabled via GPO on both client and server."
     - `PSRemotingTransportException` (general) → "WinRM connection failed to {host} on {transport}:{port}. Verify WinRM is enabled and the {transport} listener is configured."
     - `UnauthorizedAccessException` → "Access denied to {host}. Check credentials and remote management permissions."
     - `OperationCanceledException` → "Execution cancelled by user."
     - Timeout → "Execution timed out after {n} seconds on {host}."

3. **Implement `WmiQueryStrategy.cs`:**
   - `ConnectionOptions` with configurable auth:
     ```csharp
     var connOpts = new ConnectionOptions
     {
         Authentication = options.AuthMethod switch
         {
             WinRmAuthMethod.Kerberos => AuthenticationLevel.PacketPrivacy,
             WinRmAuthMethod.NTLM => AuthenticationLevel.PacketPrivacy,
             WinRmAuthMethod.CredSSP => AuthenticationLevel.PacketPrivacy,
             _ => AuthenticationLevel.Default
         },
         Impersonation = ImpersonationLevel.Impersonate
     };
     ```
   - Credential handling via `ConnectionOptions.Username` / `ConnectionOptions.SecurePassword`
   - Same timeout and error mapping approach

4. **Implement `RemoteExecutionService.cs`:**
   - Accepts `ExecutionJob` containing `WinRmConnectionOptions` (loaded from settings, overridable per-run)
   - **Pre-flight:** host reachability via TCP connect to the correct port (5985 for HTTP, 5986 for HTTPS, based on `WinRmConnectionOptions.Transport`)
   - **Parallel execution:** `SemaphoreSlim` with configurable throttle
   - **Progress reporting:** `IProgress<HostResult>` for real-time UI updates
   - **Cancellation:** `CancellationToken` propagated to all child tasks
   - **Error isolation:** per-host try/catch
   - **Large result handling:** track cumulative output size. If exceeds `AppConstants.MaxInMemoryResultBytes` (10MB), switch to streaming per-host results to temp files in `%LOCALAPPDATA%\SysOpsCommander\Temp\`. `HostResult.Output` becomes a file path reference with a `IsFileReference` flag. UI reads on demand
   - **Credential lifecycle:** accept `PSCredential`, pass by reference to strategies, never store. Caller disposes after execution

5. **Implement `ICredentialService` and `CredentialService.cs`:**
   - `PromptForCredentials()` → raises event for ViewModel to show dialog
   - `ValidateCredentialsAsync(PSCredential credential, string? targetDomain)` → LDAP bind test against the specified domain (or current domain if null)
   - `DisposeCredentials(PSCredential credential)` → dispose SecureString, null reference

6. **Implement `IHostTargetingService` and `HostTargetingService.cs`** (SINGLETON):
   - `ObservableCollection<HostTarget> Targets` — observable for UI binding
   - `AddFromHostnames(IEnumerable<string> hostnames)` → validate, de-duplicate
   - `AddFromCsvFile(string filePath)` → parse, validate, de-duplicate
   - `AddFromAdSearchResults(IEnumerable<AdObject> computers)` → extract `dNSHostName` or `cn`, add to targets
   - `CheckReachabilityAsync(CancellationToken ct)` → TCP connect test on the correct port (based on current WinRM transport setting). Parallel with throttle of 20
   - `ClearTargets()` / `RemoveTarget(string hostname)`

7. **Implement `INotificationService` and `NotificationService.cs`:**
   - Windows toast notifications via `Microsoft.Toolkit.Uwp.Notifications`
   - `ShowExecutionComplete(ExecutionJob job)` → toast showing: script name, host count, success/fail breakdown
   - Toast click opens the application and navigates to the Execution view's results panel

8. **Write unit and integration tests:**
   - `PowerShellRemoteStrategy`: verify `WSManConnectionInfo` is constructed with correct auth method and transport for each enum combination (Kerberos/HTTP, NTLM/HTTPS, CredSSP/HTTP, etc.)
   - Verify parameters are passed via `AddParameter()` not string interpolation (inspect the `PowerShell.Commands` collection in the mock)
   - `RemoteExecutionService`: throttle enforcement, cancellation, error isolation, progress reporting, large result disk streaming
   - `HostTargetingService`: validation, de-duplication, CSV parsing, singleton behavior
   - `CredentialService`: LDAP bind validation, disposal

#### Acceptance Criteria
- [ ] WinRM connection uses the correct auth method (Kerberos, NTLM, or CredSSP) based on configuration
- [ ] WinRM connection uses correct transport and port (HTTP/5985 or HTTPS/5986)
- [ ] CredSSP failure produces a clear error with remediation steps
- [ ] Script parameters are injected via `AddParameter()` (verified by unit test)
- [ ] Parallel execution respects throttle limit
- [ ] Cancellation correctly stops remaining hosts
- [ ] Per-host error isolation works
- [ ] Large results (>10MB cumulative) stream to disk
- [ ] Progress reporting delivers real-time updates
- [ ] Host reachability pre-check uses the correct port for the configured transport
- [ ] Toast notification fires on execution completion
- [ ] `IHostTargetingService` is a singleton and observable
- [ ] All unit tests pass

---

### Phase 5: Script Plugin System

**Goal:** Build the script loader and export services. Unchanged from Rev 1 except: ensure the `outputFormat` field is treated as a rendering hint per the Critical Technical Notes section.

#### Tasks

(Same as Rev 1 — scanner, file provider, export service, sample scripts)

**Additional clarification for the agent:**
- When loading manifests, validate `outputFormat` is one of `text`, `table`, `json` but do NOT build any output parsing logic. The UI will render all output as text, using `outputFormat` only to select the display component (monospace block for text/table, JSON tree viewer for json with text fallback)

#### Acceptance Criteria
(Same as Rev 1)

---

### Phase 6: UI Shell & Navigation

**Goal:** Build the complete WPF UI shell including the **domain selector** in the status bar and the navigation framework.

#### Tasks

(Mostly same as Rev 1 with these additions:)

1. **`MainWindow.xaml` additions:**
   - Status bar at the bottom must include: **domain selector dropdown** (showing the active domain, click to switch), current user, connection status indicator, log level badge
   - Domain selector triggers `IActiveDirectoryService.SetActiveDomainAsync()` and refreshes any active AD views

2. **Build `DomainSelectorDialog.xaml`** (for the case where the user wants to manually enter a domain not in the forest trust list):
   - Domain name text field
   - Optional: specific DC FQDN
   - "Test Connection" button that validates the domain is reachable
   - OK/Cancel

3. **`CredentialDialog.xaml` addition:**
   - Domain field pre-populated with the **active domain** (not just the user's home domain)
   - Auth method selector: Kerberos / NTLM / CredSSP (pre-populated from default settings)

4. **Keyboard shortcuts** (expanded):
   - `Ctrl+F` → Focus search bar
   - `Ctrl+E` → Navigate to Execution view
   - `Ctrl+D` → Open domain selector
   - `F5` → Refresh current view
   - `Escape` → Cancel current operation

#### Acceptance Criteria
(Same as Rev 1 plus:)
- [ ] Domain selector in status bar shows current domain and allows switching
- [ ] Domain selector dialog allows manual domain entry with connection test
- [ ] Credential dialog pre-populates with active domain and default auth method
- [ ] `Ctrl+D` opens domain selector

---

### Phase 7: AD Explorer Views

**Goal:** Wire the AD service layer into the UI. Includes domain-aware search and configurable stale threshold.

#### Tasks

(Same as Rev 1 with these additions:)

1. **AD Explorer view additions:**
   - Domain indicator badge at the top of the view showing which domain is being queried
   - Stale computers filter uses the threshold from settings (not hardcoded 90)
   - The stale computers button label shows the current threshold: "Stale Computers (90 days)" — updates dynamically if the setting changes

2. **Dashboard view additions:**
   - Show active domain name
   - "Quick Connect" section: enter a single hostname and immediately see its AD object detail + option to execute scripts against it. This is the fastest path for incident response — one box, one hostname, immediate action

#### Acceptance Criteria
(Same as Rev 1 plus:)
- [ ] AD Explorer shows which domain is active
- [ ] Stale computer threshold is loaded from settings, not hardcoded
- [ ] Dashboard "Quick Connect" resolves a hostname to its AD object and offers execution

---

### Phase 8: Execution View & Script Library

**Goal:** Build the main execution interface with **WinRM connection configuration** exposed in the UI.

#### Tasks

(Same as Rev 1 with these additions to the Execution View:)

1. **Execution controls bar additions:**
   - **WinRM Auth Method** dropdown: Kerberos | NTLM | CredSSP (defaults from settings)
   - **WinRM Transport** toggle: HTTP | HTTPS (defaults from settings)
   - When CredSSP is selected, show an info banner: "CredSSP requires GPO configuration on both client and server hosts."
   - These values are passed to the `ExecutionJob` and stored in the audit log

2. **Execution flow additions:**
   - Step 1.5 (after script validation): If CredSSP is selected as auth method and alternate credentials are NOT provided, warn: "CredSSP requires explicit credentials. Would you like to enter credentials now?" (CredSSP cannot use implicit Kerberos delegation)
   - After execution completes, fire `INotificationService.ShowExecutionComplete()` for toast notification

3. **Audit log entry additions:**
   - Record `WinRmAuthMethod`, `WinRmTransport`, and `TargetDomain` for every execution

#### Acceptance Criteria
(Same as Rev 1 plus:)
- [ ] Auth method and transport are selectable in the execution controls
- [ ] CredSSP selection shows info banner and forces credential prompt
- [ ] Auth/transport choices are recorded in the audit log
- [ ] Toast notification fires on execution completion (even when app is not focused)

---

### Phase 9: Audit Log View, Settings & Auto-Update

**Goal:** Build the audit log browser, settings page, and a properly designed auto-update system.

#### Tasks

(Audit Log and Settings mostly same as Rev 1, with these additions:)

1. **Audit Log view additions:**
   - Additional columns: Auth Method, Transport, Target Domain
   - Filter by domain

2. **Settings view additions:**
   - **Domain & Connection section:**
     - Default domain (text, or "auto-detect" for current user's domain)
     - Default WinRM auth method: Kerberos / NTLM / CredSSP dropdown
     - Default WinRM transport: HTTP / HTTPS toggle
     - Stale computer threshold (numeric days, default 90)
   - **Repository section:**
     - Org-wide default path shown as read-only (sourced from `appsettings.json`)
     - Per-user override checkbox and path editor

3. **Auto-Update — Fully Specified Implementation:**

   The auto-update system uses a simple network share convention:

   **Update package structure on the network share:**
   ```
   \\server\share\SysOpsCommander\
   ├── version.json          # Metadata file
   └── SysOpsCommander.zip   # Self-contained published app
   ```

   **`version.json` format:**
   ```json
   {
     "version": "1.1.0",
     "releaseDate": "2026-04-15",
     "releaseNotes": "Added CredSSP support, bug fixes.",
     "minimumVersion": "1.0.0",
     "sha256": "abc123..."
   }
   ```

   **`AutoUpdateService.cs` implementation:**
   - `CheckForUpdateAsync()`:
     1. Read the update share path from `ISettingsService`
     2. Attempt to read `version.json` from the share (handle network errors gracefully — update check failure is never a blocking error)
     3. Compare `version.json:version` against `Assembly.GetExecutingAssembly().GetName().Version`
     4. If newer: return `UpdateAvailable` with version info and release notes
     5. If same or older: return `UpToDate`
   - `DownloadAndApplyAsync()`:
     1. Copy `SysOpsCommander.zip` from the share to `%LOCALAPPDATA%\SysOpsCommander\Updates\`
     2. Verify SHA256 hash matches `version.json:sha256`
     3. Extract to `%LOCALAPPDATA%\SysOpsCommander\Updates\staged\`
     4. Write a `pending-update.json` file with the staged path
     5. Prompt user: "Update downloaded. Restart to apply?" (do NOT force restart)
   - **On application startup** (`App.xaml.cs`):
     1. Check for `pending-update.json`
     2. If present: launch a small updater bootstrapper (`SysOpsUpdater.exe`) that:
        a. Waits for the main app process to exit (poll with timeout)
        b. Copies staged files over the application directory
        c. Deletes the staged directory and `pending-update.json`
        d. Re-launches the main application
     3. The updater bootstrapper is a tiny (~50 line) console app included in the project
   - **File locking:** The app cannot overwrite its own running executables. The bootstrapper approach avoids this by running the copy after the main process exits
   - **Failure recovery:** If the bootstrapper crashes, the original files are untouched (copy happens to a temp location first, then atomic move). On next startup, if `pending-update.json` exists but the staged directory is missing, delete the pending file and continue normally

   **Update check timing:**
   - Check on app startup (background, non-blocking)
   - Check available via Settings page "Check for Updates" button
   - Show a subtle indicator in the status bar if an update is available (not a modal popup)

#### Acceptance Criteria
(Same as Rev 1 plus:)
- [ ] Audit log shows auth method, transport, and target domain columns
- [ ] Settings page includes domain/connection defaults and stale threshold
- [ ] Settings page shows org-wide default (read-only) vs per-user override clearly
- [ ] Auto-update reads `version.json` from network share
- [ ] Auto-update compares versions correctly (semver)
- [ ] Auto-update downloads, verifies SHA256, and stages the update
- [ ] Updater bootstrapper applies update on restart without file-locking errors
- [ ] Failed update checks do not block application startup
- [ ] Status bar shows update-available indicator

---

### Phase 10: Polish, Hardening, & Comprehensive Testing

**Goal:** Final phase — hardening, performance, security verification, documentation, and release.

#### Tasks

(Same as Rev 1 with these additions:)

1. **Error handling audit additions:**
   - Test CredSSP failures on hosts where CredSSP is not configured → verify clear error message
   - Test domain switching to an unreachable domain → verify graceful fallback
   - Test auto-update with corrupted zip file → verify SHA256 check catches it
   - Test auto-update with unreachable network share → verify non-blocking failure

2. **Security verification additions:**
   - Verify WinRM CredSSP connections do not leak credentials to unauthorized delegates (test in lab environment)
   - Verify auth method is correctly recorded in audit log for all three methods

3. **Documentation additions:**
   - `CONTRIBUTING.md` must include a complete example of creating a new script plugin:
     1. Write the .ps1 file with `param()` block
     2. Create the .json manifest with matching parameter names
     3. Place both in the shared repository
     4. Refresh the library in the app
     5. Show the expected result in the Script Library view
   - Deployment guide must include: CredSSP GPO configuration instructions (both client and server side), WinRM HTTPS listener setup if HTTPS is required, firewall port requirements (5985/5986)
   - `appsettings.json` documentation: all available keys, what they control, acceptable values

4. **Release preparation additions:**
   - Build the `SysOpsUpdater.exe` bootstrapper as a separate console project
   - Include it in the published output
   - Create a sample `version.json` for the update share
   - Document the process for publishing an update to the network share

#### Acceptance Criteria
(Same as Rev 1 plus:)
- [ ] CredSSP error handling produces actionable messages
- [ ] Domain switching failure is graceful
- [ ] Auto-update handles corrupted packages and unreachable shares
- [ ] `CONTRIBUTING.md` includes complete script plugin walkthrough
- [ ] Deployment guide covers CredSSP GPO, HTTPS listener, and firewall setup
- [ ] `SysOpsUpdater.exe` bootstrapper is included in published output
- [ ] `appsettings.json` is fully documented

---

## Agent Instructions

When working through these phases, follow these principles:

1. **Complete each phase fully before starting the next.** Don't jump ahead — each phase depends on the previous.
2. **Commit after each meaningful task within a phase.** Use descriptive commit messages: `Phase 0: Configure Serilog with credential destructuring policy`.
3. **Run all existing tests after every change.** Never break previously passing tests.
4. **Every public method gets an XML documentation comment.** No exceptions.
5. **Every service method logs at appropriate levels.** Use `Information` for business events, `Debug` for technical details, `Warning` for recoverable issues, `Error` for failures.
6. **Never store credentials.** If you find yourself writing credential data to any file, database, log, or config — stop. That's a bug. Passwords never appear in audit logs, Serilog output, settings, or temp files.
7. **Use `CancellationToken` on every async method.** Thread it through the entire call chain.
8. **Use `ConfigureAwait(false)` on non-UI async calls** (Services, Infrastructure layers). Do NOT use it in ViewModel layer (needs SynchronizationContext for UI updates).
9. **Interface-first development.** Define the interface, then implement it. Register it in DI. Inject it where needed. Never `new` up a service directly.
10. **When in doubt about a design decision, check the design document** (SysOpsCommander_DesignDocument.docx). It is the source of truth.
11. **Keep the solution building with zero warnings at all times.** Treat warnings as errors.
12. **Parameters to remote scripts are ALWAYS injected via `AddParameter()`.** Never concatenate user input into script strings.
13. **`IHostTargetingService` is a SINGLETON.** It is the shared state between AD Explorer and Execution views. Do not create multiple instances.
14. **Read the Critical Technical Notes section** at the top of this document before starting. It covers cross-cutting concerns that affect multiple phases.