# SysOps Commander

A WPF desktop application for cybersecurity operations teams to browse Active Directory, execute PowerShell scripts across multiple remote hosts via WinRM, manage a script plugin library, and maintain a full audit trail — from a single interface.

## Architecture Overview

SysOps Commander follows MVVM with a service layer, built on .NET 8 and C# 12.

```
┌─────────────────────────────────────────────────────────────┐
│                    SysOpsCommander.App                       │
│              WPF Views, Dialogs, Resources                  │
├─────────────────────────────────────────────────────────────┤
│                 SysOpsCommander.ViewModels                  │
│          CommunityToolkit.Mvvm ViewModels (no UI refs)      │
├──────────────────────┬──────────────────────────────────────┤
│ SysOpsCommander      │        SysOpsCommander               │
│     .Services        │         .Infrastructure              │
│ AD, Execution,       │     SQLite/Dapper, Serilog,          │
│ Script Loading,      │     File I/O                         │
│ Credential Mgmt      │                                      │
├──────────────────────┴──────────────────────────────────────┤
│                    SysOpsCommander.Core                      │
│           Interfaces, Models, Enums, Constants               │
└─────────────────────────────────────────────────────────────┘
```

| Component | Technology |
|-----------|-----------|
| Runtime | .NET 8 (LTS), C# 12 |
| UI | WPF with CommunityToolkit.Mvvm |
| DI | Microsoft.Extensions.DependencyInjection |
| Database | SQLite via Microsoft.Data.Sqlite + Dapper |
| Logging | Serilog (File + Console, Compact JSON) |
| Remote Execution | Microsoft.PowerShell.SDK (7.x) over WinRM to PS 5.1 targets |
| AD Integration | System.DirectoryServices + System.DirectoryServices.Protocols |
| Export | ClosedXML (Excel), CsvHelper (CSV) |
| Notifications | Microsoft.Toolkit.Uwp.Notifications (toast) |
| Auto-Update | Network share with SHA256-verified packages |

## Prerequisites

- .NET 8 SDK (8.0.400+) for building, or .NET 8 Runtime for running published binaries
- Windows 10/11 or Windows Server 2016+ (WPF — Windows only)
- Domain-joined machine for Active Directory features
- WinRM enabled on target hosts (port 5985 for HTTP, 5986 for HTTPS)
- Visual Studio 2022 (17.8+) or VS Code with C# Dev Kit (for development)

## Quick Start

1. Clone the repository and build:

```powershell
git clone <repo-url>
cd SysOpsCommander
dotnet build SysOpsCommander.sln
```

2. Run the application:

```powershell
.\build\run.ps1
```

Or directly:

```powershell
dotnet run --project src/SysOpsCommander.App
```

3. On first launch, the app creates its SQLite database and log directory under `%LOCALAPPDATA%\SysOpsCommander\`.

4. Open Settings to configure the default domain, WinRM auth method, and script repository path — or edit `src/SysOpsCommander.App/appsettings.json` before launching.

## Configuration Reference

All settings live under the `SysOpsCommander` key in `appsettings.json`.

| Key | Type | Default | Description |
|-----|------|---------|-------------|
| `DefaultDomain` | string | *(current domain)* | AD domain to connect to on startup |
| `DefaultWinRmAuthMethod` | string | `Kerberos` | WinRM auth: `Kerberos`, `NTLM`, or `CredSSP` |
| `DefaultWinRmTransport` | string | `HTTP` | WinRM transport: `HTTP` or `HTTPS` |
| `DefaultThrottle` | int | `5` | Max concurrent remote executions |
| `DefaultTimeoutSeconds` | int | `60` | WinRM operation timeout in seconds |
| `StaleComputerThresholdDays` | int | `90` | Days since last logon to flag a computer as stale |
| `SharedScriptRepositoryPath` | string | *(empty)* | UNC path to an org-wide shared script repository |
| `UpdateNetworkSharePath` | string | *(empty)* | UNC path for auto-update packages |
| `AuditLogRetentionDays` | int | `365` | Days to retain audit log entries before purge |

Sensitive or environment-specific values (domain, UNC paths) should be set per deployment. Do not commit credentials or internal paths to source control.

Minimal working example:

```json
{
  "SysOpsCommander": {
    "DefaultDomain": "corp.contoso.com",
    "DefaultWinRmAuthMethod": "Kerberos",
    "DefaultWinRmTransport": "HTTP",
    "SharedScriptRepositoryPath": "\\\\fileserver\\scripts\\SysOpsCommander",
    "DefaultThrottle": 5,
    "DefaultTimeoutSeconds": 60
  }
}
```

## Usage

SysOps Commander has five main views accessible from the sidebar.

**Dashboard** — Quick Connect to a host, view recent executions, and see domain connection status.

**AD Explorer** — Browse the Active Directory tree, search for users/computers/groups, and use pre-built security filters (locked accounts, disabled computers, stale machines, domain controllers). Select objects to send to the execution target list.

**Execution** — Select target hosts, pick a script from the library (or run ad-hoc PowerShell), configure parameters, and execute across hosts in parallel. Results stream in per-host with status indicators. Export results to CSV or Excel.

**Script Library** — Browse discovered scripts from three tiers: per-user directory, org-wide shared repository, and built-in examples. Each script pairs a `.ps1` file with a `.json` manifest that defines parameters, categories, and output format hints.

**Audit Log** — Full history of all executions with user, timestamp, hosts, script, and outcome. Supports filtering, export, and configurable retention.

### Building and Testing

```powershell
# Build
.\build\build.ps1
.\build\build.ps1 -Configuration Release -Clean

