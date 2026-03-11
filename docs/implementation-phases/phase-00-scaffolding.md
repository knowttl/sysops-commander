# Phase 0: Project Scaffolding & Foundation

> **Goal:** Create the solution structure, wire dependency injection, configure logging and application configuration, and verify the build pipeline works end-to-end. No features yet — just a solid skeleton that every subsequent phase builds on.
>
> **Prereqs:** None — this is the starting point.
>
> **Outputs:** A buildable .NET 8 solution with 6 projects, DI, Serilog, appsettings.json, a launchable WPF window, and passing smoke tests.

---

## Sub-Steps

### 0.1 — Initialize Git Repository

**Action:** Create a Git repository at the workspace root with a comprehensive `.gitignore`.

**File:** `.gitignore` (workspace root)

**Content must include:**
```
bin/
obj/
.vs/
*.user
*.suo
packages/
*.db
*.log
TestResults/
BenchmarkDotNet.Artifacts/
```

**Verification:**
- [ ] `git init` succeeds
- [ ] `.gitignore` exists at workspace root
- [ ] Initial commit: `chore: initialize git repository with .gitignore`

---

### 0.2 — Create `.editorconfig`

> **Improvement:** The plan does not include an `.editorconfig`, but `csharp.instructions.md` references "code-formatting style defined in `.editorconfig`". Adding this ensures consistent formatting across all contributors.

**Action:** Create `.editorconfig` at the workspace root.

**File:** `.editorconfig` (workspace root)

**Key settings:**
```ini
root = true

[*]
indent_style = space
indent_size = 4
end_of_line = crlf
charset = utf-8
trim_trailing_whitespace = true
insert_final_newline = true

[*.cs]
# Namespace
csharp_style_namespace_declarations = file_scoped:warning
# Null checks
dotnet_style_prefer_is_null_check_for_reference_equality_checks = true:warning
# var usage
csharp_style_var_for_built_in_types = false:suggestion
csharp_style_var_when_type_is_apparent = true:suggestion
# Expression bodies
csharp_style_expression_bodied_methods = when_on_single_line:suggestion
csharp_style_expression_bodied_properties = true:suggestion
# Pattern matching
csharp_style_pattern_matching_over_is_with_cast_check = true:warning
csharp_style_pattern_matching_over_as_with_null_check = true:warning
# Analyzers
dotnet_analyzer_diagnostic.severity = warning

[*.xaml]
indent_size = 4

[*.json]
indent_size = 2

[*.md]
trim_trailing_whitespace = false
```

**Verification:**
- [ ] File exists and is valid
- [ ] Commit: `build: add .editorconfig for consistent code formatting`

---

### 0.3 — Create `global.json`

> **Improvement:** Pin the .NET SDK version so all developers and CI builds use the same toolchain. Not in the original plan.

**Action:** Create `global.json` at the workspace root.

**File:** `global.json` (workspace root)

```json
{
  "sdk": {
    "version": "8.0.400",
    "rollForward": "latestFeature"
  }
}
```

**Verification:**
- [ ] `dotnet --version` returns an 8.x SDK version
- [ ] Commit: `build: add global.json to pin .NET 8 SDK`

---

### 0.4 — Create `Directory.Build.props`

> **Improvement:** Centralize shared MSBuild properties across all 6 projects. Not explicitly called out in the plan, but required for zero-warning builds and consistent settings.

**Action:** Create `Directory.Build.props` at the workspace root.

**File:** `Directory.Build.props` (workspace root)

```xml
<Project>
  <PropertyGroup>
    <TargetFramework>net8.0-windows</TargetFramework>
    <LangVersion>12</LangVersion>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <EnforceCodeStyleInBuild>true</EnforceCodeStyleInBuild>
    <AnalysisLevel>latest-recommended</AnalysisLevel>
  </PropertyGroup>
</Project>
```

> **Note:** The `Tests` project may need `<TreatWarningsAsErrors>false</TreatWarningsAsErrors>` overridden locally if analyzers generate test-specific warnings. Prefer fixing warnings first.

**Verification:**
- [ ] File exists in workspace root
- [ ] Commit: `build: add Directory.Build.props for shared project settings`

---

### 0.5 — Create Solution File

**Action:** Create the solution file.

**Commands:**
```powershell
dotnet new sln -n SysOpsCommander
```

