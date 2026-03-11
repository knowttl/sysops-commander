# Phase 1: Core Models, Settings, and Database Infrastructure

> **Goal:** Build the data layer — all models/DTOs, enums, service interfaces, the SQLite database, settings persistence, and the repository pattern. This phase defines the shared vocabulary that every subsequent phase depends on.
>
> **Prereqs:** Phase 0 complete (solution builds, DI wired, Serilog configured).
>
> **Outputs:** All Core models/enums/interfaces compiled with XML docs, SQLite database created on first run, settings layering (per-user > org default > hard default) working, and all unit tests passing.

---

## Sub-Steps

### 1.1 — Create `ExecutionStatus` Enum

**File:** `src/SysOpsCommander.Core/Enums/ExecutionStatus.cs`

**Values:** `Pending`, `Validating`, `Running`, `Completed`, `PartialFailure`, `Failed`, `Cancelled`

**Conventions:**
- File-scoped namespace
- XML `<summary>` on the enum and each value
- Values start from 0 (default)

**Commit:** `feat(core): add ExecutionStatus enum`

---

### 1.2 — Create `HostStatus` Enum

**File:** `src/SysOpsCommander.Core/Enums/HostStatus.cs`

**Values:** `Pending`, `Reachable`, `Unreachable`, `Running`, `Success`, `Failed`, `Timeout`, `Cancelled`, `Skipped`

**Commit:** `feat(core): add HostStatus enum`

---

### 1.3 — Create `ExecutionType` Enum

**File:** `src/SysOpsCommander.Core/Enums/ExecutionType.cs`

**Values:** `PowerShell`, `WMI`

**Commit:** `feat(core): add ExecutionType enum`

---

### 1.4 — Create `ScriptDangerLevel` Enum

**File:** `src/SysOpsCommander.Core/Enums/ScriptDangerLevel.cs`

**Values:** `Safe`, `Caution`, `Destructive`

**Commit:** `feat(core): add ScriptDangerLevel enum`

---

### 1.5 — Create `OutputFormat` Enum

**File:** `src/SysOpsCommander.Core/Enums/OutputFormat.cs`

**Values:** `Text`, `Table`, `Json`

> **Cross-reference:** `outputFormat` is a **rendering hint only** (see Critical Technical Notes in the implementation plan). Do NOT build parsing logic — the UI uses this to select a display component.

**Commit:** `feat(core): add OutputFormat enum`

---

### 1.6 — Create `WinRmAuthMethod` Enum

**File:** `src/SysOpsCommander.Core/Enums/WinRmAuthMethod.cs`

**Values:** `Kerberos`, `NTLM`, `CredSSP`

**XML docs should note:**
- `Kerberos` — default, requires domain membership
- `NTLM` — fallback for non-domain or cross-forest scenarios
- `CredSSP` — requires GPO configuration on both client and server

**Commit:** `feat(core): add WinRmAuthMethod enum`

---

### 1.7 — Create `WinRmTransport` Enum

**File:** `src/SysOpsCommander.Core/Enums/WinRmTransport.cs`

**Values:** `HTTP`, `HTTPS`

**XML docs should note:**
- `HTTP` — port 5985, default
- `HTTPS` — port 5986, requires certificate on target

**Commit:** `feat(core): add WinRmTransport enum`

---

### 1.8 — Create `NotificationSeverity` Enum

> **Improvement:** Referenced by `INotificationService.ShowToast()` in the plan but never explicitly defined. Adding it here.

**File:** `src/SysOpsCommander.Core/Enums/NotificationSeverity.cs`

**Values:** `Information`, `Success`, `Warning`, `Error`

**Commit:** `feat(core): add NotificationSeverity enum`

---

### 1.9 — Create `AdObject` Model

**File:** `src/SysOpsCommander.Core/Models/AdObject.cs`

**Properties:**
- `string DistinguishedName` — the full DN
- `string ObjectClass` — e.g., "user", "computer", "group"
- `string Name` — the `cn` value
- `string? DisplayName` — display-friendly name (nullable for objects without one)
- `IReadOnlyDictionary<string, object?> Attributes` — all loaded AD attributes

