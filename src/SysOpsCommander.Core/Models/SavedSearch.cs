namespace SysOpsCommander.Core.Models;

/// <summary>
/// Represents a user-saved search configuration for the AD Explorer.
/// </summary>
/// <param name="Id">The unique identifier for this saved search.</param>
/// <param name="Name">The user-assigned display name.</param>
/// <param name="QueryText">The search term or raw LDAP filter.</param>
/// <param name="SelectedAttribute">The attribute filter used.</param>
/// <param name="ActiveFilters">The object class filters that were active.</param>
/// <param name="ScopeDn">The scope DN, or <see langword="null"/> for domain root.</param>
/// <param name="IsLdapFilterMode">Whether this search uses raw LDAP filter mode.</param>
/// <param name="CreatedAtUtc">The UTC timestamp when this search was saved.</param>
public sealed record SavedSearch(
    string Id,
    string Name,
    string QueryText,
    string SelectedAttribute,
    IReadOnlyList<string> ActiveFilters,
    string? ScopeDn,
    bool IsLdapFilterMode,
    DateTime CreatedAtUtc);
