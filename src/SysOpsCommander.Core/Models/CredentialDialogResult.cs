using System.Security;
using SysOpsCommander.Core.Enums;

namespace SysOpsCommander.Core.Models;

/// <summary>
/// Represents the result of a credential dialog interaction.
/// </summary>
public sealed class CredentialDialogResult
{
    /// <summary>
    /// Gets the domain for authentication.
    /// </summary>
    public required string Domain { get; init; }

    /// <summary>
    /// Gets the username.
    /// </summary>
    public required string Username { get; init; }

    /// <summary>
    /// Gets the password as a <see cref="SecureString"/>.
    /// </summary>
    public required SecureString Password { get; init; }

    /// <summary>
    /// Gets the selected authentication method.
    /// </summary>
    public required WinRmAuthMethod AuthMethod { get; init; }
}
