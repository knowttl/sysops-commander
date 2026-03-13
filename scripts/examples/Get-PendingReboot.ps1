#Requires -Version 5.1

$rebootPending = $false
$reasons = @()

# Windows Update
$wuKey = 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\WindowsUpdate\Auto Update\RebootRequired'
if (Test-Path $wuKey) {
    $rebootPending = $true
    $reasons += 'Windows Update'
}

# Component Based Servicing
$cbsKey = 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Component Based Servicing\RebootPending'
if (Test-Path $cbsKey) {
    $rebootPending = $true
    $reasons += 'Component Based Servicing'
}

# Pending file rename operations
$pfro = Get-ItemProperty -Path 'HKLM:\SYSTEM\CurrentControlSet\Control\Session Manager' -Name 'PendingFileRenameOperations' -ErrorAction SilentlyContinue
if ($pfro) {
    $rebootPending = $true
    $reasons += 'Pending File Rename'
}

# SCCM client
try {
    $sccm = Invoke-CimMethod -Namespace 'root\ccm\ClientSDK' -ClassName 'CCM_ClientUtilities' -MethodName 'DetermineIfRebootPending' -ErrorAction SilentlyContinue
    if ($sccm -and $sccm.RebootPending) {
        $rebootPending = $true
        $reasons += 'SCCM Client'
    }
} catch {
    # SCCM not installed — skip
}

[PSCustomObject]@{
    Hostname      = $env:COMPUTERNAME
    RebootPending = $rebootPending
    Reasons       = if ($reasons.Count -gt 0) { $reasons -join '; ' } else { 'None' }
}
