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

## Testing Conventions

- **Framework**: xUnit with `[Fact]` and `[Theory]`
- **Mocking**: NSubstitute (`.Returns()`, `.Received()`)
- **Assertions**: FluentAssertions (`.Should().Be()`, `.Should().BeEquivalentTo()`)
- **Pattern**: Arrange-Act-Assert (AAA)
- **Naming**: `MethodName_Scenario_ExpectedBehavior`
- **Coverage**: Service layer requires full coverage; ViewModels require command + property change coverage
