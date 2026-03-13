using System.Diagnostics;
using System.Runtime.Versioning;
using SysOpsCommander.Core.Constants;
using SysOpsCommander.Core.Interfaces;
using SysOpsCommander.Core.Models;
using SysOpsCommander.Core.Validation;
using Serilog;

namespace SysOpsCommander.Services;

/// <summary>
/// Provides Active Directory search, browse, and query operations with multi-domain support.
/// Uses <see cref="IDirectoryAccessor"/> for testability against sealed <c>System.DirectoryServices</c> types.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class ActiveDirectoryService : IActiveDirectoryService, IDisposable
{
    private static readonly string[] SearchProperties =
    [
        "sAMAccountName", "cn", "displayName", "objectClass",
        "distinguishedName", "mail", "dNSHostName", "lastLogonTimestamp",
        "whenCreated", "userAccountControl", "description"
    ];

    private static readonly string[] BrowseProperties =
    [
        "distinguishedName", "cn", "name", "ou", "objectClass", "description"
    ];

    private readonly IDirectoryAccessor _directoryAccessor;
    private readonly ILogger _logger;
    private readonly SemaphoreSlim _domainSwitchLock = new(1, 1);
    private DomainConnection? _activeDomain;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="ActiveDirectoryService"/> class.
    /// </summary>
    /// <param name="directoryAccessor">The directory accessor for LDAP operations.</param>
    /// <param name="logger">The Serilog logger instance.</param>
    public ActiveDirectoryService(IDirectoryAccessor directoryAccessor, ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(directoryAccessor);
        ArgumentNullException.ThrowIfNull(logger);

        _directoryAccessor = directoryAccessor;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<DomainConnection>> GetAvailableDomainsAsync(CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        cancellationToken.ThrowIfCancellationRequested();

        return await Task.Run(() =>
        {
            try
            {
                IReadOnlyList<(string DomainName, string RootDn)> forestDomains =
                    _directoryAccessor.GetForestDomains();

                (string currentDomainName, string currentRootDn) = _directoryAccessor.GetCurrentDomain();

                var connections = new List<DomainConnection>(forestDomains.Count);
                foreach ((string domainName, string rootDn) in forestDomains)
                {
                    connections.Add(new DomainConnection
                    {
                        DomainName = domainName,
                        RootDistinguishedName = rootDn,
                        IsCurrentDomain = string.Equals(domainName, currentDomainName, StringComparison.OrdinalIgnoreCase)
                    });
                }

                _logger.Information("Discovered {Count} domains in forest", connections.Count);
                return (IReadOnlyList<DomainConnection>)connections;
            }
            catch (Exception ex)
            {
                _logger.Warning(ex, "Forest enumeration failed, falling back to environment domain name");

                string fallbackDomainName = Environment.UserDomainName;
                string fallbackRootDn = "DC=" + fallbackDomainName.Replace(".", ",DC=", StringComparison.Ordinal);

                try
                {
                    (string detectedName, string detectedRootDn) = _directoryAccessor.GetCurrentDomain();
                    fallbackDomainName = detectedName;
                    fallbackRootDn = detectedRootDn;
                }
                catch (Exception innerEx)
                {
                    _logger.Warning(innerEx, "GetCurrentDomain also failed, using Environment.UserDomainName");
                }

                return (IReadOnlyList<DomainConnection>)
                [
                    new DomainConnection
                    {
                        DomainName = fallbackDomainName,
                        RootDistinguishedName = fallbackRootDn,
                        IsCurrentDomain = true
                    }
                ];
            }
        }, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task SetActiveDomainAsync(DomainConnection domain, CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(domain);
        cancellationToken.ThrowIfCancellationRequested();

        await _domainSwitchLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await Task.Run(() =>
            {
                string ldapPath = $"LDAP://{domain.RootDistinguishedName}";
                if (!_directoryAccessor.TryBind(ldapPath))
                {
                    throw new InvalidOperationException(
                        $"Cannot bind to domain '{domain.DomainName}' at '{ldapPath}'. Verify network connectivity and permissions.");
                }

                _activeDomain = domain;
                _logger.Information("Switched active domain to {DomainName}", domain.DomainName);
            }, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _ = _domainSwitchLock.Release();
        }
    }

    /// <inheritdoc />
    public DomainConnection GetActiveDomain()
    {
        ThrowIfDisposed();

        if (_activeDomain is not null)
        {
            return _activeDomain;
        }

        try
        {
            (string domainName, string rootDn) = _directoryAccessor.GetCurrentDomain();
            _activeDomain = new DomainConnection
            {
                DomainName = domainName,
                RootDistinguishedName = rootDn,
                IsCurrentDomain = true
            };

            _logger.Information("Initialized AD service for domain {DomainName}", _activeDomain.DomainName);
        }
        catch (Exception ex)
        {
            _logger.Warning(ex, "DirectoryAccessor.GetCurrentDomain failed, using Environment.UserDomainName fallback");

            string fallbackName = Environment.UserDomainName;
            string fallbackRootDn = "DC=" + fallbackName.Replace(".", ",DC=", StringComparison.Ordinal);
            _activeDomain = new DomainConnection
            {
                DomainName = fallbackName,
                RootDistinguishedName = fallbackRootDn,
                IsCurrentDomain = true
            };
        }

        return _activeDomain;
    }

    /// <inheritdoc />
    public async Task<AdSearchResult> SearchAsync(string searchTerm, CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        ArgumentException.ThrowIfNullOrWhiteSpace(searchTerm);
        cancellationToken.ThrowIfCancellationRequested();

        string sanitized = LdapFilterSanitizer.SanitizeInput(searchTerm);
        string filter = $"(|(sAMAccountName=*{sanitized}*)(cn=*{sanitized}*)(displayName=*{sanitized}*)(mail=*{sanitized}*)(dNSHostName=*{sanitized}*)(description=*{sanitized}*))";

        return await ExecuteSearchAsync(filter, searchTerm, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<AdSearchResult> SearchWithFilterAsync(string ldapFilter, CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        ArgumentException.ThrowIfNullOrWhiteSpace(ldapFilter);
        cancellationToken.ThrowIfCancellationRequested();

        return await ExecuteSearchAsync(ldapFilter, ldapFilter, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<AdSearchResult> SearchWithFilterAsync(string ldapFilter, string? baseDn, CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        ArgumentException.ThrowIfNullOrWhiteSpace(ldapFilter);
        cancellationToken.ThrowIfCancellationRequested();

        string effectiveBaseDn = baseDn ?? GetActiveDomain().RootDistinguishedName;
        return await ExecuteSearchAsync(ldapFilter, ldapFilter, effectiveBaseDn, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<AdObject>> BrowseChildrenAsync(string parentDn, CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        ArgumentException.ThrowIfNullOrWhiteSpace(parentDn);
        cancellationToken.ThrowIfCancellationRequested();

        return await Task.Run(() =>
        {
            IReadOnlyList<Dictionary<string, object?>> results = _directoryAccessor.Search(
                parentDn,
                "(objectClass=*)",
                BrowseProperties,
                subtree: false,
                sizeLimit: AppConstants.MaxResultsPerPage,
                pageSize: 500,
                timeout: TimeSpan.FromSeconds(AppConstants.DefaultAdQueryTimeoutSeconds));

            return (IReadOnlyList<AdObject>)results.Select(MapToAdObject).ToList();
        }, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<AdObject> GetObjectDetailAsync(string distinguishedName, CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        ArgumentException.ThrowIfNullOrWhiteSpace(distinguishedName);
        cancellationToken.ThrowIfCancellationRequested();

        return await Task.Run(() =>
        {
            Dictionary<string, object?> rawAttributes = _directoryAccessor.GetAllAttributes(distinguishedName);

            var formattedAttributes = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            foreach (KeyValuePair<string, object?> kvp in rawAttributes)
            {
                formattedAttributes[kvp.Key] = AdAttributeMapper.FormatValue(kvp.Key, kvp.Value);
            }

            string objectClass = ExtractObjectClass(formattedAttributes);

            _logger.Debug("Loaded {AttributeCount} attributes for {Dn}", formattedAttributes.Count, distinguishedName);

            return new AdObject
            {
                DistinguishedName = distinguishedName,
                ObjectClass = objectClass,
                Name = GetStringAttribute(formattedAttributes, "cn") ?? distinguishedName,
                DisplayName = GetStringAttribute(formattedAttributes, "displayName"),
                Description = GetStringAttribute(formattedAttributes, "description"),
                Attributes = formattedAttributes
            };
        }, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<string>> GetGroupMembershipAsync(
        string objectDn,
        bool recursive,
        CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        ArgumentException.ThrowIfNullOrWhiteSpace(objectDn);
        cancellationToken.ThrowIfCancellationRequested();

        return await Task.Run(() =>
        {
            if (recursive)
            {
                IReadOnlyList<byte[]> tokenGroups = _directoryAccessor.GetTokenGroups(objectDn);
                return (IReadOnlyList<string>)[.. tokenGroups
                    .Select(AdAttributeMapper.ResolveSidToName)
                    .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)];
            }

            Dictionary<string, object?> attributes = _directoryAccessor.GetAllAttributes(objectDn);
            return !attributes.TryGetValue("memberOf", out object? memberOfValue) || memberOfValue is null
                ? []
                : memberOfValue switch
                {
                    string single => [single],
                    object[] multiple => [.. multiple
                        .OfType<string>()
                        .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)],
                    _ => []
                };
        }, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public Task<AdSearchResult> GetLockedAccountsAsync(CancellationToken cancellationToken) =>
        SearchWithFilterAsync("(&(objectClass=user)(lockoutTime>=1))", cancellationToken);

    /// <inheritdoc />
    public Task<AdSearchResult> GetDisabledComputersAsync(CancellationToken cancellationToken) =>
        SearchWithFilterAsync("(&(objectClass=computer)(userAccountControl:1.2.840.113556.1.4.803:=2))", cancellationToken);

    /// <inheritdoc />
    public Task<AdSearchResult> GetStaleComputersAsync(int daysInactive, CancellationToken cancellationToken)
    {
        long threshold = DateTime.UtcNow.AddDays(-daysInactive).ToFileTimeUtc();
        string filter = $"(&(objectClass=computer)(lastLogonTimestamp<={threshold}))";
        return SearchWithFilterAsync(filter, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<string>> GetDomainControllersAsync(CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        cancellationToken.ThrowIfCancellationRequested();

        DomainConnection domain = GetActiveDomain();

        return await Task.Run(() =>
        {
            string filter = "(&(objectClass=computer)(userAccountControl:1.2.840.113556.1.4.803:=8192))";
            IReadOnlyList<Dictionary<string, object?>> results = _directoryAccessor.Search(
                domain.RootDistinguishedName,
                filter,
                ["dNSHostName", "distinguishedName", "cn"],
                subtree: true,
                sizeLimit: AppConstants.MaxResultsPerPage,
                pageSize: 500,
                timeout: TimeSpan.FromSeconds(AppConstants.DefaultAdQueryTimeoutSeconds));

            return (IReadOnlyList<string>)[.. results
                .Select(r => GetStringAttribute(r, "dNSHostName") ?? GetStringAttribute(r, "cn") ?? "Unknown")
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)];
        }, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<AdSearchResult> SearchScopedAsync(
        string searchTerm,
        string? baseDn,
        IReadOnlyList<string>? objectClasses,
        string? attribute,
        CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        ArgumentException.ThrowIfNullOrWhiteSpace(searchTerm);
        cancellationToken.ThrowIfCancellationRequested();

        string sanitized = LdapFilterSanitizer.SanitizeInput(searchTerm);

        // Build attribute filter
        string attrFilter = string.IsNullOrEmpty(attribute)
            ? $"(|(sAMAccountName=*{sanitized}*)(cn=*{sanitized}*)(displayName=*{sanitized}*)(mail=*{sanitized}*)(dNSHostName=*{sanitized}*)(description=*{sanitized}*))"
            : $"({attribute}=*{sanitized}*)";

        // Build object class filter
        string classFilter = BuildObjectClassFilter(objectClasses);

        // Combine filters
        string filter = string.IsNullOrEmpty(classFilter)
            ? attrFilter
            : $"(&{classFilter}{attrFilter})";

        string effectiveBaseDn = baseDn ?? GetActiveDomain().RootDistinguishedName;

        return await ExecuteSearchAsync(filter, searchTerm, effectiveBaseDn, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<AdSearchResult> GetGroupMembersAsync(
        string groupDn,
        bool recursive,
        CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        ArgumentException.ThrowIfNullOrWhiteSpace(groupDn);
        cancellationToken.ThrowIfCancellationRequested();

        string sanitizedDn = LdapFilterSanitizer.SanitizeInput(groupDn);

        // Direct: memberOf=groupDn; Recursive: memberOf with LDAP_MATCHING_RULE_IN_CHAIN OID
        string filter = recursive
            ? $"(memberOf:1.2.840.113556.1.4.1941:={sanitizedDn})"
            : $"(memberOf={sanitizedDn})";

        return await ExecuteSearchAsync(filter, $"Members of: {groupDn}", cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _domainSwitchLock.Dispose();
        _directoryAccessor.Dispose();
        _disposed = true;
    }

    private async Task<AdSearchResult> ExecuteSearchAsync(
        string filter,
        string queryLabel,
        CancellationToken cancellationToken)
    {
        string baseDn = GetActiveDomain().RootDistinguishedName;
        return await ExecuteSearchAsync(filter, queryLabel, baseDn, cancellationToken).ConfigureAwait(false);
    }

    private async Task<AdSearchResult> ExecuteSearchAsync(
        string filter,
        string queryLabel,
        string baseDn,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();

        IReadOnlyList<Dictionary<string, object?>> results = await Task.Run(() =>
            _directoryAccessor.Search(
                baseDn,
                filter,
                SearchProperties,
                subtree: true,
                sizeLimit: AppConstants.MaxResultsPerPage,
                pageSize: 500,
                timeout: TimeSpan.FromSeconds(AppConstants.DefaultAdQueryTimeoutSeconds)),
            cancellationToken).ConfigureAwait(false);

        stopwatch.Stop();

        var adObjects = results.Select(MapToAdObject).ToList();

        return new AdSearchResult
        {
            Results = adObjects,
            Query = queryLabel,
            ExecutionTime = stopwatch.Elapsed,
            TotalResultCount = adObjects.Count,
            HasMoreResults = adObjects.Count >= AppConstants.MaxResultsPerPage
        };
    }

    private static AdObject MapToAdObject(Dictionary<string, object?> properties)
    {
        string dn = GetStringAttribute(properties, "distinguishedName") ?? string.Empty;
        string objectClass = ExtractObjectClass(properties);
        string name = GetNonEmptyAttribute(properties, "name")
                      ?? GetNonEmptyAttribute(properties, "cn")
                      ?? GetNonEmptyAttribute(properties, "ou")
                      ?? ExtractRdnValue(dn);

        if (string.IsNullOrWhiteSpace(name))
        {
            name = dn is { Length: > 0 } ? dn : "(unknown)";
        }

        string? displayName = GetStringAttribute(properties, "displayName");

        var attributes = new Dictionary<string, object?>(properties, StringComparer.OrdinalIgnoreCase);

        return new AdObject
        {
            DistinguishedName = dn,
            ObjectClass = objectClass,
            Name = name,
            DisplayName = displayName,
            Description = GetStringAttribute(properties, "description"),
            Attributes = attributes
        };
    }

    private static string ExtractObjectClass(Dictionary<string, object?> properties)
    {
        if (!properties.TryGetValue("objectClass", out object? value) || value is null)
        {
            return "unknown";
        }

        // objectClass is multi-valued; the last value is the most specific
        return value switch
        {
            string single => single,
            object[] multiple when multiple.Length > 0 => multiple[^1]?.ToString() ?? "unknown",
            _ => "unknown"
        };
    }

    private static string? GetStringAttribute(Dictionary<string, object?> properties, string name) =>
        properties.TryGetValue(name, out object? value) ? value?.ToString() : null;

    private static string? GetNonEmptyAttribute(Dictionary<string, object?> properties, string name)
    {
        string? value = GetStringAttribute(properties, name);
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    /// <summary>
    /// Extracts the value portion of the first RDN component from a DN.
    /// E.g. "OU=Sales,DC=contoso,DC=com" → "Sales"
    /// </summary>
    private static string ExtractRdnValue(string dn)
    {
        if (string.IsNullOrEmpty(dn))
        {
            return dn;
        }

        int equalsIndex = dn.IndexOf('=');
        if (equalsIndex < 0)
        {
            return dn;
        }

        int commaIndex = dn.IndexOf(',', equalsIndex);
        return commaIndex > 0
            ? dn[(equalsIndex + 1)..commaIndex]
            : dn[(equalsIndex + 1)..];
    }

    private static string BuildObjectClassFilter(IReadOnlyList<string>? objectClasses)
    {
        if (objectClasses is null || objectClasses.Count == 0)
        {
            return string.Empty;
        }

        if (objectClasses.Count == 1)
        {
            return $"(objectClass={LdapFilterSanitizer.SanitizeInput(objectClasses[0])})";
        }

        string orClauses = string.Concat(objectClasses.Select(c =>
            $"(objectClass={LdapFilterSanitizer.SanitizeInput(c)})"));
        return $"(|{orClauses})";
    }

    private void ThrowIfDisposed() =>
        ObjectDisposedException.ThrowIf(_disposed, this);
}
