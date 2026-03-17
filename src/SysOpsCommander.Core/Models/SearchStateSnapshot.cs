namespace SysOpsCommander.Core.Models;

/// <summary>
/// Captures a snapshot of the AD Explorer search state for undo navigation.
/// </summary>
public sealed record SearchStateSnapshot(
    IReadOnlyList<AdObject> Results,
    string ResultStatus,
    string SearchText,
    string SelectedAttribute,
    bool IsLdapFilterMode,
    string RawLdapFilter,
    string? ScopeDisplay,
    bool SearchEntireDomain,
    bool FilterAll,
    bool FilterUsers,
    bool FilterComputers,
    bool FilterGroups,
    bool FilterOus);
