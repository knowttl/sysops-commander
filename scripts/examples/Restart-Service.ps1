#Requires -Version 5.1

param(
    [Parameter(Mandatory)]
    [string]$ServiceName
)

$svc = Get-Service -Name $ServiceName -ErrorAction Stop

$before = $svc.Status
Restart-Service -Name $ServiceName -Force -ErrorAction Stop
$svc.Refresh()

[PSCustomObject]@{
    Hostname     = $env:COMPUTERNAME
    ServiceName  = $svc.Name
    DisplayName  = $svc.DisplayName
    StatusBefore = $before
    StatusAfter  = $svc.Status
    Result       = if ($svc.Status -eq 'Running') { 'Success' } else { 'Failed' }
}
