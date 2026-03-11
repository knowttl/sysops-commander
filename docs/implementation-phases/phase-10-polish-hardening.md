# Phase 10: Polish, Hardening & Comprehensive Testing

> **Goal:** Final phase — error-handling audit, security verification, performance sweep, accessibility review, documentation, and release preparation. This phase transforms a working application into a production-ready, maintainable product.
>
> **Prereqs:** Phases 0–9 complete. All features implemented, all in-phase unit tests passing.
>
> **Outputs:** Hardened application with complete documentation, verified security posture, and published release artifacts.

---

## Sub-Steps

### 10.1 — Error Handling Audit — CredSSP Failures

**Objective:** Verify that CredSSP-related failures produce clear, actionable error messages.

**Scenarios to test manually (document each in a test log file):**

| # | Scenario | Expected Behavior |
|---|----------|-------------------|
| 1 | Execute script on host where CredSSP Server is not enabled | Error: "CredSSP is not configured on target HOST01. Enable via GPO or `Enable-WSManCredSSP -Role Server`." |
| 2 | Execute script on host where CredSSP Client is not configured locally | Error: "CredSSP Client is not enabled on this machine. Run `Enable-WSManCredSSP -Role Client -DelegateComputer *` as admin." |
| 3 | Execute script with CredSSP auth but no credential provided | Blocked before execution: "CredSSP authentication requires explicit credentials." |
| 4 | Execute script with CredSSP using invalid credentials | Error: "CredSSP authentication failed. Verify the username and password." |

**Implementation:**
- Review `PowerShellRemoteStrategy` → ensure the WSMan/CredSSP error codes are mapped to user-friendly messages
- Add error code detection in the catch block:
  ```csharp
  if (ex.Message.Contains("CredSSP", StringComparison.OrdinalIgnoreCase))
  {
      // Map to specific user-friendly message
  }
  ```
- Ensure the audit log records the auth method and the failure reason

**Commit:** `fix(services): improve CredSSP error messages to be actionable`

---

### 10.2 — Error Handling Audit — Domain Switching

**Scenarios:**

| # | Scenario | Expected Behavior |
|---|----------|-------------------|
| 1 | Switch to an unreachable domain (network issue) | Error: "Cannot reach domain X. Connection timed out." Previous domain remains active. |
| 2 | Switch to a domain with insufficient permissions | Error: "Access denied for domain X. Verify credentials." Previous domain remains active. |
| 3 | Switch to a domain with no DCs resolvable | Error: "No domain controllers found for domain X." Previous domain remains active. |

**Implementation:**
- Review `ActiveDirectoryService.SwitchDomainAsync()` → verify graceful fallback to previous domain on failure
- Ensure the `SemaphoreSlim` is released even on exception (verify `try/finally` pattern)
- UI: show error toast + set status bar indicator to warning state

**Commit:** `fix(services): graceful fallback on domain switching failure`

---

### 10.3 — Error Handling Audit — Auto-Update Edge Cases

**Scenarios:**

| # | Scenario | Expected Behavior |
|---|----------|-------------------|
| 1 | `version.json` is missing from network share | Debug log: "No version.json found." No user notification. |
| 2 | `version.json` has invalid JSON | Warning log: "Invalid version.json." No user notification. |
| 3 | Network share is unreachable | Debug log: "Update share unreachable." No user notification. |
| 4 | Download zip → SHA256 mismatch | Error dialog: "Update package is corrupted (SHA256 mismatch). Download aborted." |
| 5 | Download zip → file is 0 bytes | Error dialog: "Download failed: empty file." |
| 6 | `SysOpsUpdater.exe` fails mid-copy (simulate by locking a file) | On next startup, detects orphaned `pending-update.json`, cleans up, continues normally |

**Implementation:**
- Review all `try/catch` blocks in `AutoUpdateService`
- Ensure none of these scenarios throw unhandled exceptions
- Verify the `CheckPendingUpdate()` cleanup path in `App.xaml.cs`

