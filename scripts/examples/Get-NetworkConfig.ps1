#Requires -Version 5.1

Get-CimInstance Win32_NetworkAdapterConfiguration -Filter "IPEnabled=True" |
    Select-Object @{Name='Adapter'; Expression={$_.Description}},
        @{Name='IPAddress'; Expression={($_.IPAddress | Where-Object { $_ -notmatch ':' }) -join ', '}},
        @{Name='Subnet'; Expression={$_.IPSubnet -join ', '}},
        @{Name='Gateway'; Expression={$_.DefaultIPGateway -join ', '}},
        @{Name='DNS'; Expression={$_.DNSServerSearchOrder -join ', '}},
        @{Name='DHCP'; Expression={if ($_.DHCPEnabled) { $_.DHCPServer } else { 'Static' }}},
        MACAddress
