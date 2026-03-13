#Requires -Version 5.1
<#
.SYNOPSIS
    Builds SysOps Commander in Debug or Release configuration.
.PARAMETER Configuration
    Build configuration: Debug (default) or Release.
.PARAMETER Clean
    Clean before building.
.EXAMPLE
    .\build\build.ps1
    .\build\build.ps1 -Configuration Release
    .\build\build.ps1 -Clean
#>
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Debug',

    [switch]$Clean
)

$ErrorActionPreference = 'Stop'
$SolutionRoot = Split-Path -Parent $PSScriptRoot
$Solution = Join-Path $SolutionRoot 'SysOpsCommander.sln'

Push-Location $SolutionRoot
try {
    if ($Clean) {
        Write-Host "Cleaning solution..." -ForegroundColor Cyan
        dotnet clean $Solution -c $Configuration --nologo -v quiet
    }

    Write-Host "Building solution ($Configuration)..." -ForegroundColor Cyan
    dotnet build $Solution -c $Configuration --nologo

    if ($LASTEXITCODE -ne 0) {
        Write-Host "BUILD FAILED" -ForegroundColor Red
        exit $LASTEXITCODE
    }

    Write-Host "BUILD SUCCEEDED" -ForegroundColor Green
}
finally {
    Pop-Location
}