**Commit:** `fix(services): harden auto-update error handling for all edge cases`

---

### 10.4 — Security Verification — Credential Handling

**Security audit checklist:**

- [ ] **No credentials in logs:** Search all Serilog `.Information()`, `.Debug()`, `.Warning()`, `.Error()` calls — verify no `Password`, `SecureString`, or `PSCredential` properties are logged. The `CredentialDestructuringPolicy` should catch these, but verify manually.
- [ ] **No credentials in audit log:** Review `AuditLogEntry` model — verify the username is logged but never the password. Check the `LogExecutionAsync()` method in the audit service.
- [ ] **No credentials in settings:** Verify the `SettingsService` does not persist credential values. Only credential prompts (ephemeral) should access passwords.
- [ ] **No credentials in temp files:** Search for any temp file creation — verify no credential data is written.
- [ ] **DPAPI usage:** Verify `CredentialService` uses `ProtectedData.Protect()` / `ProtectedData.Unprotect()` with `DataProtectionScope.CurrentUser` if any credential caching is implemented.
- [ ] **Auth method recorded in audit:** Verify every execution record includes the auth method used (Kerberos/NTLM/CredSSP).

```powershell
# Automated check: search for credential leaks in source code
Get-ChildItem -Path src -Recurse -Include *.cs | Select-String -Pattern 'Password|SecureString|PSCredential' |
    Where-Object { $_.Line -notmatch '// |/// |CredentialDestructuring|SecureStringToString|credential\.Password' }
```

**Commit:** `security: audit credential handling — verify no leaks in logs, DB, or files`

---

### 10.5 — Security Verification — CredSSP Delegation

> **Improvement:** This step should be performed in a **lab environment** with at least 2 Windows hosts and a domain controller.

**Lab test procedure:**
1. Enable CredSSP Client on the app's machine
2. Enable CredSSP Server on the target machine
3. Execute a script using CredSSP auth
4. **Verify:** The delegated credential is scoped to the target machine only (not freely delegatable)
5. **Verify:** Wireshark or WinRM trace shows encrypted channel
6. **Verify:** Audit log records "CredSSP" as the auth method

**Document results in:** `docs/security/credssp-verification.md`

**Commit:** `docs: document CredSSP delegation security verification results`

---

### 10.6 — Security Verification — Input Sanitization

**Checklist:**

- [ ] **LDAP filters:** `LdapFilterSanitizer` escapes all 5 RFC 4515 special characters: `*`, `(`, `)`, `\`, `NUL`
- [ ] **Hostnames:** `HostnameValidator` rejects hostnames with injection characters (`|`, `;`, `&`, `$`, `` ` ``, `"`, `'`, `<`, `>`)
- [ ] **Script parameters:** Parameters injected via `AddParameter()` — no string concatenation anywhere
- [ ] **SQL queries:** All Dapper calls use parameterized queries — no string interpolation in SQL
- [ ] **File paths:** Script paths from settings validated against path traversal (`..`, absolute paths outside allowed directories)

**Commit:** `security: verify input sanitization across all user inputs`

---

### 10.7 — Performance Optimization Sweep

**Areas to profile:**

1. **AD tree loading:** Lazy load already implemented — verify it with 1000+ OUs
   - Load time for root nodes < 500ms
   - Expanding a node with 50 children < 200ms

2. **AD search:** Verify search with broad filter doesn't return unbounded results
   - Enforce `SizeLimit` on `DirectorySearcher` (e.g., 500)
   - Warn user if results are truncated

3. **Remote execution:** Verify parallel execution with `SemaphoreSlim`
   - 20 concurrent hosts should complete without timeout at throttle=5
   - Memory should stabilize (no leak from runspace accumulation)

4. **Audit log query:** Verify pagination works with 10,000+ entries
   - Page load < 200ms
   - SQLite index on `ExecutedAt`, `ScriptName`, `UserName` columns

5. **Script library loading:** Verify initial load of 50+ scripts < 1s
   - Manifest parsing is cached
   - Script content loaded lazily (on selection, not on list load)

