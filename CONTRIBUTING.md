# Contributing to SysOps Commander

Thank you for your interest in contributing to SysOps Commander. This guide covers how to build, test, and extend the application.

## Prerequisites

- **.NET 8 SDK** (LTS)
- **Visual Studio 2022** (17.8+) or **VS Code** with C# Dev Kit extension
- Windows 10/11 (WPF application — Windows only)
- Domain-joined machine (for Active Directory features)

## Building

```powershell
# Clone and build
git clone <repo-url>
cd SysOpsCommander
dotnet build SysOpsCommander.sln

# Run the application
dotnet run --project src/SysOpsCommander.App
```

## Running Tests

```powershell
# Run all tests
dotnet test SysOpsCommander.sln

# Run with verbose output
dotnet test SysOpsCommander.sln --logger "console;verbosity=detailed"

# Run a specific test file
dotnet test --filter "FullyQualifiedName~HostnameValidatorTests"
```

### Test Conventions

- **Framework:** xUnit with `[Fact]` and `[Theory]`
- **Mocking:** NSubstitute (`.Returns()`, `.Received()`)
- **Assertions:** FluentAssertions (`.Should().Be()`)
- **Pattern:** Arrange–Act–Assert (AAA)
- **Naming:** `MethodName_Scenario_ExpectedBehavior`

## Architecture Overview

SysOps Commander follows **MVVM with a service layer**:

```
SysOpsCommander.Core          → Interfaces, Models, Enums, Validation, Constants
SysOpsCommander.Services      → Service implementations, Strategies
SysOpsCommander.Infrastructure → Database (SQLite/Dapper), Logging (Serilog), File I/O
SysOpsCommander.ViewModels    → MVVM ViewModels (no UI dependency)
SysOpsCommander.App           → WPF Views, App.xaml, Resources, Dialogs
```

Key rules:
- ViewModels **never** reference WPF types.
- All services have interfaces in `Core/Interfaces/` and implementations in `Services/` or `Infrastructure/`.
- All services are registered in DI (`Microsoft.Extensions.DependencyInjection`).
- `IHostTargetingService` is a **singleton** shared across views.

For the full design and phase-by-phase implementation history, see `docs/implementation-phases/`.

## Coding Conventions

See `.github/instructions/` for detailed guidance:

- **C# 12** with file-scoped namespaces, one type per file
- **XML documentation** on all public members
- **Async everything** — all I/O operations use `async`/`await` with `CancellationToken`
- **Parameterized SQL only** — no string concatenation in queries (Dapper)
- **`AddParameter()` only** — no string interpolation for PowerShell parameters
- **LDAP sanitization** — user inputs in AD queries go through `LdapFilterSanitizer`
- Self-explanatory code — comments explain **why**, not **what**

## Commit Message Format

Follow [Conventional Commits](https://www.conventionalcommits.org/):

```
type(scope): imperative description

Examples:
  feat(execution): add CredSSP authentication method
  fix(ad): handle stale domain controller gracefully
  test(validation): add hostname injection pattern tests
  docs: update deployment guide with HTTPS listener setup
```

## Creating a New Script Plugin

SysOps Commander uses a paired `.ps1` + `.json` manifest system for script plugins.

### Step 1: Write the PowerShell script

Create a `.ps1` file with a `param()` block. Target PowerShell 5.1 for compatibility with remote hosts.

```powershell
#Requires -Version 5.1

param(
    [int]$Days = 30
)

Get-EventLog -LogName Security -Newest $Days | Select-Object TimeGenerated, EntryType, Source, Message
```

### Step 2: Create the manifest

Create a `.json` file with the same base name. See `scripts/manifest-schema.json` for the full schema.

```json
{
    "name": "Get-SecurityEvents",
    "displayName": "Get Security Events",
    "description": "Retrieves security event log entries from target hosts.",
    "version": "1.0.0",
    "author": "Your Name",
    "category": "Security",
    "parameters": [
        {
            "name": "Days",
            "displayName": "Days to Search",
            "type": "int",
            "defaultValue": 30,
            "description": "Number of days of events to retrieve."
        }
    ],
    "outputFormat": "table",
    "requiresCredSSP": false,
    "minimumPsVersion": "5.1"
}
```

### Step 3: Deploy the scripts

Place both files in one of these locations (priority order):
1. **Per-user path** — configured in Settings → "User Script Repository Path"
2. **Org-wide path** — configured in `appsettings.json` → `SharedScriptRepositoryPath`
3. **Built-in** — `scripts/examples/` in the application directory

### Step 4: Refresh in the app

Navigate to **Script Library** and click the **Refresh** button, or press **F5**.

### Step 5: Verify

The script should appear in the Script Library list with its display name, category, description, and parameter definitions from the manifest.

## Publishing

```powershell
# Run the publish script
.\build\publish.ps1

# Or publish manually
dotnet publish src/SysOpsCommander.App/SysOpsCommander.App.csproj -c Release -r win-x64 --self-contained -o ./publish
```

See `docs/deployment-guide.md` for full deployment instructions including WinRM and CredSSP configuration.
