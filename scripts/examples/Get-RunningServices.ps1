#Requires -Version 5.1

param(
    [string]$NameFilter = '*',
    [ValidateSet('All', 'Running', 'Stopped')]
    [string]$Status = 'All'
)

$services = Get-Service -Name $NameFilter -ErrorAction SilentlyContinue

if ($Status -ne 'All') {
    $services = $services | Where-Object { $_.Status -eq $Status }
}

$services |
    Select-Object Name, DisplayName, Status, StartType |
    Sort-Object Status, Name