6. **UI responsiveness:** All I/O operations are async — verify no UI freezes during:
   - Domain switching
   - Script execution
   - Export operations
   - Update check

**Implementation:**
- Add SQLite indexes if missing:
  ```sql
  CREATE INDEX IF NOT EXISTS IX_AuditLog_ExecutedAt ON AuditLog (ExecutedAt);
  CREATE INDEX IF NOT EXISTS IX_AuditLog_ScriptName ON AuditLog (ScriptName);
  CREATE INDEX IF NOT EXISTS IX_AuditLog_UserName ON AuditLog (UserName);
  ```
- Profile with Visual Studio Profiler — document hot paths

**Commit:** `perf: add database indexes and verify performance baselines`

---

### 10.8 — Accessibility Review

**WPF accessibility checklist:**

- [ ] All interactive controls have `AutomationProperties.Name` set
- [ ] Tab order is logical across all views (verify with Tab key only)
- [ ] All buttons with icons also have text labels or tooltips
- [ ] DataGrids have proper column headers for screen readers
- [ ] Focus indicators are visible (keyboard focus rectangles)
- [ ] Color contrast meets WCAG AA (4.5:1 for text, 3:1 for large text)
- [ ] Status messages (success/error) are announced via `AutomationProperties.LiveSetting="Polite"`
- [ ] Dialogs trap focus correctly and return focus on close
- [ ] Keyboard shortcuts don't conflict with Windows system shortcuts

**Implementation (if not already done):**
```xml
<!-- Example: Add automation properties -->
<Button Content="Execute"
        AutomationProperties.Name="Execute selected script on target hosts"
        AutomationProperties.HelpText="Runs the selected PowerShell script on all listed target hosts" />

<DataGrid AutomationProperties.Name="Execution results">
    <!-- columns -->
</DataGrid>
```

**Commit:** `a11y: add automation properties and verify keyboard accessibility`

---

### 10.9 — Documentation — `CONTRIBUTING.md`

**File:** `CONTRIBUTING.md` (project root)

**Required sections:**

1. **Prerequisites** — .NET 8 SDK, Visual Studio 2022 / VS Code + C# Dev Kit
2. **Building** — `dotnet build`, `dotnet test`
3. **Architecture overview** — Link to phase docs, explain MVVM layers
4. **Coding conventions** — Link to `.github/instructions/` files
5. **Commit message format** — Link to conventional-commit skill
6. **Creating a new script plugin** (step-by-step):
   - Step 1: Write the `.ps1` file with `param()` block
   - Step 2: Create the matching `.json` manifest (reference `manifest-schema.json`)
   - Step 3: Place both files in the shared script repository
   - Step 4: Refresh the Script Library in the app
   - Step 5: Expected result in the UI — show screenshot placeholder or ASCII layout
7. **Testing** — How to run tests, naming conventions, coverage expectations

**Commit:** `docs: create CONTRIBUTING.md with script plugin walkthrough`

---

### 10.10 — Documentation — Deployment Guide

**File:** `docs/deployment-guide.md`

**Required sections:**

1. **Prerequisites**
   - .NET 8 Runtime on the operator's machine
   - AD domain-joined machine
   - WinRM enabled on all target hosts

