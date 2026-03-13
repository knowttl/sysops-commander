#Requires -Version 5.1

$os = Get-CimInstance Win32_OperatingSystem
$cs = Get-CimInstance Win32_ComputerSystem
$cpu = Get-CimInstance Win32_Processor | Select-Object -First 1

[PSCustomObject]@{
    Hostname        = $env:COMPUTERNAME
    Domain          = $cs.Domain
    OS              = $os.Caption
    OSVersion       = $os.Version
    Architecture    = $os.OSArchitecture
    CPU             = $cpu.Name
    CPUCores        = $cpu.NumberOfCores
    TotalMemoryGB   = [math]::Round($cs.TotalPhysicalMemory / 1GB, 2)
    FreeMemoryGB    = [math]::Round($os.FreePhysicalMemory / 1MB, 2)
    LastBoot        = $os.LastBootUpTime
    UptimeDays      = [math]::Round(((Get-Date) - $os.LastBootUpTime).TotalDays, 1)
    PSVersion       = $PSVersionTable.PSVersion.ToString()
    LoggedOnUsers   = (Get-CimInstance Win32_ComputerSystem).UserName
}
