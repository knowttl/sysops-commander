#Requires -Version 5.1

Get-LocalUser |
    Select-Object Name, Enabled, PasswordRequired,
        @{Name='PasswordLastSet'; Expression={$_.PasswordLastSet}},
        @{Name='LastLogon'; Expression={$_.LastLogon}},
        @{Name='PasswordExpires'; Expression={$_.PasswordExpires}},
        @{Name='AccountExpires'; Expression={$_.AccountExpires}},
        Description |
    Sort-Object Name
