namespace SysOpsCommander.Core.Models;

/// <summary>
/// Represents the result of an Active Directory search operation.
/// </summary>
public sealed class AdSearchResult
{
    /// <summary>
    /// Gets the AD objects returned by the search.
    /// </summary>
    public required IReadOnlyList<AdObject> Results { get; init; }

    /// <summary>
    /// Gets the search term or LDAP filter used.
    /// </summary>
    public required string Query { get; init; }

    /// <summary>
    /// Gets the time taken to execute the search.
    /// </summary>
    public required TimeSpan ExecutionTime { get; init; }

    /// <summary>
    /// Gets the total number of matching objects, which may exceed <see cref="Results"/> count if paginated.
    /// </summary>
    public required int TotalResultCount { get; init; }

    /// <summary>
    /// Gets a value indicating whether additional pages of results are available.
    /// </summary>
    public required bool HasMoreResults { get; init; }
}
