namespace SysOpsCommander.Core.Interfaces;

/// <summary>
/// Abstracts Active Directory operations provided by <c>System.DirectoryServices</c>.
/// Enables unit testing of <see cref="IActiveDirectoryService"/> without a live AD environment.
/// </summary>
public interface IDirectoryAccessor : IDisposable
{
    /// <summary>
    /// Detects the current user's domain.
    /// </summary>
    /// <returns>A tuple of the domain name and root distinguished name.</returns>
    (string DomainName, string RootDn) GetCurrentDomain();

    /// <summary>
    /// Enumerates all domains in the current forest.
    /// </summary>
    /// <returns>A list of domain name and root DN tuples.</returns>
    IReadOnlyList<(string DomainName, string RootDn)> GetForestDomains();

    /// <summary>
    /// Attempts to bind to the specified LDAP path to validate connectivity.
    /// </summary>
    /// <param name="ldapPath">The LDAP path to bind (e.g., "LDAP://DC=corp,DC=contoso,DC=com").</param>
    /// <returns><see langword="true"/> if the bind succeeds; otherwise, <see langword="false"/>.</returns>
    bool TryBind(string ldapPath);

    /// <summary>
    /// Executes an LDAP search and returns results as property dictionaries.
    /// </summary>
    /// <param name="baseDn">The base distinguished name for the search.</param>
    /// <param name="filter">The LDAP filter string.</param>
    /// <param name="propertiesToLoad">The attribute names to retrieve.</param>
    /// <param name="subtree">If <see langword="true"/>, searches the entire subtree; otherwise, only one level.</param>
    /// <param name="sizeLimit">The maximum number of results.</param>
    /// <param name="pageSize">The page size for paged result retrieval.</param>
    /// <param name="timeout">The server-side query timeout.</param>
    /// <returns>A list of dictionaries mapping attribute names to values.</returns>
    IReadOnlyList<Dictionary<string, object?>> Search(
        string baseDn,
        string filter,
        string[] propertiesToLoad,
        bool subtree,
        int sizeLimit,
        int pageSize,
        TimeSpan timeout);

    /// <summary>
    /// Loads all attributes for the specified AD object.
    /// </summary>
    /// <param name="distinguishedName">The distinguished name of the object.</param>
    /// <returns>A dictionary of all attribute name-value pairs.</returns>
    Dictionary<string, object?> GetAllAttributes(string distinguishedName);

    /// <summary>
    /// Retrieves the <c>tokenGroups</c> attribute (recursive group SIDs) for the specified object.
    /// </summary>
    /// <param name="distinguishedName">The distinguished name of the object.</param>
    /// <returns>A list of SID byte arrays.</returns>
    IReadOnlyList<byte[]> GetTokenGroups(string distinguishedName);
}
