#Requires -Version 5.1

# Quick system info scan — no manifest, runs as a simple drop-in script
Write-Output "=== Quick System Scan ==="
Write-Output "Hostname: $env:COMPUTERNAME"
Write-Output "Domain: $env:USERDOMAIN"
Write-Output "OS: $((Get-CimInstance Win32_OperatingSystem).Caption)"
Write-Output "Uptime: $((Get-Date) - (Get-CimInstance Win32_OperatingSystem).LastBootUpTime)"
Write-Output "CPU: $((Get-CimInstance Win32_Processor).Name)"
Write-Output "RAM (GB): $([math]::Round((Get-CimInstance Win32_ComputerSystem).TotalPhysicalMemory / 1GB, 2))"
Write-Output "Disk Free (C:): $([math]::Round((Get-PSDrive C).Free / 1GB, 2)) GB"