**File:** `SysOpsCommander.sln` (workspace root)

**Verification:**
- [ ] Solution file exists
- [ ] Commit: `build: create SysOpsCommander solution`

---

### 0.6 — Create `SysOpsCommander.Core` Class Library

**Action:** Create the Core project. This is the dependency root — no project references.

**Commands:**
```powershell
dotnet new classlib -n SysOpsCommander.Core -o src/SysOpsCommander.Core
dotnet sln add src/SysOpsCommander.Core
```

**Post-creation:**
- Delete the auto-generated `Class1.cs`
- Create empty directory structure:
  ```
  src/SysOpsCommander.Core/
  ├── Interfaces/
  ├── Models/
  ├── Enums/
  ├── Constants/
  └── Validation/
  ```

**Verification:**
- [ ] Project compiles with zero warnings
- [ ] No project references
- [ ] Directory structure created

---

### 0.7 — Create `SysOpsCommander.Services` Class Library

**Action:** Create the Services project. References Core only.

**Commands:**
```powershell
dotnet new classlib -n SysOpsCommander.Services -o src/SysOpsCommander.Services
dotnet sln add src/SysOpsCommander.Services
dotnet add src/SysOpsCommander.Services reference src/SysOpsCommander.Core
```

**Post-creation:**
- Delete the auto-generated `Class1.cs`
- Create `Strategies/` subdirectory

**Verification:**
- [ ] Project compiles
- [ ] References only `SysOpsCommander.Core`

---

### 0.8 — Create `SysOpsCommander.Infrastructure` Class Library

**Action:** Create the Infrastructure project. References Core only.

**Commands:**
```powershell
dotnet new classlib -n SysOpsCommander.Infrastructure -o src/SysOpsCommander.Infrastructure
dotnet sln add src/SysOpsCommander.Infrastructure
dotnet add src/SysOpsCommander.Infrastructure reference src/SysOpsCommander.Core
```

**Post-creation:**
- Delete `Class1.cs`
- Create directory structure:
  ```
  src/SysOpsCommander.Infrastructure/
  ├── Database/
  ├── Logging/
  └── FileSystem/
  ```

**Verification:**
- [ ] Project compiles
- [ ] References only `SysOpsCommander.Core`

---

### 0.9 — Create `SysOpsCommander.ViewModels` Class Library

**Action:** Create the ViewModels project. References Core only.

**Commands:**
```powershell
dotnet new classlib -n SysOpsCommander.ViewModels -o src/SysOpsCommander.ViewModels
dotnet sln add src/SysOpsCommander.ViewModels
dotnet add src/SysOpsCommander.ViewModels reference src/SysOpsCommander.Core
```

**Post-creation:**
- Delete `Class1.cs`
- Create `Dialogs/` subdirectory

**Verification:**
- [ ] Project compiles
- [ ] References only `SysOpsCommander.Core`
- [ ] Does NOT reference any `System.Windows.*` assemblies

---

### 0.10 — Create `SysOpsCommander.App` WPF Application

**Action:** Create the WPF application project. References all 4 source projects.

**Commands:**
```powershell
dotnet new wpf -n SysOpsCommander.App -o src/SysOpsCommander.App
dotnet sln add src/SysOpsCommander.App
dotnet add src/SysOpsCommander.App reference src/SysOpsCommander.Core
dotnet add src/SysOpsCommander.App reference src/SysOpsCommander.Services
dotnet add src/SysOpsCommander.App reference src/SysOpsCommander.Infrastructure
dotnet add src/SysOpsCommander.App reference src/SysOpsCommander.ViewModels
```

**Post-creation:**
- Create directory structure:
  ```
  src/SysOpsCommander.App/
  ├── Views/
  ├── Dialogs/
  ├── Converters/
  ├── Resources/
  └── DependencyInjection/
  ```

**Verification:**
- [ ] WPF app launches (blank window)
- [ ] References Core, Services, Infrastructure, ViewModels

---

### 0.11 — Create `SysOpsCommander.Tests` xUnit Project

**Action:** Create the test project. References all 4 source projects (not App).

