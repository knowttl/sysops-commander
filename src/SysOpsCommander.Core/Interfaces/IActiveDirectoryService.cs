using SysOpsCommander.Core.Models;

namespace SysOpsCommander.Core.Interfaces;

/// <summary>
/// Provides Active Directory search, browse, and query operations with multi-domain support.
/// </summary>
public interface IActiveDirectoryService
{
    /// <summary>
    /// Gets the available AD domains that the current user can access.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A list of discoverable domain connections.</returns>
    Task<IReadOnlyList<DomainConnection>> GetAvailableDomainsAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Sets the active domain for subsequent AD operations.
    /// </summary>
    /// <param name="domain">The domain connection to activate.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task SetActiveDomainAsync(DomainConnection domain, CancellationToken cancellationToken);

    /// <summary>
    /// Gets the currently active domain connection.
    /// </summary>
    /// <returns>The active <see cref="DomainConnection"/>.</returns>
    DomainConnection GetActiveDomain();

    /// <summary>
    /// Searches for AD objects matching the specified term.
    /// </summary>
    /// <param name="searchTerm">The search term to match against common attributes.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The search results.</returns>
    Task<AdSearchResult> SearchAsync(string searchTerm, CancellationToken cancellationToken);

    /// <summary>
    /// Searches for AD objects using a raw LDAP filter.
    /// </summary>
    /// <param name="ldapFilter">The sanitized LDAP filter string.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The search results.</returns>
    Task<AdSearchResult> SearchWithFilterAsync(string ldapFilter, CancellationToken cancellationToken);

    /// <summary>
    /// Browses the child objects of a specified container.
    /// </summary>
    /// <param name="parentDn">The distinguished name of the parent container.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A list of child AD objects.</returns>
    Task<IReadOnlyList<AdObject>> BrowseChildrenAsync(string parentDn, CancellationToken cancellationToken);

    /// <summary>
    /// Gets the full details of a specific AD object.
    /// </summary>
    /// <param name="distinguishedName">The distinguished name of the object.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The AD object with all loaded attributes.</returns>
    Task<AdObject> GetObjectDetailAsync(string distinguishedName, CancellationToken cancellationToken);

    /// <summary>
    /// Gets the group memberships for an AD object.
    /// </summary>
    /// <param name="objectDn">The distinguished name of the object.</param>
    /// <param name="recursive">Whether to resolve nested group memberships.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A list of group distinguished names.</returns>
    Task<IReadOnlyList<string>> GetGroupMembershipAsync(string objectDn, bool recursive, CancellationToken cancellationToken);

    /// <summary>
    /// Gets currently locked-out user accounts.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The search results containing locked accounts.</returns>
    Task<AdSearchResult> GetLockedAccountsAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Gets disabled computer accounts.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The search results containing disabled computers.</returns>
    Task<AdSearchResult> GetDisabledComputersAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Gets computer accounts that have been inactive for the specified number of days.
    /// </summary>
    /// <param name="daysInactive">The inactivity threshold in days.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The search results containing stale computers.</returns>
    Task<AdSearchResult> GetStaleComputersAsync(int daysInactive, CancellationToken cancellationToken);

    /// <summary>
    /// Gets the FQDNs of all domain controllers in the active domain.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A list of domain controller hostnames.</returns>
    Task<IReadOnlyList<string>> GetDomainControllersAsync(CancellationToken cancellationToken);
}
