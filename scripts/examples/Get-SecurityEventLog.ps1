#Requires -Version 5.1

param(
    [int]$MaxEvents = 100,
    [string]$EventId = ''
)

$filterHash = @{
    LogName   = 'Security'
    MaxEvents = $MaxEvents
}

if ($EventId -ne '') {
    $filterHash['Id'] = [int]$EventId
}

Get-WinEvent -FilterHashtable $filterHash |
    Select-Object TimeCreated, Id, LevelDisplayName, Message |
    Sort-Object TimeCreated -Descending