**Commands:**
```powershell
dotnet new xunit -n SysOpsCommander.Tests -o tests/SysOpsCommander.Tests
dotnet sln add tests/SysOpsCommander.Tests
dotnet add tests/SysOpsCommander.Tests reference src/SysOpsCommander.Core
dotnet add tests/SysOpsCommander.Tests reference src/SysOpsCommander.Services
dotnet add tests/SysOpsCommander.Tests reference src/SysOpsCommander.Infrastructure
dotnet add tests/SysOpsCommander.Tests reference src/SysOpsCommander.ViewModels
```

**Post-creation:**
- Delete auto-generated `UnitTest1.cs`
- Create directory structure:
  ```
  tests/SysOpsCommander.Tests/
  ├── ViewModels/
  ├── Services/
  ├── Validation/
  ├── Infrastructure/
  └── Security/
  ```

**Verification:**
- [ ] `dotnet test` runs (zero tests, zero failures)

---

### 0.12 — Install NuGet Packages

**Action:** Install all NuGet packages per the technology stack table. Use `dotnet add package` per `.github/skills/nuget-manager/SKILL.md` — never edit `.csproj` directly for adding packages.

**Core project:**
```powershell
dotnet add src/SysOpsCommander.Core package CommunityToolkit.Mvvm
dotnet add src/SysOpsCommander.Core package Microsoft.Extensions.Configuration.Abstractions
```

**Services project:**
```powershell
dotnet add src/SysOpsCommander.Services package Microsoft.PowerShell.SDK
dotnet add src/SysOpsCommander.Services package System.DirectoryServices
dotnet add src/SysOpsCommander.Services package System.DirectoryServices.Protocols
dotnet add src/SysOpsCommander.Services package System.Management
dotnet add src/SysOpsCommander.Services package CsvHelper
dotnet add src/SysOpsCommander.Services package ClosedXML
dotnet add src/SysOpsCommander.Services package Microsoft.Toolkit.Uwp.Notifications
```

**Infrastructure project:**
```powershell
dotnet add src/SysOpsCommander.Infrastructure package Serilog
dotnet add src/SysOpsCommander.Infrastructure package Serilog.Sinks.File
dotnet add src/SysOpsCommander.Infrastructure package Serilog.Sinks.Console
dotnet add src/SysOpsCommander.Infrastructure package Serilog.Formatting.Compact
dotnet add src/SysOpsCommander.Infrastructure package Serilog.Enrichers.Thread
dotnet add src/SysOpsCommander.Infrastructure package Serilog.Enrichers.Environment
dotnet add src/SysOpsCommander.Infrastructure package Microsoft.Data.Sqlite
dotnet add src/SysOpsCommander.Infrastructure package Dapper
```

**App project:**
```powershell
dotnet add src/SysOpsCommander.App package Microsoft.Extensions.DependencyInjection
dotnet add src/SysOpsCommander.App package Microsoft.Extensions.Configuration.Json
dotnet add src/SysOpsCommander.App package Serilog.Extensions.Logging
dotnet add src/SysOpsCommander.App package Microsoft.CodeAnalysis.NetAnalyzers
```

**Tests project:**
```powershell
dotnet add tests/SysOpsCommander.Tests package NSubstitute
dotnet add tests/SysOpsCommander.Tests package FluentAssertions
dotnet add tests/SysOpsCommander.Tests package Microsoft.Data.Sqlite
```

> **Note:** After installation, review each `.csproj` to confirm versions are pinned (no `*` or floating versions). Record the exact versions used.

**Verification:**
- [ ] `dotnet restore` succeeds
- [ ] `dotnet build` succeeds with zero warnings
- [ ] Commit: `build(all): install NuGet packages per technology stack`

---

### 0.13 — Create `AppConstants.cs`

**Action:** Create the application constants file.

**File:** `src/SysOpsCommander.Core/Constants/AppConstants.cs`

