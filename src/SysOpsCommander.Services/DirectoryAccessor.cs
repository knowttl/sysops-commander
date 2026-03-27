using System.DirectoryServices;
using System.DirectoryServices.ActiveDirectory;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Principal;
using SysOpsCommander.Core.Interfaces;
using SysOpsCommander.Core.Models;

namespace SysOpsCommander.Services;

/// <summary>
/// Production implementation of <see cref="IDirectoryAccessor"/> using <c>System.DirectoryServices</c>.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class DirectoryAccessor : IDirectoryAccessor
{
    private bool _disposed;

    /// <inheritdoc />
    public (string DomainName, string RootDn) GetCurrentDomain()
    {
        try
        {
            using var domain = Domain.GetCurrentDomain();
            string rootDn = ConvertDomainNameToRootDn(domain.Name);
            return (domain.Name, rootDn);
        }
        catch (ActiveDirectoryObjectNotFoundException ex)
        {
            throw new InvalidOperationException(
                "No Active Directory domain found for the current user. Verify the machine is domain-joined.", ex);
        }
        catch (ActiveDirectoryOperationException ex)
        {
            throw new InvalidOperationException(
                "Active Directory domain detection failed. A domain controller may be unreachable.", ex);
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<(string DomainName, string RootDn)> GetForestDomains()
    {
        try
        {
            using var forest = Forest.GetCurrentForest();
            var domains = new List<(string, string)>();

            foreach (Domain domain in forest.Domains)
            {
                string rootDn = ConvertDomainNameToRootDn(domain.Name);
                domains.Add((domain.Name, rootDn));
                domain.Dispose();
            }

            return domains;
        }
        catch (ActiveDirectoryObjectNotFoundException ex)
        {
            throw new InvalidOperationException(
                "No Active Directory forest found for the current user. Verify the machine is domain-joined.", ex);
        }
        catch (ActiveDirectoryOperationException ex)
        {
            throw new InvalidOperationException(
                "Active Directory forest enumeration failed. A domain controller may be unreachable.", ex);
        }
    }

    /// <inheritdoc />
    public bool TryBind(string ldapPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ldapPath);

        try
        {
            using var entry = new DirectoryEntry(ldapPath);
            // Force the bind by accessing NativeObject
            _ = entry.NativeObject;
            return true;
        }
        catch (DirectoryServicesCOMException)
        {
            return false;
        }
        catch (System.Runtime.InteropServices.COMException)
        {
            return false;
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<Dictionary<string, object?>> Search(
        string baseDn,
        string filter,
        string[] propertiesToLoad,
        bool subtree,
        int sizeLimit,
        int pageSize,
        TimeSpan timeout)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(baseDn);
        ArgumentException.ThrowIfNullOrWhiteSpace(filter);
        ArgumentNullException.ThrowIfNull(propertiesToLoad);

        using var entry = new DirectoryEntry($"LDAP://{baseDn}");
        using var searcher = new DirectorySearcher(entry)
        {
            Filter = filter,
            SearchScope = subtree ? SearchScope.Subtree : SearchScope.OneLevel,
            SizeLimit = sizeLimit,
            PageSize = pageSize,
            ServerTimeLimit = timeout
        };

        searcher.PropertiesToLoad.AddRange(propertiesToLoad);

        using SearchResultCollection results = searcher.FindAll();
        var list = new List<Dictionary<string, object?>>();

        foreach (SearchResult result in results)
        {
            var dict = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            foreach (string propName in result.Properties.PropertyNames!)
            {
                ResultPropertyValueCollection values = result.Properties[propName];
                dict[propName] = values.Count == 1
                    ? values[0]
                    : values.Cast<object>().ToArray();
            }

            list.Add(dict);
        }

        return list;
    }

    /// <inheritdoc />
    public Dictionary<string, object?> GetAllAttributes(string distinguishedName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(distinguishedName);

        using var entry = new DirectoryEntry($"LDAP://{distinguishedName}");
        entry.RefreshCache();

        var attributes = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

        foreach (string propName in entry.Properties.PropertyNames!)
        {
            PropertyValueCollection values = entry.Properties[propName];
            attributes[propName] = values.Count == 1
                ? ConvertComObject(values[0]!)
                : values.Cast<object>().Select(ConvertComObject).ToArray();
        }

        return attributes;
    }

    /// <summary>
    /// Converts COM interop objects (IADsLargeInteger) to .NET types.
    /// Falls through for non-COM values.
    /// </summary>
    private static object ConvertComObject(object value)
    {
        if (!Marshal.IsComObject(value))
        {
            return value;
        }

        try
        {
            // IADsLargeInteger — used for file-time attributes (lastLogon, accountExpires, etc.)
            int highPart = (int)value.GetType().InvokeMember(
                "HighPart", System.Reflection.BindingFlags.GetProperty, null, value, null,
                System.Globalization.CultureInfo.InvariantCulture)!;
            int lowPart = (int)value.GetType().InvokeMember(
                "LowPart", System.Reflection.BindingFlags.GetProperty, null, value, null,
                System.Globalization.CultureInfo.InvariantCulture)!;
            long fileTime = ((long)highPart << 32) | (uint)lowPart;
            return fileTime;
        }
        catch (Exception)
        {
            return value.GetType().Name;
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<byte[]> GetTokenGroups(string distinguishedName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(distinguishedName);

        using var entry = new DirectoryEntry($"LDAP://{distinguishedName}");
        entry.RefreshCache(["tokenGroups"]);

        var sids = new List<byte[]>();
        foreach (byte[] sidBytes in entry.Properties["tokenGroups"])
        {
            sids.Add(sidBytes);
        }

        return sids;
    }

    /// <inheritdoc />
    public IReadOnlyList<AdAccessControlEntry> GetAccessControlEntries(string distinguishedName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(distinguishedName);

        using var entry = new DirectoryEntry($"LDAP://{distinguishedName}");
        ActiveDirectorySecurity security = entry.ObjectSecurity;

        AuthorizationRuleCollection rules = security.GetAccessRules(
            includeExplicit: true, includeInherited: true, targetType: typeof(NTAccount));

        var entries = new List<AdAccessControlEntry>(rules.Count);

        foreach (ActiveDirectoryAccessRule rule in rules)
        {
            string identity;
            try
            {
                identity = rule.IdentityReference.Value;
            }
            catch (IdentityNotMappedException)
            {
                identity = rule.IdentityReference.Value;
            }

            string inheritedFrom = rule.IsInherited
                ? rule.InheritanceType.ToString()
                : string.Empty;

            entries.Add(new AdAccessControlEntry(
                Identity: identity,
                AccessType: rule.AccessControlType.ToString(),
                Permission: rule.ActiveDirectoryRights.ToString(),
                IsInherited: rule.IsInherited,
                InheritedFrom: rule.IsInherited ? inheritedFrom : null));
        }

        return entries;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
    }

    private static string ConvertDomainNameToRootDn(string domainName) =>
        "DC=" + domainName.Replace(".", ",DC=", StringComparison.Ordinal);
}