**Design notes:**
- Immutable record or class with `init` setters
- Do NOT inherit from `ObservableObject` — this is a pure data model, not a ViewModel

**Commit:** `feat(core): add AdObject model`

---

### 1.10 — Create `AdSearchResult` Model

**File:** `src/SysOpsCommander.Core/Models/AdSearchResult.cs`

**Properties:**
- `IReadOnlyList<AdObject> Results`
- `string Query` — the search term or LDAP filter used
- `TimeSpan ExecutionTime`
- `int TotalResultCount` — may differ from `Results.Count` if paginated
- `bool HasMoreResults` — indicates additional pages available

**Commit:** `feat(core): add AdSearchResult model`

---

### 1.11 — Create `HostTarget` Model

**File:** `src/SysOpsCommander.Core/Models/HostTarget.cs`

**Properties:**
- `string Hostname` — validated hostname (NetBIOS, FQDN, or IPv4)
- `HostStatus Status` — current reachability/execution status
- `bool IsValidated` — whether hostname passed validation
- `string? ValidationError` — error message if validation failed

**Design notes:**
- Consider using `ObservableObject` since `HostTargetingService` exposes `ObservableCollection<HostTarget>` and the UI binds to status changes. This is one of the few models that needs property change notification.

**Commit:** `feat(core): add HostTarget model`

---

### 1.12 — Create `HostResult` Model

**File:** `src/SysOpsCommander.Core/Models/HostResult.cs`

**Properties:**
- `string Hostname`
- `HostStatus Status`
- `string Output` — raw output text (or file path if `IsFileReference` is true)
- `string? ErrorOutput` — error stream text
- `string? WarningOutput` — warning stream text
- `TimeSpan Duration`
- `bool IsFileReference` — when true, `Output` is a temp file path (large result streaming)
- `DateTime CompletedAt`

**Static factory methods (for cleaner construction in the execution engine):**
```csharp
public static HostResult Success(string hostname, string output, TimeSpan duration)
public static HostResult Failure(string hostname, string errorMessage)
public static HostResult Cancelled(string hostname)
public static HostResult Timeout(string hostname, int timeoutSeconds)
```

**Commit:** `feat(core): add HostResult model with static factory methods`

---

### 1.13 — Create `WinRmConnectionOptions` Model

**File:** `src/SysOpsCommander.Core/Models/WinRmConnectionOptions.cs`

**Properties:**
- `WinRmAuthMethod AuthMethod` — defaults to `Kerberos`
- `WinRmTransport Transport` — defaults to `HTTP`
- `int? CustomPort` — override default port if needed
- `string? ShellUri` — optional custom shell URI

**Methods:**
- `int GetEffectivePort()` — returns `CustomPort ?? (Transport == HTTPS ? 5986 : 5985)`
- `static WinRmConnectionOptions CreateDefault()` — factory for default Kerberos/HTTP config

**Commit:** `feat(core): add WinRmConnectionOptions model`

---

### 1.14 — Create `DomainConnection` Model

**File:** `src/SysOpsCommander.Core/Models/DomainConnection.cs`

**Properties:**
- `string DomainName` — e.g., "corp.contoso.com"
- `string? DomainControllerFqdn` — optional, for explicit DC targeting
- `string RootDistinguishedName` — e.g., "DC=corp,DC=contoso,DC=com"
- `bool IsCurrentDomain` — true if this is the logged-in user's domain

> **Improvement:** Do NOT store a `DirectoryEntry` object as a property. `DirectoryEntry` is `IDisposable` and creates a tight coupling to `System.DirectoryServices`. Store connection parameters and create `DirectoryEntry` on demand in the service layer.

**Commit:** `feat(core): add DomainConnection model for multi-domain AD support`

---

### 1.15 — Create `ExecutionJob` Model

**File:** `src/SysOpsCommander.Core/Models/ExecutionJob.cs`