**Contents:**
```csharp
namespace SysOpsCommander.Core.Constants;

/// <summary>
/// Provides application-wide constant values.
/// </summary>
public static class AppConstants
{
    /// <summary>
    /// The display name of the application.
    /// </summary>
    public const string AppName = "SysOps Commander";

    /// <summary>
    /// The folder name used under %LOCALAPPDATA% for application data storage.
    /// </summary>
    public const string AppDataFolder = "SysOpsCommander";

    /// <summary>
    /// The default number of concurrent remote executions.
    /// </summary>
    public const int DefaultThrottle = 5;

    /// <summary>
    /// The default WinRM operation timeout in seconds.
    /// </summary>
    public const int DefaultWinRmTimeoutSeconds = 60;

    /// <summary>
    /// The default Active Directory query timeout in seconds.
    /// </summary>
    public const int DefaultAdQueryTimeoutSeconds = 30;

    /// <summary>
    /// The maximum number of results returned per paginated query.
    /// </summary>
    public const int MaxResultsPerPage = 500;

    /// <summary>
    /// The standard WinRM HTTP port.
    /// </summary>
    public const int WinRmHttpPort = 5985;

    /// <summary>
    /// The standard WinRM HTTPS port.
    /// </summary>
    public const int WinRmHttpsPort = 5986;

    /// <summary>
    /// The default number of days to retain audit log entries.
    /// </summary>
    public const int AuditLogRetentionDays = 365;

    /// <summary>
    /// The default number of days after which a computer is considered stale.
    /// </summary>
    public const int DefaultStaleComputerDays = 90;

    /// <summary>
    /// The maximum number of parallel TCP connect checks during reachability testing.
    /// </summary>
    public const int ReachabilityCheckParallelism = 20;

    /// <summary>
    /// Switch to disk streaming when cumulative output exceeds this threshold (10 MB).
    /// </summary>
    public const long MaxInMemoryResultBytes = 10 * 1024 * 1024;

    /// <summary>
    /// The default category assigned to scripts without a manifest.
    /// </summary>
    public const string DefaultScriptCategory = "Uncategorized";
}
```

**Conventions applied:**
- File-scoped namespace (`csharp.instructions.md`)
- XML documentation on every member (`csharp-docs/SKILL.md`)
- `<summary>` starts with present-tense 3rd-person verb

**Verification:**
- [ ] Compiles with zero warnings
- [ ] Commit: `feat(core): add AppConstants with application-wide defaults`

---

### 0.14 — Create `AppConfiguration.cs`

**Action:** Create the strongly-typed configuration class that binds to `appsettings.json`.

**File:** `src/SysOpsCommander.Core/Models/AppConfiguration.cs`

**Contents:**
```csharp
namespace SysOpsCommander.Core.Models;

/// <summary>
/// Represents the org-wide application configuration loaded from appsettings.json.
/// Per-user overrides are stored separately in SQLite.
/// </summary>
public sealed class AppConfiguration
{
    /// <summary>
    /// Gets or sets the UNC path to the shared script repository.
    /// </summary>
    public string SharedScriptRepositoryPath { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the UNC path to the network share used for auto-updates.
    /// </summary>
    public string UpdateNetworkSharePath { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the default Active Directory domain. Empty string means auto-detect.
    /// </summary>
    public string DefaultDomain { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the default WinRM transport protocol ("HTTP" or "HTTPS").
    /// </summary>
    public string DefaultWinRmTransport { get; set; } = "HTTP";

    /// <summary>
    /// Gets or sets the default WinRM authentication method ("Kerberos", "NTLM", or "CredSSP").
    /// </summary>
    public string DefaultWinRmAuthMethod { get; set; } = "Kerberos";

    /// <summary>
    /// Gets or sets the default number of concurrent remote executions.
    /// </summary>
    public int DefaultThrottle { get; set; } = 5;

    /// <summary>
    /// Gets or sets the default execution timeout in seconds.
    /// </summary>
    public int DefaultTimeoutSeconds { get; set; } = 60;

    /// <summary>
    /// Gets or sets the number of days of inactivity after which a computer is considered stale.
    /// </summary>
    public int StaleComputerThresholdDays { get; set; } = 90;

    /// <summary>
    /// Gets or sets the number of days to retain audit log entries.
    /// </summary>
    public int AuditLogRetentionDays { get; set; } = 365;
}
```

**Verification:**
- [ ] Compiles
- [ ] Commit: `feat(core): add AppConfiguration for org-wide settings binding`

---

### 0.15 — Create `appsettings.json`

**Action:** Create the application configuration file in the App project.

**File:** `src/SysOpsCommander.App/appsettings.json`

**Contents:**
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

**Post-creation:** Edit the App `.csproj` to ensure the file is copied to the output directory:
```xml
<ItemGroup>
  <None Update="appsettings.json">
    <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
  </None>
</ItemGroup>
```

