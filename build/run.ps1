#Requires -Version 5.1
<#
.SYNOPSIS
    Builds and launches SysOps Commander.
.PARAMETER Configuration
    Build configuration: Debug (default) or Release.
.PARAMETER NoBuild
    Skip building, just launch the existing executable.
.EXAMPLE
    .\build\run.ps1
    .\build\run.ps1 -NoBuild
    .\build\run.ps1 -Configuration Release
#>
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Debug',

    [switch]$NoBuild
)

$ErrorActionPreference = 'Stop'
$SolutionRoot = Split-Path -Parent $PSScriptRoot
$ExePath = Join-Path $SolutionRoot "src\SysOpsCommander.App\bin\$Configuration\net8.0-windows\SysOpsCommander.App.exe"

Push-Location $SolutionRoot
try {
    if (-not $NoBuild) {
        Write-Host "Building ($Configuration)..." -ForegroundColor Cyan
        dotnet build src\SysOpsCommander.App -c $Configuration --nologo
        if ($LASTEXITCODE -ne 0) {
            Write-Host "BUILD FAILED" -ForegroundColor Red
            exit $LASTEXITCODE
        }
    }

    if (-not (Test-Path $ExePath)) {
        Write-Host "Executable not found: $ExePath" -ForegroundColor Red
        Write-Host "Run without -NoBuild to build first." -ForegroundColor Yellow
        exit 1
    }

    Write-Host "Launching SysOps Commander..." -ForegroundColor Green
    Start-Process $ExePath
}
finally {
    Pop-Location
}
