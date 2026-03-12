using System.DirectoryServices;
using System.Management.Automation;
using System.Runtime.Versioning;
using Serilog;
using SysOpsCommander.Core.Interfaces;

namespace SysOpsCommander.Services;

/// <summary>
/// Manages credential acquisition, LDAP-bind validation, and secure disposal.
/// Never logs credential values — only metadata (username, domain, success/failure).
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class CredentialService : ICredentialService
{
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="CredentialService"/> class.
    /// </summary>
    /// <param name="logger">The Serilog logger instance.</param>
    public CredentialService(ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        _logger = logger;
    }

    /// <inheritdoc/>
    public event EventHandler<EventArgs>? CredentialRequested;

    /// <inheritdoc/>
    public void RequestCredentials()
    {
        _logger.Debug("Credential prompt requested");
        CredentialRequested?.Invoke(this, EventArgs.Empty);
    }

    /// <inheritdoc/>
    public async Task<bool> ValidateCredentialsAsync(
        PSCredential credential,
        string? targetDomain,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(credential);

        string domain = targetDomain ?? Environment.UserDomainName;
        _logger.Information("Validating credentials for {Username} against domain {Domain}",
            credential.UserName, domain);

        return await Task.Run(() =>
        {
            try
            {
                string ldapPath = $"LDAP://{domain}";
                using DirectoryEntry entry = new(
                    ldapPath,
                    credential.UserName,
                    credential.GetNetworkCredential().Password);

                // Force a bind by accessing a property
                _ = entry.NativeObject;

                _logger.Information("Credential validation succeeded for {Username}", credential.UserName);
                return true;
            }
            catch (DirectoryServicesCOMException ex)
            {
                _logger.Warning(ex, "Credential validation failed for {Username} against {Domain}",
                    credential.UserName, domain);
                return false;
            }
            catch (Exception ex)
            {
                _logger.Warning(ex, "Unexpected error validating credentials for {Username}",
                    credential.UserName);
                return false;
            }
        }, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public void DisposeCredentials(PSCredential credential)
    {
        ArgumentNullException.ThrowIfNull(credential);
        credential.Password.Dispose();
        _logger.Debug("Credentials disposed for {Username}", credential.UserName);
    }
}
