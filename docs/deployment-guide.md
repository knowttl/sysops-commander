# SysOps Commander — Deployment Guide

## Prerequisites

- **.NET 8 Runtime** installed on the operator's machine
- **Active Directory domain-joined** machine for AD features
- **WinRM enabled** on all target hosts
- Windows 10/11 or Windows Server 2016+

## Installation

1. Copy the published output to the desired location (e.g., `C:\Program Files\SysOpsCommander\`)
2. On first run, the app creates a SQLite database in `%LOCALAPPDATA%\SysOpsCommander\`
3. Configure `appsettings.json` with organization-wide defaults (see [Configuration Reference](#appsettingsjson-reference) below)

## WinRM Configuration on Target Hosts

WinRM must be enabled on all hosts you want to manage remotely.

### Enable WinRM (Standard)

```powershell
# Run on each target host (elevated PowerShell)
Enable-PSRemoting -Force

# Verify the listener is active
Get-WSManInstance -ResourceURI winrm/config/listener -Enumerate
```

### WinRM HTTPS Listener (Optional — Recommended for Production)

```powershell
# Create self-signed cert (or use an enterprise CA certificate)
$cert = New-SelfSignedCertificate -DnsName "host01.corp.contoso.com" -CertStoreLocation "Cert:\LocalMachine\My"

# Create HTTPS listener
New-WSManInstance -ResourceURI winrm/config/Listener -SelectorSet @{
    Address = "*"; Transport = "HTTPS"
} -ValueSet @{ CertificateThumbprint = $cert.Thumbprint }
```

## CredSSP Configuration (If Needed)

CredSSP enables double-hop authentication (e.g., accessing network resources from the remote host). Only enable it if scripts need to access network shares or other remote resources from the target host.

### Client Side (Operator Machine)

```powershell
Enable-WSManCredSSP -Role Client -DelegateComputer *.corp.contoso.com -Force
```

### Server Side (Each Target Host)

```powershell
Enable-WSManCredSSP -Role Server -Force
```

### GPO Approach (Recommended for Scale)

1. Open **Group Policy Management** → Create/edit a GPO linked to the target OU
2. Navigate to: `Computer Configuration → Administrative Templates → System → Credentials Delegation`
3. Enable: **Allow Delegating Fresh Credentials**
4. Add server list: `WSMAN/*.corp.contoso.com`

> **Security Note:** CredSSP delegates full credentials to the target host. Only enable it for trusted hosts within your domain. Consider scoping the `DelegateComputer` parameter to specific hosts rather than using wildcards.

## Firewall Requirements

| Port | Protocol | Direction | Purpose |
|------|----------|-----------|---------|
| 5985 | TCP | Inbound on targets | WinRM HTTP |
| 5986 | TCP | Inbound on targets | WinRM HTTPS |

```powershell
# Open WinRM HTTP port on target hosts
New-NetFirewallRule -DisplayName "WinRM HTTP" -Direction Inbound -Protocol TCP -LocalPort 5985 -Action Allow

# Open WinRM HTTPS port (if using HTTPS transport)
New-NetFirewallRule -DisplayName "WinRM HTTPS" -Direction Inbound -Protocol TCP -LocalPort 5986 -Action Allow
```

## Network Share for Auto-Updates

SysOps Commander supports automatic updates from a network share.

1. Create a share: `\\server\share\SysOpsCommander\`
2. Place the following files on the share:
   - `SysOpsCommander.zip` — the published application archive
   - `version.json` — version metadata (see `docs/samples/version.json` for format)
3. Configure the share path in `appsettings.json`:
   ```json
   "UpdateNetworkSharePath": "\\\\server\\share\\SysOpsCommander"
   ```

The app checks the share on startup. If a newer version is available, the user is prompted to update. The update is downloaded, SHA256-verified, staged locally, and applied via `SysOpsUpdater.exe`.

## appsettings.json Reference

| Key | Type | Default | Description |
|-----|------|---------|-------------|
| `DefaultDomain` | string | *(current domain)* | Domain to connect to on startup |
| `DefaultWinRmAuthMethod` | string | `Kerberos` | Default auth method: `Kerberos`, `NTLM`, or `CredSSP` |
| `DefaultWinRmTransport` | string | `HTTP` | Default transport: `HTTP` or `HTTPS` |
| `StaleComputerThresholdDays` | int | `90` | Days since last logon to flag a computer as stale |
| `SharedScriptRepositoryPath` | string | *(empty)* | UNC path to org-wide script repository |
| `DefaultThrottle` | int | `5` | Maximum concurrent remote executions |
| `DefaultTimeoutSeconds` | int | `60` | WinRM operation timeout in seconds |
| `UpdateNetworkSharePath` | string | *(empty)* | UNC path for auto-update share |
| `AuditLogRetentionDays` | int | `365` | Days to retain audit log entries before purge |

### Example Configuration

```json
{
  "SysOpsCommander": {
    "DefaultDomain": "corp.contoso.com",
    "DefaultWinRmAuthMethod": "Kerberos",
    "DefaultWinRmTransport": "HTTP",
    "StaleComputerThresholdDays": 90,
    "SharedScriptRepositoryPath": "\\\\fileserver\\scripts\\SysOpsCommander",
    "DefaultThrottle": 5,
    "DefaultTimeoutSeconds": 60,
    "UpdateNetworkSharePath": "\\\\fileserver\\updates\\SysOpsCommander",
    "AuditLogRetentionDays": 365
  }
}
```

## Troubleshooting

### WinRM Connection Failures

```powershell
# Test WinRM connectivity from operator machine
Test-WSMan -ComputerName TARGET_HOST

# Test PowerShell remoting
Enter-PSSession -ComputerName TARGET_HOST
```

### CredSSP Errors

| Error | Resolution |
|-------|------------|
| "CredSSP is not configured on target" | Run `Enable-WSManCredSSP -Role Server` on the target |
| "CredSSP Client is not enabled" | Run `Enable-WSManCredSSP -Role Client -DelegateComputer *` on operator machine |
| "CredSSP authentication failed" | Verify username/password and that CredSSP is enabled on both sides |

### Application Logs

Logs are written to: `%LOCALAPPDATA%\SysOpsCommander\logs\`

Log files use Serilog Compact JSON format. Adjust the log level in `appsettings.json` (`LogLevel` key) or via the Settings view in the application.
