# Phase 4: Remote Execution Engine

> **Goal:** Build the core execution engine with configurable WinRM authentication (Kerberos, NTLM, CredSSP), transport (HTTP/HTTPS), the strategy pattern for PowerShell/WMI execution, credential management, host targeting, notifications, and large-result streaming.
>
> **Prereqs:** Phase 3 complete (AD service provides host resolution). Phase 2 validators used by `HostTargetingService`.
>
> **Outputs:** `RemoteExecutionService`, `PowerShellRemoteStrategy`, `WmiQueryStrategy`, `CredentialService`, `HostTargetingService`, `NotificationService` — all fully implemented and tested.

---

## Sub-Steps

### 4.1 — Define `IExecutionStrategy` Interface (Already in Core)

**File:** `src/SysOpsCommander.Core/Interfaces/IExecutionStrategy.cs`

**Confirm the interface matches this signature:**
```csharp
public interface IExecutionStrategy
{
    ExecutionType Type { get; }
    Task<HostResult> ExecuteAsync(
        string hostname,
        string scriptContent,
        IDictionary<string, object>? parameters,
        PSCredential? credential,
        WinRmConnectionOptions connectionOptions,
        int timeoutSeconds,
        CancellationToken ct);
}
```

**Commit:** (Should already exist from Phase 1. Verify and update if needed.)

---

### 4.2 — Implement `PowerShellRemoteStrategy.cs` — Connection Setup

**File:** `src/SysOpsCommander.Services/Strategies/PowerShellRemoteStrategy.cs`

**Dependencies:**
- `Serilog.ILogger`

**`ExecuteAsync()` — Step 1: Build `WSManConnectionInfo`:**
```csharp
var useSsl = connectionOptions.Transport == WinRmTransport.HTTPS;
var port = connectionOptions.CustomPort
    ?? (useSsl ? AppConstants.WinRmHttpsPort : AppConstants.WinRmHttpPort);

var connInfo = new WSManConnectionInfo(
    useSsl,
    hostname,
    port,
    "/wsman",
    "http://schemas.microsoft.com/powershell/Microsoft.PowerShell",
    credential);

connInfo.AuthenticationMechanism = connectionOptions.AuthMethod switch
{
    WinRmAuthMethod.Kerberos => AuthenticationMechanism.Kerberos,
    WinRmAuthMethod.NTLM => AuthenticationMechanism.Negotiate,
    WinRmAuthMethod.CredSSP => AuthenticationMechanism.Credssp,
    _ => AuthenticationMechanism.Default
};

connInfo.OperationTimeout = TimeSpan.FromSeconds(timeoutSeconds);
connInfo.OpenTimeout = TimeSpan.FromSeconds(30);
```

> **Note:** `Negotiate` is used for NTLM because it allows NTLM fallback within Kerberos Negotiate. If strict NTLM-only is needed, use `AuthenticationMechanism.NegotiateWithImplicitCredential` — but `Negotiate` is safer for enterprise environments.

**Commit:** `feat(services): implement PowerShellRemoteStrategy connection setup`

---

### 4.3 — Implement `PowerShellRemoteStrategy.cs` — Script Execution

**`ExecuteAsync()` — Step 2: Execute the script:**
```csharp
using var runspace = RunspaceFactory.CreateRunspace(connInfo);
await Task.Run(() => runspace.Open(), ct);

using var ps = PowerShell.Create();
ps.Runspace = runspace;
ps.AddScript(scriptContent);

if (parameters != null)
{
    foreach (var kvp in parameters)
        ps.AddParameter(kvp.Key, kvp.Value);
}

var output = await ps.InvokeAsync();
```

**Critical:** Parameters injected via `AddParameter()` — NEVER string interpolation or concatenation.

**Output stream capture:**
```csharp
var outputText = string.Join(Environment.NewLine,
    output.Select(o => o?.ToString() ?? string.Empty));

var errorText = string.Join(Environment.NewLine,
    ps.Streams.Error.Select(e => e.ToString()));

var warningText = string.Join(Environment.NewLine,
    ps.Streams.Warning.Select(w => w.ToString()));
```

**Combine into `HostResult`:**
```csharp
return new HostResult
{
    Hostname = hostname,
    Status = ps.HadErrors ? HostStatus.Failed : HostStatus.Success,
    Output = outputText,
    ErrorOutput = errorText,
    WarningOutput = warningText,
    Duration = stopwatch.Elapsed
};
```

