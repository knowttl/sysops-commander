#Requires -Version 5.1

param(
    [int]$Hours = 24,
    [int]$MaxEvents = 200
)

$startTime = (Get-Date).AddHours(-$Hours)

Get-WinEvent -FilterHashtable @{
    LogName   = 'Security'
    Id        = 4625
    StartTime = $startTime
} -MaxEvents $MaxEvents -ErrorAction SilentlyContinue |
    ForEach-Object {
        [PSCustomObject]@{
            TimeCreated    = $_.TimeCreated
            TargetAccount  = $_.Properties[5].Value
            TargetDomain   = $_.Properties[6].Value
            SourceAddress  = $_.Properties[19].Value
            FailureReason  = $_.Properties[8].Value
            LogonType      = $_.Properties[10].Value
        }
    } |
    Sort-Object TimeCreated -Descending
