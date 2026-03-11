namespace SysOpsCommander.Core.Models;

/// <summary>
/// Represents a connection to an Active Directory domain.
/// Stores connection parameters only — <c>DirectoryEntry</c> instances are created on demand in the service layer.
/// </summary>
public sealed class DomainConnection
{
    /// <summary>
    /// Gets the fully qualified domain name (e.g., "corp.contoso.com").
    /// </summary>
    public required string DomainName { get; init; }

    /// <summary>
    /// Gets the explicit domain controller FQDN for targeted DC queries, if specified.
    /// </summary>
    public string? DomainControllerFqdn { get; init; }

    /// <summary>
    /// Gets the root distinguished name (e.g., "DC=corp,DC=contoso,DC=com").
    /// </summary>
    public required string RootDistinguishedName { get; init; }

    /// <summary>
    /// Gets a value indicating whether this is the currently logged-in user's domain.
    /// </summary>
    public bool IsCurrentDomain { get; init; }
}