**Commit:** `feat(services): implement PowerShellRemoteStrategy script execution with AddParameter()`

---

### 4.4 — Implement `PowerShellRemoteStrategy.cs` — Error Mapping

**`ExecuteAsync()` — Step 3: Error handling with auth-specific messages:**

```csharp
catch (PSRemotingTransportException ex) when (ex.Message.Contains("Access is denied"))
{
    return HostResult.Failure(hostname,
        $"Authentication failed for {hostname} using {connectionOptions.AuthMethod}. " +
        $"Verify credentials and that {connectionOptions.AuthMethod} is enabled on the target.");
}
catch (PSRemotingTransportException ex) when (ex.Message.Contains("CredSSP"))
{
    return HostResult.Failure(hostname,
        $"CredSSP authentication failed for {hostname}. " +
        "Ensure CredSSP is enabled via GPO on both client and server.");
}
catch (PSRemotingTransportException ex)
{
    return HostResult.Failure(hostname,
        $"WinRM connection failed to {hostname} on {connectionOptions.Transport}:{port}. " +
        $"Verify WinRM is enabled and the {connectionOptions.Transport} listener is configured. " +
        $"Error: {ex.Message}");
}
catch (UnauthorizedAccessException)
{
    return HostResult.Failure(hostname,
        $"Access denied to {hostname}. Check credentials and remote management permissions.");
}
catch (OperationCanceledException)
{
    return HostResult.Cancelled(hostname);
}
catch (Exception ex) when (IsTimeout(ex))
{
    return HostResult.Timeout(hostname, timeoutSeconds);
}
```

**Commit:** `feat(services): implement auth-specific error mapping in PowerShellRemoteStrategy`

---

### 4.5 — Implement `WmiQueryStrategy.cs` — Connection and Execution

**File:** `src/SysOpsCommander.Services/Strategies/WmiQueryStrategy.cs`

**Dependencies:**
- `Serilog.ILogger`

**Auth mapping:**
```csharp
var connOpts = new ConnectionOptions
{
    Authentication = AuthenticationLevel.PacketPrivacy,
    Impersonation = ImpersonationLevel.Impersonate,
    Timeout = TimeSpan.FromSeconds(timeoutSeconds)
};

if (credential != null)
{
    connOpts.Username = credential.UserName;
    connOpts.SecurePassword = credential.Password;
}
```

**WMI execution:**
```csharp
var scope = new ManagementScope($"\\\\{hostname}\\root\\cimv2", connOpts);
await Task.Run(() => scope.Connect(), ct);

using var searcher = new ManagementObjectSearcher(scope, new ObjectQuery(scriptContent));
var results = await Task.Run(() => searcher.Get(), ct);
```

> **Improvement:** WMI `AuthenticationLevel` mapping doesn't distinguish between Kerberos/NTLM/CredSSP — WMI uses DCOM authentication which handles this differently. The `ConnectionOptions.Authentication` controls packet security, not the auth protocol. Document this limitation: WMI always uses the Windows authentication stack, and the `WinRmAuthMethod` enum has limited applicability to WMI queries.

**Error mapping:** Same pattern as PowerShell strategy — map `ManagementException`, `UnauthorizedAccessException`, `COMException` to descriptive messages.

**Commit:** `feat(services): implement WmiQueryStrategy with configurable credentials`

---

### 4.6 — Implement `RemoteExecutionService.cs` — Orchestration

**File:** `src/SysOpsCommander.Services/RemoteExecutionService.cs`

**Dependencies:**
- `IEnumerable<IExecutionStrategy>` (all registered strategies)
- `IHostTargetingService`
- `IAuditLogService`
- `INotificationService`
- `Serilog.ILogger`

**`ExecuteAsync(ExecutionJob job, IProgress<HostResult> progress, CancellationToken ct)` flow:**
1. Select the correct strategy based on `job.ExecutionType`
2. Run pre-flight reachability check (step 4.7)
3. Execute against all reachable hosts in parallel with throttle (step 4.8)
4. Log audit entry for the execution
5. Fire notification via `INotificationService`
6. Return completed `ExecutionJob` with all results

