using System.DirectoryServices;
using System.DirectoryServices.ActiveDirectory;
using System.Runtime.Versioning;
using SysOpsCommander.Core.Interfaces;

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
        using var domain = Domain.GetCurrentDomain();
        string rootDn = ConvertDomainNameToRootDn(domain.Name);
        return (domain.Name, rootDn);
    }

    /// <inheritdoc />
    public IReadOnlyList<(string DomainName, string RootDn)> GetForestDomains()
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
                ? values[0]
                : values.Cast<object>().ToArray();
        }

        return attributes;
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
