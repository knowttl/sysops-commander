namespace SysOpsCommander.Core.Models;

/// <summary>
/// Represents a single access control entry (ACE) from an AD object's DACL.
/// </summary>
/// <param name="Identity">The security principal (e.g., "DOMAIN\GroupName" or a SID string).</param>
/// <param name="AccessType">The access control type: "Allow" or "Deny".</param>
/// <param name="Permission">The Active Directory rights granted or denied.</param>
/// <param name="IsInherited">Whether this ACE is inherited from a parent object.</param>
/// <param name="InheritedFrom">A description of the inheritance source, or <see langword="null"/> if explicit.</param>
public sealed record AdAccessControlEntry(
    string Identity,
    string AccessType,
    string Permission,
    bool IsInherited,
    string? InheritedFrom);
