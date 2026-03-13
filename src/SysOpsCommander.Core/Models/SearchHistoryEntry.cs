namespace SysOpsCommander.Core.Models;

/// <summary>
/// Represents a single search history entry for the AD Explorer.
/// </summary>
/// <param name="QueryText">The search term that was executed.</param>
/// <param name="SelectedAttribute">The attribute filter used (e.g., "All attributes", "sAMAccountName").</param>
/// <param name="ActiveFilters">The object class filters that were active (e.g., ["user", "computer"]).</param>
/// <param name="ScopeDn">The scope DN used for the search, or <see langword="null"/> for domain root.</param>
/// <param name="IsLdapFilterMode">Whether the search used raw LDAP filter mode.</param>
/// <param name="ResultCount">The number of results returned by the search.</param>
/// <param name="ExecutedAtUtc">The UTC timestamp when the search was executed.</param>
public sealed record SearchHistoryEntry(
    string QueryText,
    string SelectedAttribute,
    IReadOnlyList<string> ActiveFilters,
    string? ScopeDn,
    bool IsLdapFilterMode,
    int ResultCount,
    DateTime ExecutedAtUtc);
