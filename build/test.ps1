#Requires -Version 5.1
<#
.SYNOPSIS
    Runs all unit tests for SysOps Commander.
.PARAMETER Filter
    Optional test filter expression (e.g. "FullyQualifiedName~HostTargeting").
.PARAMETER Verbose
    Show detailed test output.
.EXAMPLE
    .\build\test.ps1
    .\build\test.ps1 -Filter "FullyQualifiedName~CredentialService"
    .\build\test.ps1 -Verbose
#>
param(
    [string]$Filter,
    [switch]$Verbose
)

$ErrorActionPreference = 'Stop'
$SolutionRoot = Split-Path -Parent $PSScriptRoot
$Solution = Join-Path $SolutionRoot 'SysOpsCommander.sln'

$verbosity = if ($Verbose) { 'normal' } else { 'quiet' }

Push-Location $SolutionRoot
try {
    Write-Host "Running tests..." -ForegroundColor Cyan

    $dotnetArgs = @('test', $Solution, '--nologo', '-v', $verbosity)
    if ($Filter) {
        $dotnetArgs += '--filter'
        $dotnetArgs += $Filter
    }

    & dotnet @dotnetArgs

    if ($LASTEXITCODE -ne 0) {
        Write-Host "TESTS FAILED" -ForegroundColor Red
        exit $LASTEXITCODE
    }

    Write-Host "ALL TESTS PASSED" -ForegroundColor Green
}
finally {
    Pop-Location
}