**Commit:** `feat(services): implement RemoteExecutionService orchestration`

---

### 4.7 — Implement Pre-Flight Reachability Check

**Part of `RemoteExecutionService.ExecuteAsync()`:**

1. Determine target port from `job.WinRmConnectionOptions`:
   - HTTP → `AppConstants.WinRmHttpPort` (5985)
   - HTTPS → `AppConstants.WinRmHttpsPort` (5986)
2. For each host in `job.TargetHosts`:
   - Attempt TCP connect to `hostname:port` with 2-second timeout
   - Set `HostTarget.Status` to `Reachable` or `Unreachable`
3. Execute in parallel with `AppConstants.ReachabilityCheckParallelism` (20)
4. Filter out unreachable hosts — mark their `HostResult.Status = HostStatus.Unreachable`
5. Log at `Information`: "Pre-flight check: {reachable}/{total} hosts reachable on port {port}"

> **Improvement:** Make the per-host TCP timeout configurable rather than hardcoded at 2 seconds. In high-latency WAN environments, 2 seconds may not be sufficient. Default to 2 seconds but allow override in `AppConstants`.

**Commit:** `feat(services): implement pre-flight TCP reachability check`

---

### 4.8 — Implement Parallel Execution with Throttle

**Part of `RemoteExecutionService.ExecuteAsync()`:**

```csharp
var semaphore = new SemaphoreSlim(job.ThrottleLimit);
var tasks = reachableHosts.Select(async host =>
{
    await semaphore.WaitAsync(ct);
    try
    {
        host.Status = HostStatus.Running;
        var result = await strategy.ExecuteAsync(
            host.Hostname,
            job.ScriptContent,
            job.Parameters,
            job.Credential,
            job.WinRmConnectionOptions,
            job.TimeoutSeconds,
            ct);

        progress?.Report(result);
        return result;
    }
    catch (OperationCanceledException)
    {
        return HostResult.Cancelled(host.Hostname);
    }
    catch (Exception ex)
    {
        _logger.Error(ex, "Execution failed on {Hostname}", host.Hostname);
        return HostResult.Failure(host.Hostname, ex.Message);
    }
    finally
    {
        semaphore.Release();
    }
});

var results = await Task.WhenAll(tasks);
```

**Error isolation:** Each host runs in its own try/catch. One host failure does not affect others.

**Commit:** `feat(services): implement parallel execution with SemaphoreSlim throttle`

---

### 4.9 — Implement Large Result Streaming

**Part of `RemoteExecutionService.ExecuteAsync()` — post-processing:**

1. Track cumulative output size across all `HostResult.Output` strings
2. If total exceeds `AppConstants.MaxInMemoryResultBytes` (10 MB):
   - For each result, write output to a temp file at `%LOCALAPPDATA%\SysOpsCommander\Temp\{jobId}_{hostname}.txt`
   - Set `HostResult.Output` to the file path
   - Set `HostResult.IsFileReference = true`
3. UI reads file content on demand when the user selects a specific host result

> **Improvement:** Also implement individual host result size cap. A single host returning 50 MB of output should stream to disk regardless of total cumulative size.

**Cleanup:** On application exit, delete temp files older than 24 hours from the Temp directory.

**Commit:** `feat(services): implement large result streaming to disk for outputs exceeding 10MB`

---

### 4.10 — Implement `ICredentialService` and `CredentialService.cs`

**File:** `src/SysOpsCommander.Services/CredentialService.cs`

**Dependencies:**
- `Serilog.ILogger`

**Methods:**

**`PromptForCredentials()` — event-based pattern:**
- Define `event EventHandler<CredentialRequestEventArgs>? CredentialRequested`
- When called, raise the event → ViewModel subscribes and shows the `CredentialDialog`
- Returns `Task<PSCredential?>` (null if user cancels)

**`ValidateCredentialsAsync(PSCredential credential, string? targetDomain, CancellationToken ct)`:**
1. Attempt LDAP bind to the target domain (or current domain if null) using the provided credentials
2. If bind success → return `ValidationResult.Success()`
3. If bind fails → return `ValidationResult.Failure("Credentials are invalid for domain {domain}.")`
4. Wrap in try/catch for `DirectoryServicesCOMException`

**`DisposeCredentials(PSCredential credential)`:**
- Dispose the `SecureString` password
- Null the reference