**Properties:**
- `Guid Id` — unique execution run identifier
- `string ScriptName` — display name of the script
- `string ScriptContent` — the raw .ps1 content
- `IDictionary<string, object>? Parameters` — script parameters
- `IReadOnlyList<HostTarget> TargetHosts`
- `ExecutionStatus Status`
- `ExecutionType ExecutionType`
- `WinRmConnectionOptions WinRmConnectionOptions`
- `PSCredential? Credential` — explicit credentials (required for CredSSP, optional otherwise)
- `DateTime StartTime`
- `DateTime? EndTime`
- `IList<HostResult> Results` — populated as hosts complete
- `int ThrottleLimit` — concurrent execution limit
- `int TimeoutSeconds` — per-host timeout
- `string? TargetDomain` — the AD domain context for this execution

> **Note:** `Credential` is typed as `PSCredential?` (from `System.Management.Automation`). This creates a dependency on the PowerShell SDK in the Core project, which is acceptable for v1 since the entire execution model is PowerShell-centric.

**Commit:** `feat(core): add ExecutionJob model`

---

### 1.16 — Create `ScriptManifest` Model

**File:** `src/SysOpsCommander.Core/Models/ScriptManifest.cs`

**Properties:**
- `string Name`
- `string Description`
- `string Version` — semver format (e.g., "1.0.0")
- `string Author`
- `string Category`
- `ScriptDangerLevel DangerLevel`
- `OutputFormat OutputFormat` — rendering hint
- `IReadOnlyList<ScriptParameter> Parameters`

**Nested type `ScriptParameter`:**
- `string Name`
- `string DisplayName`
- `string Description`
- `string Type` — "string", "int", "bool", "choice"
- `bool Required`
- `object? DefaultValue`
- `IReadOnlyList<string>? Choices` — required when Type is "choice"

**Design notes:**
- Use `System.Text.Json` attributes (`[JsonPropertyName]`) for deserialization
- This maps directly to the JSON manifest files in `scripts/examples/`

**Commit:** `feat(core): add ScriptManifest model with ScriptParameter`

---

### 1.17 — Create `ScriptPlugin` Model

**File:** `src/SysOpsCommander.Core/Models/ScriptPlugin.cs`

**Properties:**
- `string FilePath` — full path to the .ps1 file
- `string FileName` — just the filename
- `ScriptManifest? Manifest` — null if no JSON manifest found
- `bool HasManifest` — convenience property
- `bool IsValidated` — whether validation has been run
- `IReadOnlyList<string> ValidationErrors` — empty if valid
- `IReadOnlyList<string> ValidationWarnings`
- `IReadOnlyList<DangerousPatternWarning> DangerousPatterns` — patterns detected by AST analysis
- `ScriptDangerLevel EffectiveDangerLevel` — max of manifest level and detected pattern level
- `string Category` — from manifest or `AppConstants.DefaultScriptCategory`
- `string? Content` — lazily loaded script content (null until first access)
- `DateTime LastModified` — file system timestamp

> **Improvement:** Added `LastModified` for detecting stale cached scripts when the file changes on disk.

**Commit:** `feat(core): add ScriptPlugin model`

---

### 1.18 — Create `AuditLogEntry` Model

**File:** `src/SysOpsCommander.Core/Models/AuditLogEntry.cs`

**Properties:**
- `long Id` — SQLite auto-increment
- `DateTime Timestamp`
- `string UserName` — the user who ran the execution
- `string MachineName` — the machine the app ran on
- `string ScriptName`
- `string TargetHosts` — comma-separated list (or JSON array)
- `int TargetHostCount`
- `int SuccessCount`
- `int FailureCount`
- `ExecutionStatus Status`
- `TimeSpan Duration`
- `string? ErrorSummary` — first error message, if any
- `WinRmAuthMethod? AuthMethod` — nullable for pre-v2 entries
- `WinRmTransport? Transport`
- `string? TargetDomain`
- `Guid CorrelationId` — links to log entries

**Commit:** `feat(core): add AuditLogEntry model with WinRM audit fields`

---

### 1.19 — Create `UserSettings` Model

**File:** `src/SysOpsCommander.Core/Models/UserSettings.cs`

