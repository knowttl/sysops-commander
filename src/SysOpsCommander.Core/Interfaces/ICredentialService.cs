using System.Management.Automation;

namespace SysOpsCommander.Core.Interfaces;

/// <summary>
/// Manages credential acquisition, validation, and disposal for remote operations.
/// </summary>
public interface ICredentialService
{
    /// <summary>
    /// Occurs when credentials are needed from the user.
    /// </summary>
    event EventHandler<EventArgs>? CredentialRequested;

    /// <summary>
    /// Raises the <see cref="CredentialRequested"/> event to prompt the user for credentials.
    /// </summary>
    void RequestCredentials();

    /// <summary>
    /// Validates the provided credentials against the specified domain.
    /// </summary>
    /// <param name="credential">The credentials to validate.</param>
    /// <param name="targetDomain">The domain to validate against, or <see langword="null"/> for the current domain.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns><see langword="true"/> if the credentials are valid; otherwise, <see langword="false"/>.</returns>
    Task<bool> ValidateCredentialsAsync(PSCredential credential, string? targetDomain, CancellationToken cancellationToken);

    /// <summary>
    /// Disposes of the credential's secure string to prevent memory leaks.
    /// </summary>
    /// <param name="credential">The credential to dispose.</param>
    void DisposeCredentials(PSCredential credential);
}
