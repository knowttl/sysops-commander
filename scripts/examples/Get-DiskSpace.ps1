#Requires -Version 5.1

param(
    [double]$ThresholdPercent = 20
)

Get-CimInstance -ClassName Win32_LogicalDisk -Filter "DriveType=3" |
    Select-Object DeviceID,
        @{Name='SizeGB'; Expression={[math]::Round($_.Size / 1GB, 2)}},
        @{Name='FreeGB'; Expression={[math]::Round($_.FreeSpace / 1GB, 2)}},
        @{Name='FreePercent'; Expression={[math]::Round(($_.FreeSpace / $_.Size) * 100, 1)}},
        @{Name='Status'; Expression={
            if (($_.FreeSpace / $_.Size) * 100 -lt $ThresholdPercent) { 'WARNING' } else { 'OK' }
        }} |
    Sort-Object DeviceID