2. **Installation**
   - Copy published output to desired location
   - First-run: app creates SQLite database in `%LOCALAPPDATA%\SysOpsCommander\`
   - Populate `appsettings.json` with org-wide defaults

3. **WinRM Configuration on Targets**
   ```powershell
   # Enable WinRM (standard)
   Enable-PSRemoting -Force
   
   # Verify listener
   Get-WSManInstance -ResourceURI winrm/config/listener -Enumerate
   ```

4. **CredSSP Configuration (if needed)**
   - **Client side (operator machine):**
     ```powershell
     Enable-WSManCredSSP -Role Client -DelegateComputer *.corp.contoso.com -Force
     ```
   - **Server side (each target):**
     ```powershell
     Enable-WSManCredSSP -Role Server -Force
     ```
   - **GPO approach:** `Computer Configuration → Administrative Templates → System → Credentials Delegation → Allow Delegating Fresh Credentials` — add `WSMAN/*.corp.contoso.com`

5. **WinRM HTTPS Listener Setup (if required)**
   ```powershell
   # Create self-signed cert (or use enterprise CA)
   $cert = New-SelfSignedCertificate -DnsName "host01.corp.contoso.com" -CertStoreLocation "Cert:\LocalMachine\My"
   
   # Create HTTPS listener
   New-WSManInstance -ResourceURI winrm/config/Listener -SelectorSet @{
       Address = "*"; Transport = "HTTPS"
   } -ValueSet @{ CertificateThumbprint = $cert.Thumbprint }
   ```

6. **Firewall Requirements**
   | Port | Protocol | Direction | Purpose |
   |------|----------|-----------|---------|
   | 5985 | TCP | Inbound on targets | WinRM HTTP |
   | 5986 | TCP | Inbound on targets | WinRM HTTPS |

7. **Network Share for Updates**
   - Create `\\server\share\SysOpsCommander\`
   - Place `version.json` and `SysOpsCommander.zip`
   - Configure share in `appsettings.json`: `"UpdateNetworkSharePath": "\\\\server\\share\\SysOpsCommander"`

8. **appsettings.json Reference**

   | Key | Type | Default | Description |
   |-----|------|---------|-------------|
   | `DefaultDomain` | string | current domain | Domain to connect to on startup |
   | `DefaultWinRmAuthMethod` | string | `Kerberos` | Default auth method (Kerberos, NTLM, CredSSP) |
   | `DefaultWinRmTransport` | string | `HTTP` | Default transport (HTTP, HTTPS) |
   | `StaleComputerThresholdDays` | int | `90` | Days since last logon to consider a computer stale |
   | `SharedScriptRepositoryPath` | string | — | UNC path to org-wide script repo |
   | `DefaultThrottle` | int | `5` | Max concurrent remote executions |
   | `DefaultWinRmTimeoutSeconds` | int | `60` | WinRM operation timeout |
   | `UpdateNetworkSharePath` | string | — | UNC path for auto-update share |
   | `LogLevel` | string | `Information` | Serilog minimum level |

**Commit:** `docs: create deployment guide with CredSSP, HTTPS, and firewall instructions`

---

### 10.11 — Documentation — `appsettings.json` Sample

**File:** `src/SysOpsCommander.App/appsettings.json` (ensure complete)

```json
{
  "SysOpsCommander": {
    "DefaultDomain": "",
    "DefaultWinRmAuthMethod": "Kerberos",
    "DefaultWinRmTransport": "HTTP",
    "StaleComputerThresholdDays": 90,
    "SharedScriptRepositoryPath": "",
    "DefaultThrottle": 5,
    "DefaultTimeoutSeconds": 60,
    "UpdateNetworkSharePath": "",
    "LogLevel": "Information",
    "AuditLogRetentionDays": 365
  }
}
```

**Commit:** `docs: ensure complete appsettings.json with all documented keys`

---

### 10.12 — Release Preparation — Build Configuration

**Tasks:**
1. Verify `Directory.Build.props` sets `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`
2. Verify all projects build with zero warnings:
   ```powershell
   dotnet build SysOpsCommander.sln -warnaserror
   ```
3. Verify the publish profile includes `SysOpsUpdater.exe`:
   ```xml
   <!-- SysOpsCommander.App.csproj -->
   <ItemGroup>
       <None Include="$(SolutionDir)src\SysOpsUpdater\bin\$(Configuration)\net8.0\SysOpsUpdater.exe"
             CopyToOutputDirectory="PreserveNewest" />
   </ItemGroup>
   ```
   Or use a post-build event to copy the updater binary.

4. Create a publish script:
   ```powershell
   # publish.ps1
   dotnet publish src/SysOpsCommander.App/SysOpsCommander.App.csproj `
       -c Release -r win-x64 --self-contained false -o ./publish
   
   dotnet publish src/SysOpsUpdater/SysOpsUpdater.csproj `
       -c Release -r win-x64 --self-contained false -o ./publish
   
   # Create update package
   Compress-Archive -Path ./publish/* -DestinationPath ./SysOpsCommander.zip -Force
   
   # Generate SHA256
   $hash = (Get-FileHash ./SysOpsCommander.zip -Algorithm SHA256).Hash
   Write-Host "SHA256: $hash"
   Write-Host "Add this hash to version.json on the update share."
   ```

**Commit:** `build: configure publish profile and create publish script`

---

### 10.13 — Release Preparation — Sample `version.json`

**File:** `docs/samples/version.json`

```json
{
    "version": "1.0.0",
    "releaseDate": "2026-04-01",
    "releaseNotes": "Initial release of SysOps Commander.",
    "minimumVersion": "1.0.0",
    "sha256": "REPLACE_WITH_ACTUAL_HASH"
}
```

**Commit:** `docs: create sample version.json for update share`

---

### 10.14 — Write Integration-Level Smoke Tests

**File:** `tests/SysOpsCommander.Tests/Integration/SmokeTests.cs`

> These tests verify end-to-end paths without external dependencies (mocked AD, mocked WinRM).

**Test cases (5+):**

| # | Test Name | Scenario | Verification |
|---|-----------|----------|-------------|
| 1 | `DI_AllServicesResolvable` | Build DI container | All registered services resolve without error |
| 2 | `DatabaseInitializer_CreatesSchema` | Init in-memory DB | All tables exist |
| 3 | `SettingsService_RoundTrip` | Save + load setting | Value persisted and retrieved |
| 4 | `AuditLogService_WriteAndQuery` | Log execution + query | Entry found with correct fields |
| 5 | `ScriptLoader_LoadManifests` | Load from test scripts dir | Manifests parsed, scripts listed |
| 6 | `HostnameValidator_SecurityMatrix` | All injection patterns | All rejected |

**Commit:** `test(integration): add end-to-end smoke tests`

---

### 10.15 — Final Test Suite Execution

**Run the complete test suite:**
```powershell
dotnet test SysOpsCommander.sln --logger "console;verbosity=detailed" --collect:"XPlat Code Coverage"
```

**Coverage targets:**
- Services layer: > 80% line coverage
- ViewModels: > 70% line coverage (command + property change coverage)
- Validation: > 90% line coverage
- Infrastructure: > 60% line coverage (DB operations, file I/O)
- Overall: > 70% line coverage

**If coverage is below target:**
1. Identify uncovered methods via coverage report
2. Add targeted tests for the most critical uncovered paths
3. Do not write tests solely to increase coverage numbers — focus on meaningful assertions

**Commit:** `test: verify full test suite passes with acceptable coverage`

---

### 10.16 — Code Quality Final Pass

**Checklist:**
- [ ] No `TODO` comments without associated work items (remove or create issues)
- [ ] No `HACK` or `FIXME` comments remaining
- [ ] No commented-out code (use version control, not comments)
- [ ] All public members have XML documentation (`///`)
- [ ] All `async` methods accept `CancellationToken` and propagate it
- [ ] All `IDisposable` implementations have proper `Dispose()` patterns
- [ ] All `ConfigureAwait(false)` used in service/infrastructure layers (not in ViewModels)
- [ ] No `new` instantiation of service classes (all via DI)
- [ ] `IHostTargetingService` is registered as singleton (verify in DI)
- [ ] No raw SQL string concatenation (all parameterized Dapper)
- [ ] No `ps.AddScript($"...")` with interpolated user input (all `AddParameter`)
- [ ] `ILogger` replaced with `Serilog.ILogger` everywhere
- [ ] No empty catch blocks (at minimum, log the exception)

```powershell
# Automated checks
# 1. Find TODOs
Get-ChildItem -Path src -Recurse -Include *.cs | Select-String -Pattern 'TODO|HACK|FIXME' | Format-Table Path, LineNumber, Line

# 2. Find new service instantiations (excluding tests)
Get-ChildItem -Path src -Recurse -Include *.cs | Select-String -Pattern 'new\s+\w+Service\(' | Format-Table Path, LineNumber, Line

# 3. Find string interpolation in AddScript
Get-ChildItem -Path src -Recurse -Include *.cs | Select-String -Pattern 'AddScript\(\$"' | Format-Table Path, LineNumber, Line

# 4. Find empty catch blocks
Get-ChildItem -Path src -Recurse -Include *.cs | Select-String -Pattern 'catch.*\{\s*\}' | Format-Table Path, LineNumber, Line
```

**Commit:** `chore: code quality final pass — resolve all TODOs and verify conventions`

---

### 10.17 — Phase 10 Final Verification

**Complete acceptance criteria check:**

**Error Handling:**
- [ ] CredSSP failures produce actionable error messages (4 scenarios verified)
- [ ] Domain switching failure is graceful — falls back to previous domain
- [ ] Auto-update handles corrupted packages (SHA256 mismatch → clear error)
- [ ] Auto-update handles unreachable shares (silent, non-blocking)
- [ ] Auto-update handles malformed `version.json` (silent, non-blocking)

**Security:**
- [ ] No credentials in logs, database, settings, or temp files
- [ ] Auth method recorded in audit log for all three methods
- [ ] LDAP filter sanitization working for all special characters
- [ ] Hostname validation rejects all injection patterns
- [ ] All SQL is parameterized
- [ ] All script parameters use `AddParameter()`
- [ ] CredSSP delegation verified in lab (documented)

**Performance:**
- [ ] AD tree loading responsive with 1000+ OUs
- [ ] Audit log pagination fast with 10,000+ entries
- [ ] Database indexes in place
- [ ] No UI freezes during any I/O operation
- [ ] Memory stable during extended execution sessions

**Accessibility:**
- [ ] All interactive controls have automation properties
- [ ] Tab order is logical
- [ ] Keyboard shortcuts functional and documented

**Documentation:**
- [ ] `CONTRIBUTING.md` includes script plugin walkthrough
- [ ] Deployment guide covers CredSSP GPO, HTTPS listener, firewall
- [ ] `appsettings.json` fully documented with all keys
- [ ] `version.json` sample available

**Release:**
- [ ] `SysOpsUpdater.exe` included in published output
- [ ] Publish script generates correct output
- [ ] `dotnet build -warnaserror` — zero errors, zero warnings
- [ ] `dotnet test` — all tests pass
- [ ] Code coverage meets targets

**Final commit:** `chore: complete Phase 10 — Polish, Hardening & Release Preparation`

---

## Improvements & Notes

1. **Automated security scanning (v2):** Integrate a SAST tool (e.g., `dotnet security-scan` or Roslyn analyzers like `SecurityCodeScan`) into the CI pipeline for ongoing security detection beyond the manual audit in this phase.

2. **Performance regression testing (v2):** Consider adding `BenchmarkDotNet` benchmarks for the most critical hot paths (AD search, script execution dispatch, audit log queries). Run benchmarks in CI and fail on significant regressions.

3. **Telemetry (v2):** If the organization allows, consider adding anonymous usage telemetry (opt-in) to understand which features are most used, which scripts are most popular, and common error patterns. This data drives future development priorities.

4. **Error handling consistency:** Consider creating a centralized `ErrorHandler` or `IErrorDisplayService` that maps exception types to user-friendly messages. This avoids duplicating error message logic across ViewModels and ensures consistent user experience.

5. **Post-release monitoring:** After deployment, monitor:
   - Serilog log files for recurring errors
   - SQLite database size growth (audit log accumulation)
   - Auto-update success/failure rates
   - User feedback on CredSSP configuration difficulty

6. **Publish automation (v2):** Create a GitHub Actions or Azure DevOps pipeline that automates the build → test → publish → hash → `version.json` generation → network share deployment workflow. This eliminates manual steps and reduces human error in release preparation.