**Properties:**
- `string Key` — setting identifier
- `string Value` — serialized value
- `DateTime LastModified`

**Commit:** `feat(core): add UserSettings model`

---

### 1.20 — Create `AuditLogFilter` Model

> **Improvement:** Referenced by `IAuditLogService.QueryAsync(AuditLogFilter filter)` in the plan but never explicitly defined.

**File:** `src/SysOpsCommander.Core/Models/AuditLogFilter.cs`

**Properties:**
- `DateTime? StartDate`
- `DateTime? EndDate`
- `string? ScriptName`
- `string? UserName`
- `ExecutionStatus? Status`
- `WinRmAuthMethod? AuthMethod`
- `string? Hostname` — filter by target hostname (partial match)
- `string? TargetDomain`
- `int PageNumber` — default 1
- `int PageSize` — default `AppConstants.MaxResultsPerPage`

**Commit:** `feat(core): add AuditLogFilter model for audit log queries`

---

### 1.21 — Create `ValidationResult` in Core/Validation

> **Improvement:** Referenced by `HostnameValidator` (Phase 2) but never defined. Defining it now so interfaces can reference it.

**File:** `src/SysOpsCommander.Core/Validation/ValidationResult.cs`

**Properties:**
- `bool IsValid`
- `string? ErrorMessage`
- Static factories: `ValidationResult.Success()`, `ValidationResult.Failure(string message)`

**Commit:** `feat(core): add ValidationResult for validation framework`

---

### 1.22 — Create `ManifestValidationResult` in Core/Validation

> **Improvement:** Referenced by `ManifestSchemaValidator` (Phase 2) but never defined.

**File:** `src/SysOpsCommander.Core/Validation/ManifestValidationResult.cs`

**Properties:**
- `bool IsValid` — true if no errors (warnings are OK)
- `IReadOnlyList<string> Errors`
- `IReadOnlyList<string> Warnings`

**Commit:** `feat(core): add ManifestValidationResult for manifest validation`

---

### 1.23 — Define `IActiveDirectoryService` Interface

**File:** `src/SysOpsCommander.Core/Interfaces/IActiveDirectoryService.cs`

**Methods (all with `CancellationToken`):**
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

**Commit:** `feat(core): add IActiveDirectoryService interface with multi-domain support`

---

### 1.24 — Define `IRemoteExecutionService` Interface

**File:** `src/SysOpsCommander.Core/Interfaces/IRemoteExecutionService.cs`

**Methods:**
```
Task<ExecutionJob> ExecuteAsync(ExecutionJob job, IProgress<HostResult> progress, CancellationToken ct)
Task CancelExecutionAsync(Guid jobId)
```

> **Note:** Credentials are passed via `ExecutionJob.Credential` rather than as a separate parameter. This keeps the signature clean and ensures the credential travels with the job through the execution pipeline.

**Commit:** `feat(core): add IRemoteExecutionService interface`

---

### 1.25 — Define `IExecutionStrategy` Interface

**File:** `src/SysOpsCommander.Core/Interfaces/IExecutionStrategy.cs`

**Properties and methods:**
```
ExecutionType Type { get; }
Task<HostResult> ExecuteAsync(string hostname, string scriptContent, IDictionary<string, object>? parameters, PSCredential? credential, WinRmConnectionOptions connectionOptions, int timeoutSeconds, CancellationToken ct)
```

**Commit:** `feat(core): add IExecutionStrategy interface`

---

### 1.26 — Define `IHostTargetingService` Interface

**File:** `src/SysOpsCommander.Core/Interfaces/IHostTargetingService.cs`

**Properties and methods:**
```
ObservableCollection<HostTarget> Targets { get; }
void AddFromHostnames(IEnumerable<string> hostnames)
Task AddFromCsvFileAsync(string filePath, CancellationToken ct)
void AddFromAdSearchResults(IEnumerable<AdObject> computers)
Task CheckReachabilityAsync(WinRmConnectionOptions connectionOptions, CancellationToken ct)
void ClearTargets()
void RemoveTarget(string hostname)
```

