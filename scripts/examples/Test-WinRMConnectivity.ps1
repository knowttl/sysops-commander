#Requires -Version 5.1

[PSCustomObject]@{
    Hostname        = $env:COMPUTERNAME
    WinRMRunning    = (Get-Service WinRM).Status -eq 'Running'
    PSVersion       = $PSVersionTable.PSVersion.ToString()
    OSVersion       = [System.Environment]::OSVersion.VersionString
    LastBootUpTime  = (Get-CimInstance Win32_OperatingSystem).LastBootUpTime
} | ConvertTo-Json -Depth 2