> **Security (critical):** Never log credential values. Never store `PSCredential` beyond the execution lifetime. Dispose immediately after execution completes.

**Commit:** `feat(services): implement CredentialService with LDAP bind validation`

---

### 4.11 — Implement `IHostTargetingService` and `HostTargetingService.cs` (SINGLETON)

**File:** `src/SysOpsCommander.Services/HostTargetingService.cs`

**Dependencies:**
- `Serilog.ILogger`

**Properties:**
```csharp
public ObservableCollection<HostTarget> Targets { get; } = new();
```

**Methods:**

**`AddFromHostnames(IEnumerable<string> hostnames)`:**
1. Validate each hostname via `HostnameValidator.ValidateMany()`
2. Filter out invalid — log at `Warning`: "Skipped invalid hostname: {hostname} — {error}"
3. De-duplicate against existing `Targets` (case-insensitive comparison)
4. Add valid, unique hostnames to `Targets`
5. Log at `Information`: "Added {count} targets ({skipped} duplicates, {invalid} invalid)"

**`AddFromCsvFile(string filePath)`:**
1. Read file (one hostname per line, skip empty lines and lines starting with `#`)
2. Delegate to `AddFromHostnames()`

**`AddFromAdSearchResults(IEnumerable<AdObject> computers)`:**
1. Extract `dNSHostName` attribute, fall back to `cn` if missing
2. Delegate to `AddFromHostnames()`

**`CheckReachabilityAsync(CancellationToken ct)`:**
1. TCP connect test on the configured WinRM port for each target
2. Parallel with `AppConstants.ReachabilityCheckParallelism` (20)
3. Update each `HostTarget.Status` to `Reachable` or `Unreachable`
4. Since `Targets` is `ObservableCollection`, changes auto-propagate to UI

> **Improvement (thread safety):** `ObservableCollection<T>` is not thread-safe. When modifying from background threads (parallel reachability check), use `Dispatcher.Invoke()` or `BindingOperations.EnableCollectionSynchronization()`. Register the synchronization lock during construction:
> ```csharp
> private readonly object _lockObj = new();
> public HostTargetingService()
> {
>     BindingOperations.EnableCollectionSynchronization(Targets, _lockObj);
> }
> ```

**`ClearTargets()` / `RemoveTarget(string hostname)`:**
- Simple collection operations with appropriate logging

**Commit:** `feat(services): implement HostTargetingService (singleton) with validation and reachability`

---

### 4.12 — Implement `INotificationService` and `NotificationService.cs`

**File:** `src/SysOpsCommander.Services/NotificationService.cs`

**Dependencies:**
- `Serilog.ILogger`

**Methods:**

**`ShowToast(string title, string message, NotificationSeverity severity)`:**
```csharp
new ToastContentBuilder()
    .AddText(title)
    .AddText(message)
    .Show();
```

**`ShowExecutionComplete(ExecutionJob job)`:**
```csharp
var successCount = job.Results.Count(r => r.Status == HostStatus.Success);
var failCount = job.Results.Count(r => r.Status == HostStatus.Failed);

new ToastContentBuilder()
    .AddText($"Execution Complete: {job.ScriptName}")
    .AddText($"{successCount}/{job.Results.Count} succeeded, {failCount} failed")
    .AddArgument("action", "viewResults")
    .AddArgument("jobId", job.Id.ToString())
    .Show();
```

**Toast click handling:** Register activation handler in `App.xaml.cs` via `ToastNotificationManagerCompat.OnActivated()`. Navigate to Execution view and select the completed job.

**Commit:** `feat(services): implement NotificationService with toast notifications`

---

### 4.13 — Register All Phase 4 Services in DI

**Action:** Update `ServiceCollectionExtensions.cs`:
```csharp
// Strategies
services.AddSingleton<IExecutionStrategy, PowerShellRemoteStrategy>();
services.AddSingleton<IExecutionStrategy, WmiQueryStrategy>();

// Services
services.AddSingleton<IRemoteExecutionService, RemoteExecutionService>();
services.AddSingleton<ICredentialService, CredentialService>();
services.AddSingleton<IHostTargetingService, HostTargetingService>();  // SINGLETON — critical
services.AddSingleton<INotificationService, NotificationService>();
```