> **Cross-reference:** This is a **SINGLETON** — shared between AD Explorer and Execution views.

**Commit:** `feat(core): add IHostTargetingService interface (singleton)`

---

### 1.27 — Define `ICredentialService` Interface

**File:** `src/SysOpsCommander.Core/Interfaces/ICredentialService.cs`

**Methods:**
```
event EventHandler<EventArgs>? CredentialRequested
void RequestCredentials()
Task<bool> ValidateCredentialsAsync(PSCredential credential, string? targetDomain, CancellationToken ct)
void DisposeCredentials(PSCredential credential)
```

**Commit:** `feat(core): add ICredentialService interface`

---

### 1.28 — Define `IScriptLoaderService` Interface

**File:** `src/SysOpsCommander.Core/Interfaces/IScriptLoaderService.cs`

**Methods:**
```
Task<IReadOnlyList<ScriptPlugin>> LoadAllScriptsAsync(CancellationToken ct)
Task<ScriptPlugin> LoadScriptAsync(string filePath, CancellationToken ct)
Task RefreshAsync(CancellationToken ct)
```

**Commit:** `feat(core): add IScriptLoaderService interface`

---

### 1.29 — Define `IAuditLogService` Interface

**File:** `src/SysOpsCommander.Core/Interfaces/IAuditLogService.cs`

**Methods:**
```
Task LogExecutionAsync(AuditLogEntry entry, CancellationToken ct)
Task<IReadOnlyList<AuditLogEntry>> QueryAsync(AuditLogFilter filter, CancellationToken ct)
Task<int> PurgeOldEntriesAsync(int retentionDays, CancellationToken ct)
```

**Commit:** `feat(core): add IAuditLogService interface`

---

### 1.30 — Define `ISettingsService` Interface

**File:** `src/SysOpsCommander.Core/Interfaces/ISettingsService.cs`

**Methods:**
```
Task<string> GetAsync(string key, string defaultValue, CancellationToken ct)
Task SetAsync(string key, string value, CancellationToken ct)
Task<T> GetTypedAsync<T>(string key, T defaultValue, CancellationToken ct) where T : IParsable<T>
string GetOrgDefault(string key)
Task<string> GetEffectiveAsync(string key, CancellationToken ct)
```

**Commit:** `feat(core): add ISettingsService interface with settings layering`

---

### 1.31 — Define `IExportService` Interface

**File:** `src/SysOpsCommander.Core/Interfaces/IExportService.cs`

**Methods:**
```
Task ExportToExcelAsync(IEnumerable<HostResult> results, string filePath, CancellationToken ct)
Task ExportToCsvAsync(IEnumerable<HostResult> results, string filePath, CancellationToken ct)
Task ExportAuditLogToExcelAsync(IEnumerable<AuditLogEntry> entries, string filePath, CancellationToken ct)
Task ExportAuditLogToCsvAsync(IEnumerable<AuditLogEntry> entries, string filePath, CancellationToken ct)
```

**Commit:** `feat(core): add IExportService interface`

---

### 1.32 — Define `IAutoUpdateService` Interface

**File:** `src/SysOpsCommander.Core/Interfaces/IAutoUpdateService.cs`

**Methods:**
```
Task<UpdateCheckResult> CheckForUpdateAsync(CancellationToken ct)
Task<UpdateDownloadResult> DownloadAndStageAsync(CancellationToken ct)
bool HasPendingUpdate()
void LaunchUpdaterAndExit()
```