**Verification:**
- [ ] File exists and is valid JSON
- [ ] Build copies `appsettings.json` to `bin/Debug/net8.0-windows/`
- [ ] Commit: `build(app): add appsettings.json with org-wide defaults`

---

### 0.16 — Create `CredentialDestructuringPolicy.cs`

**Action:** Implement the Serilog destructuring policy that prevents credential leakage.

**File:** `src/SysOpsCommander.Infrastructure/Logging/CredentialDestructuringPolicy.cs`

**Behavior:**
- Implements `Serilog.Core.IDestructuringPolicy`
- Intercepts `SecureString`, `PSCredential`, and `NetworkCredential` objects
- Replaces their entire value with `"[REDACTED]"` in log output
- Returns `true` (handled) for these types, `false` for everything else

**Conventions:**
- XML docs on the class and `TryDestructure` method
- Use `is` pattern matching for type checks (`csharp.instructions.md`)

**Verification:**
- [ ] Compiles
- [ ] Commit: `feat(infrastructure): add CredentialDestructuringPolicy for Serilog`

---

### 0.17 — Create `CorrelationIdEnricher.cs`

> **Improvement:** The plan mentions a `CorrelationIdEnricher` in the Serilog configuration but never specifies a file. Creating it as a dedicated class.

**Action:** Create a Serilog enricher that adds a unique correlation ID to every log event.

**File:** `src/SysOpsCommander.Infrastructure/Logging/CorrelationIdEnricher.cs`

**Behavior:**
- Implements `Serilog.Core.ILogEventEnricher`
- Generates a GUID-based correlation ID per application session (set once at startup)
- Enriches every log event with a `CorrelationId` property
- Useful for correlating logs across a single app session (especially for error dialogs that show a correlation ID)

**Verification:**
- [ ] Compiles
- [ ] Commit: `feat(infrastructure): add CorrelationIdEnricher for session-level log correlation`

---

### 0.18 — Create `SerilogConfigurator.cs`

**Action:** Implement the Serilog configuration module.

**File:** `src/SysOpsCommander.Infrastructure/Logging/SerilogConfigurator.cs`

**Configuration:**
- **File sink:** `%LOCALAPPDATA%\SysOpsCommander\Logs\sysops-{Date}.log`
  - Rolling daily
  - Compact JSON format (`CompactJsonFormatter`)
- **Console sink:** enabled only in `#if DEBUG` builds
- **Default level:** `Information`
- **Enrichers:**
  - `Enrich.WithMachineName()` (from `Serilog.Enrichers.Environment`)
  - `Enrich.WithThreadId()` (from `Serilog.Enrichers.Thread`)
  - Custom `CorrelationIdEnricher` (from step 0.17)
- **Destructuring policy:** `CredentialDestructuringPolicy` (from step 0.16)
- Expose a static `Configure()` method that returns an `ILogger`
- Ensure the `Logs` directory is created if it doesn't exist

**Conventions:**
- Use `ConfigureAwait(false)` — N/A (synchronous setup)
- XML docs on the public `Configure` method

