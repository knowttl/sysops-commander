# SysOps Commander — Copilot Instructions

> This file provides project-level context for GitHub Copilot when working on the SysOps Commander codebase.

## Project Overview

SysOps Commander is a **cybersecurity desktop application** built with **.NET 8 / WPF / C# 12** for enterprise IT operations. It provides Active Directory exploration, remote PowerShell execution across multiple hosts, script plugin management, and audit logging — all from a single MVVM-based WPF interface.

## Technology Stack

- **Runtime**: .NET 8 (LTS)
- **UI Framework**: WPF with MVVM pattern
- **Language**: C# 12
- **MVVM Toolkit**: CommunityToolkit.Mvvm 8.x (`[ObservableProperty]`, `[RelayCommand]`, `ObservableObject`)
- **Dependency Injection**: Microsoft.Extensions.DependencyInjection
- **Database**: SQLite via Microsoft.Data.Sqlite + Dapper (async, parameterized queries only)
- **Logging**: Serilog (File + Console sinks, Compact JSON formatting)
- **PowerShell**: Microsoft.PowerShell.SDK (7.x) — hosts PS locally, remotes to PS 5.1 on targets
- **AD Integration**: System.DirectoryServices + System.DirectoryServices.Protocols
- **Testing**: xUnit + NSubstitute + FluentAssertions
- **Export**: ClosedXML (Excel), CsvHelper (CSV)
- **Notifications**: Microsoft.Toolkit.Uwp.Notifications (toast)

## Solution Structure

```
SysOpsCommander/
├── src/
│   ├── SysOpsCommander.Core/         # Interfaces, Models, Enums, Validation, Constants
│   ├── SysOpsCommander.Services/     # Service implementations, Strategies
│   ├── SysOpsCommander.Infrastructure/ # Database, Logging, FileSystem
│   ├── SysOpsCommander.ViewModels/   # MVVM ViewModels (no UI dependency)
│   └── SysOpsCommander.App/          # WPF Views, App.xaml, Resources
├── tests/
│   └── SysOpsCommander.Tests/        # xUnit test project
```

## Architecture Rules

1. **MVVM Strict Separation**: ViewModels NEVER reference WPF types. Views bind to ViewModels via `DataContext`. Use CommunityToolkit.Mvvm source generators.
2. **Interface-First Design**: Every service has an interface in `Core/Interfaces/`. Implementations go in `Services/` or `Infrastructure/`.
3. **Dependency Injection**: All services registered in DI container. Use constructor injection. `IHostTargetingService` is a **singleton** (shared across views).
4. **Async Everything**: All I/O operations (AD queries, WinRM, database, file access) must be async with `CancellationToken` support.
5. **No String Interpolation for Parameters**: PowerShell parameters MUST use `AddParameter()` method — NEVER string interpolation/concatenation.
6. **Parameterized SQL Only**: All database queries use parameterized Dapper calls — no string concatenation in SQL.

## Critical Technical Constraints

### PowerShell Execution
- App hosts PS 7.x SDK locally but remotes to PS 5.1 on target hosts via WinRM
- Scripts use `#Requires -Version 5.1` for compatibility
- Parameters injected via `ps.AddParameter(key, value)` — NEVER via string interpolation
- `outputFormat` in manifests is a **rendering hint only**, not a parsing directive

### WinRM Configuration
- Authentication: Kerberos (default), NTLM, CredSSP (user-selectable)
- Transport: HTTP (5985) default, HTTPS (5986) configurable
- CredSSP must be validated before use

### Security
- Credentials stored via DPAPI (`ProtectedData.Protect`)
- LDAP filters sanitized via `LdapFilterSanitizer`
- Audit log captures all execution with user context
- No SYSTEM context execution — user-context + alternate credentials only
- Never log credential values — use `CredentialDestructuringPolicy` for Serilog

### Active Directory
- Default to current user's domain; allow switching to any reachable domain
- Multi-domain support via `DomainConnection` model
- Stale computer threshold configurable (default 90 days)

## Coding Standards

- Follow all instructions in `.github/instructions/*.md`
- Use XML documentation on all public members
- Use `ArgumentNullException.ThrowIfNull()` for null checks
- Use primary constructors for DI when appropriate
- Follow conventional commits: `type(scope): description`
- Keep methods focused — single responsibility
- All public methods should have corresponding unit tests

## Version Control

- **Git initialized** with conventional commit messages
- **Commit after each phase** or large set of changes completes successfully (0 warnings, 0 errors, all tests pass)
- Follow conventional commit format: `type(scope): imperative description`
- Stage all related files before committing — do not leave partial work uncommitted
- Verify build + tests pass **before** every commit

## Testing Conventions

- **Framework**: xUnit with `[Fact]` and `[Theory]`
- **Mocking**: NSubstitute (`.Returns()`, `.Received()`)
- **Assertions**: FluentAssertions (`.Should().Be()`, `.Should().BeEquivalentTo()`)
- **Pattern**: Arrange-Act-Assert (AAA)
- **Naming**: `MethodName_Scenario_ExpectedBehavior`
- **Coverage**: Service layer requires full coverage; ViewModels require command + property change coverage
- **WPF test target**: `net8.0-windows` implicit usings do NOT include `System.IO` — add explicitly when using `Path`, `File`, `Directory`

## Implementation Status

### Phase 0 — Scaffolding ✅
Solution structure, 6 projects, NuGet packages, build configuration (`TreatWarningsAsErrors`, `EnforceCodeStyleInBuild`, `AnalysisLevel=latest-recommended`).

### Phase 1 — Models & Database ✅
8 enums, 14 models, 12 interfaces, `DatabaseInitializer`, `AuditLogRepository`, `SettingsRepository`, `SettingsService`, DI registrations. 31 tests.

### Phase 2 — Validation Framework ✅
- `HostnameValidator` — static, `[GeneratedRegex]` for IPv4/NetBIOS/FQDN, injection detection
- `LdapFilterSanitizer` — RFC 4515 escaping for LDAP filter inputs
- `ManifestSchemaValidator` — validates `ScriptManifest` schema (required fields, semver, categories, parameters)
- `ScriptValidationService` — PS script syntax/AST validation, dangerous pattern detection, manifest-pair validation, CredSSP availability check
- `IScriptValidationService` interface in Core for testability
- `InternalsVisibleTo("SysOpsCommander.Tests")` on Services project for `AnalyzeAst` access
- 86 total tests passing (42 new validation tests)

### Known Analyzer Behaviors
- **IDE0046**: Extremely aggressive — requires ALL cascading `if (...) return X;` before a final return to be folded into nested ternary chains
- **IDE0007/IDE0008**: `var` only when type is apparent from `new`/factory; explicit type for method returns
- **IDE0200**: Prefer method group over lambda when possible
- **IDE0052**: Remove unused private members
- **IDE0005**: Remove unnecessary usings