> **Note:** Both strategies registered as `IExecutionStrategy` — `RemoteExecutionService` resolves the correct one from `IEnumerable<IExecutionStrategy>` by matching `strategy.Type == job.ExecutionType`.

**Commit:** `build(app): register Phase 4 services in DI container`

---

### 4.14 — Write Unit Tests — `PowerShellRemoteStrategy`

**File:** `tests/SysOpsCommander.Tests/Services/PowerShellRemoteStrategyTests.cs`

**Test cases (6+):**

| # | Test Name | Scenario | Verification |
|---|-----------|----------|-------------|
| 1 | `Execute_KerberosHttp_CorrectConnectionInfo` | Kerberos + HTTP | Auth = Kerberos, SSL = false, Port = 5985 |
| 2 | `Execute_NtlmHttps_CorrectConnectionInfo` | NTLM + HTTPS | Auth = Negotiate, SSL = true, Port = 5986 |
| 3 | `Execute_CredSspHttp_CorrectConnectionInfo` | CredSSP + HTTP | Auth = CredSSP, SSL = false |
| 4 | `Execute_CustomPort_UsesCustomPort` | CustomPort = 9999 | Port = 9999 |
| 5 | `Execute_ParametersInjectedViaAddParameter` | 3 parameters | `ps.Commands` contains all 3 parameters |
| 6 | `Execute_NoStringInterpolationInScript` | Script content checked | Script text is unmodified |

> **Note:** Full execution tests require WinRM connectivity. These tests should verify the setup/configuration logic using reflection or by inspecting the `WSManConnectionInfo` object properties.

**Commit:** `test(services): add PowerShellRemoteStrategy connection configuration tests`

---

### 4.15 — Write Unit Tests — `RemoteExecutionService`

**File:** `tests/SysOpsCommander.Tests/Services/RemoteExecutionServiceTests.cs`

**Test cases (7+):**

| # | Test Name | Scenario | Verification |
|---|-----------|----------|-------------|
| 1 | `Execute_ThrottleEnforced_MaxConcurrentRespected` | 3 hosts, throttle=1 | Hosts execute sequentially |
| 2 | `Execute_Cancellation_StopsRemainingHosts` | Cancel mid-execution | Remaining hosts get `Cancelled` status |
| 3 | `Execute_PerHostErrorIsolation_OtherHostsContinue` | 1st host throws | 2nd and 3rd hosts still execute |
| 4 | `Execute_ProgressReported_ForEachHost` | 3 hosts | `IProgress<HostResult>.Report()` called 3 times |
| 5 | `Execute_LargeResult_StreamsToDisk` | Output > 10 MB | `IsFileReference = true` |
| 6 | `Execute_AllHostsUnreachable_ReturnsUnreachableResults` | All TCP fail | All `HostStatus.Unreachable` |
| 7 | `Execute_SelectsCorrectStrategy_ByExecutionType` | PowerShell type | Uses `PowerShellRemoteStrategy` |

**Mocking:** Use NSubstitute to mock `IExecutionStrategy`, `IAuditLogService`, `INotificationService`.

**Commit:** `test(services): add RemoteExecutionService orchestration tests`

---

### 4.16 — Write Unit Tests — `HostTargetingService`

**File:** `tests/SysOpsCommander.Tests/Services/HostTargetingServiceTests.cs`

**Test cases (8+):**

| # | Test Name | Scenario | Verification |
|---|-----------|----------|-------------|
| 1 | `AddFromHostnames_ValidHostnames_AddsToTargets` | 3 valid hostnames | `Targets.Count == 3` |
| 2 | `AddFromHostnames_InvalidHostname_SkipsAndLogs` | 1 valid + 1 invalid | `Targets.Count == 1`, warning logged |
| 3 | `AddFromHostnames_Duplicates_DeDuplicated` | Same hostname twice | `Targets.Count == 1` |
| 4 | `AddFromHostnames_CaseInsensitiveDeDup` | "SERVER" + "server" | `Targets.Count == 1` |
| 5 | `AddFromCsvFile_ValidFile_ParsesAll` | CSV with 5 hosts | `Targets.Count == 5` |
| 6 | `AddFromCsvFile_SkipsComments` | Lines starting with `#` | Comments not added |
| 7 | `ClearTargets_RemovesAll` | 3 targets → clear | `Targets.Count == 0` |
| 8 | `RemoveTarget_ExistingHost_Removed` | Remove "SERVER01" | Not in `Targets` |