**Verification:**
- [ ] App startup writes a log entry to `%LOCALAPPDATA%\SysOpsCommander\Logs\`
- [ ] Log file is in Compact JSON format
- [ ] Commit: `build(infrastructure): configure Serilog with file sink, enrichers, and credential destructuring`

---

### 0.19 — Create `ServiceCollectionExtensions.cs`

**Action:** Create the DI registration extension methods.

**File:** `src/SysOpsCommander.App/DependencyInjection/ServiceCollectionExtensions.cs`

**Methods:**
- `AddSysOpsServices(this IServiceCollection services)` — registers all service interfaces → implementations. For Phase 0, register stub/placeholder implementations that throw `NotImplementedException` for services not yet built. **Register `IHostTargetingService` as Singleton.**
- `AddSysOpsViewModels(this IServiceCollection services)` — registers all ViewModels as Transient.
- `AddSysOpsInfrastructure(this IServiceCollection services, IConfiguration configuration)` — registers repositories, database initializer, Serilog, `AppConfiguration` binding.

> **Note:** Stub registrations are temporary — they allow the DI container to build in Phase 0. Each subsequent phase replaces stubs with real implementations.

**Verification:**
- [ ] DI container builds without errors
- [ ] `IHostTargetingService` resolves as the same instance (singleton test)
- [ ] Commit: `build(app): create ServiceCollectionExtensions with DI registration`

---

### 0.20 — Create `MainWindowViewModel.cs`

**Action:** Create the main window ViewModel with view navigation.

**File:** `src/SysOpsCommander.ViewModels/MainWindowViewModel.cs`

**Properties:**
- `CurrentView` (`ObservableObject`) — the currently displayed ViewModel, bound to `ContentControl.Content`
- `Title` — application title string

**Commands:**
- `NavigateCommand` — switches `CurrentView` to the selected view's ViewModel

**Conventions:**
- Extends `ObservableObject` from CommunityToolkit.Mvvm
- Uses `[ObservableProperty]` and `[RelayCommand]` source generators
- Does NOT reference any `System.Windows.*` types

**Verification:**
- [ ] Compiles
- [ ] Commit: `feat(viewmodels): add MainWindowViewModel with navigation`

---

### 0.21 — Create `MainWindow.xaml`

**Action:** Build the minimal main window with sidebar skeleton and content area.

**File:** `src/SysOpsCommander.App/MainWindow.xaml` + `MainWindow.xaml.cs`

**Layout:**
- `DockPanel` root
- Left sidebar: `StackPanel` with placeholder `Button`s for each view (Dashboard, AD Explorer, Execution, Script Library, Audit Log, Settings)
- Main area: `ContentControl` bound to `{Binding CurrentView}`
- Sidebar buttons are wired to the `NavigateCommand`
- Window title bound to `{Binding Title}`

**Code-behind:**
- Minimal — only `InitializeComponent()` and possibly `DataContext` assignment
- No business logic in code-behind (`dotnet-wpf.instructions.md`)

**Verification:**
- [ ] App launches and displays a window with sidebar buttons and empty content area
- [ ] Commit: `feat(app): create MainWindow with sidebar navigation skeleton`

---

### 0.22 — Wire `App.xaml.cs` Composition Root

**Action:** Configure the DI container and application startup in `App.xaml.cs`.

**File:** `src/SysOpsCommander.App/App.xaml.cs`

**Startup flow:**
1. Build `IConfiguration` from `appsettings.json` via `ConfigurationBuilder`
2. Create `ServiceCollection`
3. Call `AddSysOpsInfrastructure(configuration)`, `AddSysOpsServices()`, `AddSysOpsViewModels()`
4. Build `IServiceProvider`
5. Configure Serilog via `SerilogConfigurator.Configure()`
6. Log startup: `Log.Information("SysOps Commander starting — version {Version}", assemblyVersion)`
7. Resolve `MainWindow` and `MainWindowViewModel`, set `DataContext`, show window

**Override `OnStartup` and `OnExit`:**
- `OnStartup` — build DI, configure logging, show window
- `OnExit` — flush Serilog (`Log.CloseAndFlush()`)

**Verification:**
- [ ] App launches
- [ ] Serilog writes a startup log entry
- [ ] `appsettings.json` values are accessible via `AppConfiguration`
- [ ] Commit: `build(app): wire DI composition root and Serilog in App.xaml.cs`

---

### 0.23 — Wire Global Exception Handlers

**Action:** Add unhandled exception handlers in `App.xaml.cs`.

**Handlers:**
1. **`DispatcherUnhandledException`** — log to Serilog with `Error` level, show a `MessageBox` displaying: "An unexpected error occurred. Correlation ID: {id}. Please report this to IT.", offer "Continue" or "Exit"
2. **`AppDomain.CurrentDomain.UnhandledException`** — log to Serilog with `Fatal` level
3. **`TaskScheduler.UnobservedTaskException`** — log to Serilog with `Error` level, mark observed to prevent crash

**Correlation ID:** Use the `CorrelationIdEnricher`'s session ID so the user can report the ID and IT can find matching log entries.

**Verification:**
- [ ] Throwing an unhandled exception from a button click shows the error dialog with correlation ID
- [ ] The exception is logged to the Serilog file
- [ ] Commit: `feat(app): add global exception handlers with correlation ID`

---

### 0.24 — Write Smoke Tests

**Action:** Create smoke tests that verify the Phase 0 infrastructure.

**File:** `tests/SysOpsCommander.Tests/Infrastructure/SmokeTests.cs`

**Test cases:**
1. `DiContainer_Builds_WithoutErrors` — build the full DI container, assert no exceptions
2. `CredentialDestructuringPolicy_SecureString_ReturnsRedacted` — destructure a `SecureString`, assert the output contains `"[REDACTED]"`
3. `CredentialDestructuringPolicy_NetworkCredential_ReturnsRedacted` — same for `NetworkCredential`
4. `AppConfiguration_BindsFromJson_DefaultValues` — load test `appsettings.json`, assert `DefaultThrottle == 5`
5. `HostTargetingService_Registration_IsSingleton` — resolve `IHostTargetingService` twice, assert `ReferenceEquals`

**Conventions:**
- Test naming: `MethodName_Scenario_ExpectedBehavior` (`copilot-instructions.md`)
- Use FluentAssertions (`.Should().Be()`)
- Use NSubstitute where mocking needed
- No AAA comments (`csharp.instructions.md`)

**Verification:**
- [ ] All 5 tests pass
- [ ] `dotnet test` reports 5 passed, 0 failed
- [ ] Commit: `test(infrastructure): add smoke tests for DI, Serilog, and AppConfiguration`

---

### 0.25 — Full Build & Launch Verification

**Action:** Final verification of Phase 0 completeness.

**Steps:**
1. `dotnet build SysOpsCommander.sln` — zero warnings
2. `dotnet test` — all tests pass
3. Launch the app — window appears with sidebar skeleton
4. Check `%LOCALAPPDATA%\SysOpsCommander\Logs\` — log file exists with startup entry
5. Verify Roslyn analyzers are active: temporarily introduce a warning (e.g., unused variable) and confirm it fails the build
6. Remove the test warning

**Verification (Phase 0 Acceptance Criteria):**
- [ ] Git repository initialized with `.gitignore` and initial commit
- [ ] Solution builds with zero warnings
- [ ] Application launches and displays a blank window with sidebar skeleton
- [ ] `appsettings.json` loads and binds to `AppConfiguration`
- [ ] Serilog writes a `sysops-{date}.log` file to `%LOCALAPPDATA%\SysOpsCommander\Logs\`
- [ ] Global exception handler catches a test exception and shows a dialog with correlation ID
- [ ] `CredentialDestructuringPolicy` unit test passes
- [ ] `IHostTargetingService` is registered as a singleton (verified by test)
- [ ] All 6 projects compile and reference each other correctly
- [ ] Roslyn analyzers are active and produce no warnings
- [ ] Final commit: `chore: complete Phase 0 — project scaffolding and foundation`

---

## Improvements & Notes

1. **`.editorconfig` added (step 0.2)** — Referenced by `csharp.instructions.md` ("Apply code-formatting style defined in `.editorconfig`") but missing from the original plan. Essential for enforcing consistent formatting.

2. **`global.json` added (step 0.3)** — Pins the .NET SDK version so builds are reproducible across developer machines and CI. Prevents accidental use of .NET 9+ features.

3. **`Directory.Build.props` added (step 0.4)** — Centralizes `LangVersion`, `Nullable`, `TreatWarningsAsErrors`, and `AnalysisLevel` so they don't need to be repeated in every `.csproj`. The plan mentions zero-warning builds but doesn't specify the mechanism.

4. **`CorrelationIdEnricher` file location specified (step 0.17)** — The plan mentions "custom `CorrelationIdEnricher`" in the Serilog config task but never assigns it to a file path. Created as `Infrastructure/Logging/CorrelationIdEnricher.cs`.

5. **Stub service registrations (step 0.19)** — The DI container cannot build if interfaces have no implementations. Phase 0 registers stubs that throw `NotImplementedException` — each subsequent phase replaces them. This isn't explicitly described in the plan but is required for the smoke test and app launch to work.

6. **NuGet package installation order** — The plan says "pin versions explicitly" but doesn't list exact versions. Step 0.12 notes that exact versions should be recorded after installation. Consider creating a `Directory.Packages.props` for centralized version management if the team adopts NuGet Central Package Management.

7. **ViewModels project must NOT reference System.Windows** — The plan states "ViewModels NEVER reference WPF types" but doesn't call out how to enforce this. Consider adding an architectural test in Phase 10 that scans `SysOpsCommander.ViewModels.dll` for `System.Windows` references.
