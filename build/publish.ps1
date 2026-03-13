#Requires -Version 5.1
<#
.SYNOPSIS
    Publishes SysOps Commander as a self-contained single-file executable.
.PARAMETER Runtime
    Target runtime identifier: win-x64 (default) or win-arm64.
.PARAMETER OutputDir
    Output directory (default: publish/ in repo root).
.PARAMETER SkipTests
    Skip running tests before publishing.
.EXAMPLE
    .\build\publish.ps1
    .\build\publish.ps1 -Runtime win-arm64
    .\build\publish.ps1 -SkipTests
#>
param(
    [ValidateSet('win-x64', 'win-arm64')]
    [string]$Runtime = 'win-x64',

    [string]$OutputDir,

    [switch]$SkipTests
)

$ErrorActionPreference = 'Stop'
$SolutionRoot = Split-Path -Parent $PSScriptRoot
$AppProject = Join-Path $SolutionRoot 'src\SysOpsCommander.App\SysOpsCommander.App.csproj'

if (-not $OutputDir) {
    $OutputDir = Join-Path $SolutionRoot 'publish'
}

Push-Location $SolutionRoot
try {
    if (-not $SkipTests) {
        Write-Host "Running tests before publish..." -ForegroundColor Cyan
        dotnet test --nologo -v quiet
        if ($LASTEXITCODE -ne 0) {
            Write-Host "Tests failed — aborting publish." -ForegroundColor Red
            exit $LASTEXITCODE
        }
        Write-Host "Tests passed." -ForegroundColor Green
    }

    Write-Host "Publishing self-contained app ($Runtime)..." -ForegroundColor Cyan
    dotnet publish $AppProject `
        -c Release `
        -r $Runtime `
        --self-contained `
        -p:PublishSingleFile=true `
        -p:IncludeNativeLibrariesForSelfExtract=true `
        -o $OutputDir `
        --nologo

    if ($LASTEXITCODE -ne 0) {
        Write-Host "PUBLISH FAILED" -ForegroundColor Red
        exit $LASTEXITCODE
    }

    $exe = Join-Path $OutputDir 'SysOpsCommander.App.exe'
    $size = [math]::Round((Get-Item $exe).Length / 1MB, 1)
    Write-Host "PUBLISH SUCCEEDED — $exe ($size MB)" -ForegroundColor Green
}
finally {
    Pop-Location
}
