#Requires -Version 5.1

param(
    [int]$Days = 90
)

$cutoff = (Get-Date).AddDays(-$Days)

Get-HotFix |
    Where-Object { $_.InstalledOn -ge $cutoff } |
    Select-Object HotFixID, Description,
        @{Name='InstalledOn'; Expression={$_.InstalledOn.ToString('yyyy-MM-dd')}},
        InstalledBy |
    Sort-Object InstalledOn -Descending
