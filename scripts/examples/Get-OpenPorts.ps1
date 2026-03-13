#Requires -Version 5.1

param(
    [ValidateSet('All', 'Listening', 'Established')]
    [string]$State = 'Listening'
)

$connections = Get-NetTCPConnection -ErrorAction SilentlyContinue

if ($State -ne 'All') {
    $connections = $connections | Where-Object { $_.State -eq $State }
}

$connections |
    Select-Object LocalAddress, LocalPort, RemoteAddress, RemotePort, State,
        @{Name='Process'; Expression={
            (Get-Process -Id $_.OwningProcess -ErrorAction SilentlyContinue).ProcessName
        }} |
    Sort-Object LocalPort