**Commit:** `test(services): add HostTargetingService validation and targeting tests`

---

### 4.17 — Write Unit Tests — `CredentialService`

**File:** `tests/SysOpsCommander.Tests/Services/CredentialServiceTests.cs`

**Test cases (3+):**

| # | Test Name | Scenario | Verification |
|---|-----------|----------|-------------|
| 1 | `PromptForCredentials_RaisesEvent` | Call prompt | `CredentialRequested` event fires |
| 2 | `DisposeCredentials_DisposesSecureString` | Dispose valid credential | SecureString disposed |
| 3 | `ValidateCredentials_InvalidCreds_ReturnsFailure` | Wrong password | `ValidationResult.Failure` |

**Commit:** `test(services): add CredentialService tests`

---

### 4.18 — Phase 4 Verification

**Full acceptance criteria check:**
- [ ] WinRM connection uses correct auth method (Kerberos, NTLM, CredSSP) based on configuration
- [ ] WinRM connection uses correct transport and port (HTTP/5985 or HTTPS/5986)
- [ ] CredSSP failure produces a clear error with remediation steps
- [ ] Script parameters are injected via `AddParameter()` (verified by unit test)
- [ ] Parallel execution respects throttle limit
- [ ] Cancellation correctly stops remaining hosts
- [ ] Per-host error isolation works (one failure doesn't affect others)
- [ ] Large results (>10MB cumulative) stream to disk with `IsFileReference` flag
- [ ] Progress reporting delivers real-time updates via `IProgress<HostResult>`
- [ ] Host reachability pre-check uses the correct port for configured transport
- [ ] Toast notification fires on execution completion
- [ ] `IHostTargetingService` is a singleton and its `Targets` collection is observable
- [ ] `CredentialService` validates via LDAP bind and never logs credential values
- [ ] All unit tests pass (24+ new cases)
- [ ] `dotnet build` — zero warnings
- [ ] `dotnet test` — all tests pass (Phase 0–4)
- [ ] Final commit: `chore: complete Phase 4 — remote execution engine`

---

## Improvements & Notes

1. **WMI auth mapping limitation (step 4.5):** WMI uses DCOM authentication, not WinRM. The `WinRmAuthMethod` enum doesn't map cleanly to WMI's `ConnectionOptions.Authentication`, which controls packet security level, not the auth protocol. Document this: WMI always uses the Windows auth stack (Kerberos or NTLM based on environment), and the explicit auth selection in the UI primarily affects PowerShell WinRM connections.

2. **Temp file cleanup (step 4.9):** Implement a cleanup routine in `App.xaml.cs` shutdown handler that deletes temp files older than 24 hours. Also consider a startup cleanup pass for orphaned files from crashed sessions.

3. **Thread-safe `ObservableCollection` (step 4.11):** `BindingOperations.EnableCollectionSynchronization()` is the standard WPF approach for thread-safe ObservableCollection. This must be called on the UI thread during construction. Since `HostTargetingService` is registered as singleton, its constructor runs during DI container build — which is on the UI thread in `App.xaml.cs`. This timing is correct.

4. **Per-host TCP timeout (step 4.7):** The 2-second TCP connect timeout is hardcoded. For high-latency WAN environments (e.g., cross-site management), consider making this configurable via `AppConstants.TcpConnectTimeoutMs` with a default of 2000. Add to `UserSettings` in a future version.

5. **`PSCredential` dependency in Core (Phase 1 note):** `IExecutionStrategy` references `PSCredential` (from `System.Management.Automation`). This forces the Core project to reference the PowerShell SDK, which is a heavy dependency for a "dependency-free" core project. Consider wrapping credentials in a custom `ExecutionCredential` class in Core, with `PSCredential` conversion only in the Services layer. For v1, the direct reference is acceptable but should be addressed in v2.

6. **Notification toast click navigation:** The `ToastNotificationManagerCompat.OnActivated()` handler in `App.xaml.cs` needs to marshal back to the UI thread and navigate the `MainWindowViewModel` to the Execution view. This cross-cutting concern should use `IServiceProvider.GetRequiredService<MainWindowViewModel>()` to access the ViewModel from the app context.
