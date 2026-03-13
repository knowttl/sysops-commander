#Requires -Version 5.1

param(
    [ValidateSet('All', 'Ready', 'Running', 'Disabled')]
    [string]$State = 'All',
    [switch]$ExcludeMicrosoft
)

$tasks = Get-ScheduledTask -ErrorAction SilentlyContinue

if ($State -ne 'All') {
    $tasks = $tasks | Where-Object { $_.State -eq $State }
}

if ($ExcludeMicrosoft) {
    $tasks = $tasks | Where-Object { $_.TaskPath -notlike '\Microsoft\*' }
}

$tasks |
    Select-Object TaskName, TaskPath, State,
        @{Name='LastRunTime'; Expression={
            (Get-ScheduledTaskInfo -TaskName $_.TaskName -TaskPath $_.TaskPath -ErrorAction SilentlyContinue).LastRunTime
        }},
        @{Name='NextRunTime'; Expression={
            (Get-ScheduledTaskInfo -TaskName $_.TaskName -TaskPath $_.TaskPath -ErrorAction SilentlyContinue).NextRunTime
        }},
        @{Name='RunAs'; Expression={$_.Principal.UserId}} |
    Sort-Object TaskPath, TaskName