# Run tests
.\build\test.ps1
.\build\test.ps1 -Filter "FullyQualifiedName~HostnameValidator"

# Publish self-contained single-file executable
.\build\publish.ps1
.\build\publish.ps1 -Runtime win-arm64 -CreateUpdatePackage
```

### Creating a Script Plugin

1. Write a `.ps1` script targeting PowerShell 5.1 with a `param()` block.
2. Create a matching `.json` manifest (same base name) defining parameters, category, and output format.
3. Place both files in the shared script repository or the built-in `scripts/examples/` directory.

See `scripts/examples/` for working examples (e.g., `Get-InstalledSoftware.ps1` + `Get-InstalledSoftware.json`).

## Troubleshooting

**WinRM connection refused on target host** → WinRM is not enabled or the firewall is blocking port 5985/5986. Run `Enable-PSRemoting -Force` on the target and verify with `Test-WSMan -ComputerName TARGET`.

**CredSSP authentication failed** → CredSSP is not configured on the client, the server, or both. Run `Enable-WSManCredSSP -Role Client -DelegateComputer *.yourdomain.com` on the operator machine and `Enable-WSManCredSSP -Role Server` on each target.

**Scripts fail with "The term is not recognized"** → The script uses PowerShell 7+ syntax but the remote host runs PowerShell 5.1. Check `#Requires -Version` in the script and remove incompatible constructs.

**AD Explorer shows no results** → The operator machine is not domain-joined, or the configured domain is unreachable. Check network connectivity and the `DefaultDomain` setting.

**Application fails to start with database errors** → The SQLite database under `%LOCALAPPDATA%\SysOpsCommander\` may be corrupted. Delete it and restart; the app recreates it on launch.

**Auto-update not detecting new versions** → Verify `UpdateNetworkSharePath` in `appsettings.json` points to an accessible share containing `version.json` and `SysOpsCommander.zip` with a matching SHA256 hash.

## Directory Structure

```
SysOpsCommander/
├── build/                          Build, test, run, and publish scripts
│   ├── build.ps1
│   ├── test.ps1
│   ├── run.ps1
│   └── publish.ps1
├── docs/                           Deployment guide and implementation history
├── scripts/
│   ├── manifest-schema.json        JSON schema for script manifests
│   └── examples/                   Built-in script plugins (.ps1 + .json pairs)
├── src/
│   ├── SysOpsCommander.Core/       Interfaces, Models, Enums, Constants
│   ├── SysOpsCommander.Services/   AD, Execution, Script, Credential services
│   ├── SysOpsCommander.Infrastructure/  SQLite/Dapper repos, Serilog, File I/O
│   ├── SysOpsCommander.ViewModels/ MVVM ViewModels (no WPF dependency)
│   ├── SysOpsCommander.App/        WPF Views, Dialogs, Resources, DI setup
│   └── SysOpsUpdater/              Auto-update helper executable
├── tests/
│   └── SysOpsCommander.Tests/      xUnit + NSubstitute + FluentAssertions
├── Directory.Build.props           Shared build settings (TreatWarningsAsErrors, etc.)
├── global.json                     .NET SDK version pin (8.0.400+)
└── SysOpsCommander.sln
```

## Maintenance and Ownership

Report issues and request changes via the repository's issue tracker.

Recurring maintenance tasks:
- Review and rotate any credentials stored in the operator's DPAPI-protected credential store.
- Update the `SharedScriptRepositoryPath` when org-wide scripts are added or retired.
- Purge audit log entries beyond the configured retention period (handled automatically, but verify periodically).
- Update the auto-update share (`version.json` + `SysOpsCommander.zip`) when publishing new releases.
- Run `dotnet outdated` periodically to check for dependency updates and security patches.