**Supporting records** (defined in the same file or as separate models):
```csharp
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

**Commit:** `feat(core): add IAutoUpdateService interface with result types`

---

### 1.33 — Define `INotificationService` Interface

**File:** `src/SysOpsCommander.Core/Interfaces/INotificationService.cs`

**Methods:**
```
void ShowToast(string title, string message, NotificationSeverity severity)
void ShowExecutionComplete(ExecutionJob job)
```

**Commit:** `feat(core): add INotificationService interface`

---

### 1.34 — Define Repository Interfaces

> **Improvement:** The plan has service interfaces but no repository interfaces. Adding `IAuditLogRepository` and `ISettingsRepository` enables testability via NSubstitute without requiring in-memory SQLite for unit tests.

**Files:**
- `src/SysOpsCommander.Core/Interfaces/IAuditLogRepository.cs`
- `src/SysOpsCommander.Core/Interfaces/ISettingsRepository.cs`

**`IAuditLogRepository`:**
```
Task InsertAsync(AuditLogEntry entry, CancellationToken ct)
Task<IReadOnlyList<AuditLogEntry>> QueryAsync(AuditLogFilter filter, CancellationToken ct)
Task<int> DeleteOlderThanAsync(DateTime cutoffDate, CancellationToken ct)
```

**`ISettingsRepository`:**
```
Task<string?> GetValueAsync(string key, CancellationToken ct)
Task SetValueAsync(string key, string value, CancellationToken ct)
Task<IReadOnlyList<UserSettings>> GetAllAsync(CancellationToken ct)
```

**Commit:** `feat(core): add IAuditLogRepository and ISettingsRepository interfaces`

---

### 1.35 — Implement `DatabaseInitializer.cs`

**File:** `src/SysOpsCommander.Infrastructure/Database/DatabaseInitializer.cs`

**Behavior:**
1. Determine DB path: `Path.Combine(Environment.GetFolderPath(SpecialFolder.LocalApplicationData), AppConstants.AppDataFolder, "audit.db")`
2. Create directory if it doesn't exist
3. Create tables using `IF NOT EXISTS`:
   - **`AuditLog`** — Id (INTEGER PK AUTOINCREMENT), Timestamp, UserName, MachineName, ScriptName, TargetHosts, TargetHostCount, SuccessCount, FailureCount, Status, DurationMs, ErrorSummary, AuthMethod, Transport, TargetDomain, CorrelationId
   - **`UserSettings`** — Key (TEXT PK), Value (TEXT), LastModified (TEXT)
   - **`SchemaVersion`** — Version (INTEGER PK), AppliedAt (TEXT)
4. Insert `SchemaVersion` record with version `1` if table is empty

**Conventions:**
- All SQL uses parameterized queries via Dapper (`security-and-owasp.instructions.md`)
- Async methods with `CancellationToken`
- `ConfigureAwait(false)` on all awaits
- XML docs on the public `InitializeAsync` method

**Verification:**
- [ ] Database file created at correct path
- [ ] Tables exist with correct columns
- [ ] Idempotent — running twice doesn't error
- [ ] Commit: `feat(infrastructure): implement DatabaseInitializer with AuditLog, UserSettings, SchemaVersion tables`

---

### 1.36 — Implement `AuditLogRepository.cs`

**File:** `src/SysOpsCommander.Infrastructure/Database/AuditLogRepository.cs`

**Implements:** `IAuditLogRepository`

**Methods:**
- `InsertAsync` — parameterized INSERT including `AuthMethod`, `Transport`, `TargetDomain` columns
- `QueryAsync` — dynamic WHERE clause built from `AuditLogFilter` (each non-null filter adds an AND condition; use parameterized queries for all values)
- `DeleteOlderThanAsync` — `DELETE FROM AuditLog WHERE Timestamp < @cutoff`

**Conventions:**
- Constructor injects connection string or `DatabaseInitializer` for the path
- Use Dapper's `QueryAsync<T>` and `ExecuteAsync`
- All queries parameterized — no string concatenation

**Verification:**
- [ ] CRUD operations work against in-memory SQLite (`:memory:`)
- [ ] Commit: `feat(infrastructure): implement AuditLogRepository with Dapper`

---

### 1.37 — Implement `SettingsRepository.cs`

**File:** `src/SysOpsCommander.Infrastructure/Database/SettingsRepository.cs`

**Implements:** `ISettingsRepository`

**Methods:**
- `GetValueAsync` — `SELECT Value FROM UserSettings WHERE Key = @key`
- `SetValueAsync` — `INSERT OR REPLACE INTO UserSettings (Key, Value, LastModified) VALUES (@key, @value, @now)`
- `GetAllAsync` — `SELECT * FROM UserSettings`

**Commit:** `feat(infrastructure): implement SettingsRepository with Dapper`

---

### 1.38 — Implement `SettingsService.cs`

**File:** `src/SysOpsCommander.Services/SettingsService.cs`

**Implements:** `ISettingsService`

**Constructor dependencies:**
- `ISettingsRepository` — for per-user overrides
- `AppConfiguration` — for org-wide defaults from `appsettings.json`

**Layering logic in `GetEffectiveAsync(key)`:**
1. Try per-user override from `ISettingsRepository.GetValueAsync(key)` → if non-null, return it
2. Try org default from `AppConfiguration` via reflection or a dictionary mapping → if non-empty, return it
3. Fall back to `AppConstants` hard default

**Settings key mapping:**
| Key | AppConfiguration Property | AppConstants Fallback |
|-----|--------------------------|----------------------|
| `SharedScriptRepositoryPath` | `SharedScriptRepositoryPath` | `""` |
| `DefaultThrottle` | `DefaultThrottle` | `AppConstants.DefaultThrottle` |
| `DefaultTimeoutSeconds` | `DefaultTimeoutSeconds` | `AppConstants.DefaultWinRmTimeoutSeconds` |
| `DefaultWinRmTransport` | `DefaultWinRmTransport` | `"HTTP"` |
| `DefaultWinRmAuthMethod` | `DefaultWinRmAuthMethod` | `"Kerberos"` |
| `StaleComputerThresholdDays` | `StaleComputerThresholdDays` | `AppConstants.DefaultStaleComputerDays` |
| `AuditLogRetentionDays` | `AuditLogRetentionDays` | `AppConstants.AuditLogRetentionDays` |

**Commit:** `feat(services): implement SettingsService with three-tier settings layering`

---

### 1.39 — Update DI Registrations

**Action:** Replace Phase 0 stub registrations with real implementations for:
- `ISettingsRepository` → `SettingsRepository` (Singleton)
- `IAuditLogRepository` → `AuditLogRepository` (Singleton)
- `ISettingsService` → `SettingsService` (Singleton)
- `DatabaseInitializer` (Singleton)

Call `DatabaseInitializer.InitializeAsync()` during app startup in `App.xaml.cs`.

**Commit:** `build(app): register Phase 1 services in DI container`

---

### 1.40 — Write Unit Tests

**Files:**
- `tests/SysOpsCommander.Tests/Infrastructure/AuditLogRepositoryTests.cs`
- `tests/SysOpsCommander.Tests/Infrastructure/SettingsRepositoryTests.cs`
- `tests/SysOpsCommander.Tests/Services/SettingsServiceTests.cs`
- `tests/SysOpsCommander.Tests/Models/ScriptManifestTests.cs`
- `tests/SysOpsCommander.Tests/Models/WinRmConnectionOptionsTests.cs`

**Test cases:**

**`AuditLogRepositoryTests` (against in-memory SQLite):**
1. `InsertAsync_ValidEntry_Persists`
2. `QueryAsync_FilterByDateRange_ReturnsMatchingEntries`
3. `QueryAsync_FilterByAuthMethod_ReturnsMatchingEntries`
4. `QueryAsync_NoFilters_ReturnsAllEntries`
5. `DeleteOlderThanAsync_OldEntries_RemovesCorrectCount`

**`SettingsRepositoryTests` (against in-memory SQLite):**
1. `GetValueAsync_ExistingKey_ReturnsValue`
2. `GetValueAsync_MissingKey_ReturnsNull`
3. `SetValueAsync_NewKey_InsertsValue`
4. `SetValueAsync_ExistingKey_UpdatesValue`

**`SettingsServiceTests` (mock repository and AppConfiguration):**
1. `GetEffectiveAsync_PerUserOverrideExists_ReturnsOverride`
2. `GetEffectiveAsync_NoOverride_ReturnsOrgDefault`
3. `GetEffectiveAsync_NoOverrideNoOrgDefault_ReturnsHardDefault`
4. `GetEffectiveAsync_EmptyOrgDefault_ReturnsHardDefault`

**`ScriptManifestTests`:**
1. `Deserialize_ValidJson_MapsAllProperties`
2. `Deserialize_WithParameters_MapsParameterArray`
3. `Deserialize_ChoiceParameter_MapsChoicesArray`

**`WinRmConnectionOptionsTests`:**
1. `GetEffectivePort_HttpNoCustomPort_Returns5985`
2. `GetEffectivePort_HttpsNoCustomPort_Returns5986`
3. `GetEffectivePort_CustomPort_ReturnsCustom`
4. `CreateDefault_ReturnsKerberosHttp`

**Conventions:**
- All tests use FluentAssertions (`.Should().Be()`)
- All tests use NSubstitute for mocking
- Name pattern: `MethodName_Scenario_ExpectedBehavior`
- No AAA comments

**Commit:** `test(all): add Phase 1 unit tests for repositories, settings, and models`

---

### 1.41 — Phase 1 Verification

**Full acceptance criteria check:**
- [ ] All models, enums, and interfaces compile with XML documentation
- [ ] `DomainConnection` and `WinRmConnectionOptions` models correctly represent multi-domain and auth config
- [ ] `WinRmAuthMethod` and `WinRmTransport` enums include all three auth methods and both transports
- [ ] SQLite database is created on first run with correct schema (including new columns)
- [ ] Settings layering works: per-user override > `appsettings.json` org default > `AppConstants` hard default
- [ ] Audit log entries include WinRM auth/transport/domain fields
- [ ] `ScriptManifest` correctly deserializes sample JSON manifests
- [ ] All unit tests pass
- [ ] `dotnet build` — zero warnings
- [ ] Final commit: `chore: complete Phase 1 — core models, settings, and database infrastructure`

---

## Improvements & Notes

1. **Missing model definitions identified and added:** `AuditLogFilter` (step 1.20), `NotificationSeverity` (step 1.8), `ValidationResult` (step 1.21), `ManifestValidationResult` (step 1.22) — all referenced in the plan's interface signatures but never listed as Phase 1 deliverables.

2. **Repository interfaces added (step 1.34):** The plan defines service interfaces (`IAuditLogService`, `ISettingsService`) but no repository interfaces. Without `IAuditLogRepository` and `ISettingsRepository`, the service layer tests must use real SQLite, which makes them integration tests. Adding repository interfaces enables pure unit testing with NSubstitute.

3. **`DomainConnection` simplified (step 1.14):** The plan says `DomainConnection` should hold a `DirectoryEntry root path`. Storing a `DirectoryEntry` object is problematic — it's `IDisposable`, COM-backed, and not serializable. Replaced with `RootDistinguishedName` (a plain string) and deferred `DirectoryEntry` creation to the service layer.

4. **`ScriptManifest` test fixtures needed (step 1.40):** The actual JSON manifest files aren't created until Phase 5, but Phase 1 tests need to deserialize sample manifests. Embed test JSON strings in the test file or create a `TestData/` folder in the test project.

5. **`HostTarget` as ObservableObject:** The plan stores `HostTarget` in `ObservableCollection<HostTarget>`. If the UI binds to individual `HostTarget.Status` changes (e.g., during reachability checks), the model needs `INotifyPropertyChanged`. Making it extend `ObservableObject` is pragmatic — pure model purists might prefer a ViewModel wrapper, but for this project's scope it's acceptable.

6. **Missing `UpdateCheckResult` model:** Referenced in `IAutoUpdateService.CheckForUpdateAsync()` return type but not listed as a standalone model. Defined inline in step 1.32 — consider whether it deserves its own file.

7. **`PSCredential` dependency in Core interfaces:** `IExecutionStrategy` and `ICredentialService` reference `PSCredential` from `System.Management.Automation`. This means the Core project needs a reference to the PowerShell SDK (or at least the `System.Management.Automation` package). Consider whether this is acceptable or whether an abstraction like `ICredentialWrapper` should be used instead. For v1, the direct dependency is pragmatic.
