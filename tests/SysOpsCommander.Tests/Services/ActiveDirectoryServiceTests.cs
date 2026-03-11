using FluentAssertions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Serilog;
using SysOpsCommander.Core.Constants;
using SysOpsCommander.Core.Interfaces;
using SysOpsCommander.Core.Models;
using SysOpsCommander.Services;

namespace SysOpsCommander.Tests.Services;

[System.Runtime.Versioning.SupportedOSPlatform("windows")]
public sealed class ActiveDirectoryServiceTests : IDisposable
{
    private readonly IDirectoryAccessor _accessor = Substitute.For<IDirectoryAccessor>();
    private readonly ILogger _logger = Substitute.For<ILogger>();
    private readonly ActiveDirectoryService _service;

    public ActiveDirectoryServiceTests()
    {
        _accessor.GetCurrentDomain().Returns(("corp.contoso.com", "DC=corp,DC=contoso,DC=com"));
        _service = new ActiveDirectoryService(_accessor, _logger);
    }

    public void Dispose() =>
        _service.Dispose();

    [Fact]
    public async Task SearchAsync_BuildsCompoundFilterWithSanitizedInput()
    {
        _accessor.Search(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<string[]>(),
            Arg.Any<bool>(),
            Arg.Any<int>(),
            Arg.Any<int>(),
            Arg.Any<TimeSpan>())
            .Returns([]);

        AdSearchResult result = await _service.SearchAsync("jsmith", CancellationToken.None);

        _accessor.Received(1).Search(
            "DC=corp,DC=contoso,DC=com",
            Arg.Is<string>(f =>
                f.Contains("sAMAccountName=*jsmith*", StringComparison.Ordinal) &&
                f.Contains("cn=*jsmith*", StringComparison.Ordinal) &&
                f.Contains("displayName=*jsmith*", StringComparison.Ordinal) &&
                f.Contains("mail=*jsmith*", StringComparison.Ordinal) &&
                f.Contains("dNSHostName=*jsmith*", StringComparison.Ordinal) &&
                f.StartsWith("(|", StringComparison.Ordinal)),
            Arg.Any<string[]>(),
            true,
            AppConstants.MaxResultsPerPage,
            500,
            Arg.Any<TimeSpan>());

        result.Results.Should().BeEmpty();
        result.Query.Should().Be("jsmith");
    }

    [Fact]
    public async Task SearchAsync_SanitizesSpecialCharacters()
    {
        _accessor.Search(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<string[]>(),
            Arg.Any<bool>(),
            Arg.Any<int>(),
            Arg.Any<int>(),
            Arg.Any<TimeSpan>())
            .Returns([]);

        await _service.SearchAsync("test*user", CancellationToken.None);

        // LdapFilterSanitizer escapes * to \2a
        _accessor.Received(1).Search(
            Arg.Any<string>(),
            Arg.Is<string>(f => f.Contains(@"\2a", StringComparison.Ordinal) && !f.Contains("test*user", StringComparison.Ordinal)),
            Arg.Any<string[]>(),
            Arg.Any<bool>(),
            Arg.Any<int>(),
            Arg.Any<int>(),
            Arg.Any<TimeSpan>());
    }

    [Fact]
    public async Task SearchAsync_MapsResultsToAdObjects()
    {
        var searchResult = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["distinguishedName"] = "CN=jsmith,OU=Users,DC=corp,DC=contoso,DC=com",
            ["cn"] = "jsmith",
            ["objectClass"] = new object[] { "top", "person", "user" },
            ["displayName"] = "John Smith"
        };

        _accessor.Search(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string[]>(),
            Arg.Any<bool>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<TimeSpan>())
            .Returns((IReadOnlyList<Dictionary<string, object?>>)[searchResult]);

        AdSearchResult result = await _service.SearchAsync("jsmith", CancellationToken.None);

        result.Results.Should().HaveCount(1);
        AdObject adObj = result.Results[0];
        adObj.Name.Should().Be("jsmith");
        adObj.DisplayName.Should().Be("John Smith");
        adObj.ObjectClass.Should().Be("user");
        adObj.DistinguishedName.Should().Be("CN=jsmith,OU=Users,DC=corp,DC=contoso,DC=com");
    }

    [Fact]
    public async Task GetAvailableDomainsAsync_ReturnsForestDomains()
    {
        _accessor.GetForestDomains().Returns((IReadOnlyList<(string, string)>)
        [
            ("corp.contoso.com", "DC=corp,DC=contoso,DC=com"),
            ("child.contoso.com", "DC=child,DC=contoso,DC=com")
        ]);

        IReadOnlyList<DomainConnection> domains =
            await _service.GetAvailableDomainsAsync(CancellationToken.None);

        domains.Should().HaveCount(2);
        domains.Should().ContainSingle(d => d.IsCurrentDomain && d.DomainName == "corp.contoso.com");
    }

    [Fact]
    public async Task GetAvailableDomainsAsync_ForestEnumerationFails_FallsBackToCurrentDomain()
    {
        _accessor.GetForestDomains().Throws(new InvalidOperationException("Forest not available"));

        IReadOnlyList<DomainConnection> domains =
            await _service.GetAvailableDomainsAsync(CancellationToken.None);

        domains.Should().HaveCount(1);
        domains[0].DomainName.Should().Be("corp.contoso.com");
        domains[0].IsCurrentDomain.Should().BeTrue();
    }

    [Fact]
    public async Task SetActiveDomainAsync_BindSucceeds_UpdatesActiveDomain()
    {
        var newDomain = new DomainConnection
        {
            DomainName = "child.contoso.com",
            RootDistinguishedName = "DC=child,DC=contoso,DC=com"
        };

        _accessor.TryBind("LDAP://DC=child,DC=contoso,DC=com").Returns(true);

        await _service.SetActiveDomainAsync(newDomain, CancellationToken.None);

        DomainConnection active = _service.GetActiveDomain();
        active.DomainName.Should().Be("child.contoso.com");
    }

    [Fact]
    public async Task SetActiveDomainAsync_BindFails_ThrowsInvalidOperationException()
    {
        var unreachableDomain = new DomainConnection
        {
            DomainName = "unreachable.contoso.com",
            RootDistinguishedName = "DC=unreachable,DC=contoso,DC=com"
        };

        _accessor.TryBind(Arg.Any<string>()).Returns(false);

        Func<Task> act = () => _service.SetActiveDomainAsync(unreachableDomain, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Cannot bind*unreachable.contoso.com*");
    }

    [Fact]
    public void GetActiveDomain_NoDomainSet_ReturnsCurrentDomain()
    {
        DomainConnection domain = _service.GetActiveDomain();

        domain.DomainName.Should().Be("corp.contoso.com");
        domain.RootDistinguishedName.Should().Be("DC=corp,DC=contoso,DC=com");
        domain.IsCurrentDomain.Should().BeTrue();
    }

    [Fact]
    public async Task GetLockedAccountsAsync_UsesCorrectFilter()
    {
        _accessor.Search(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string[]>(),
            Arg.Any<bool>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<TimeSpan>())
            .Returns([]);

        await _service.GetLockedAccountsAsync(CancellationToken.None);

        _accessor.Received(1).Search(
            Arg.Any<string>(),
            Arg.Is<string>(f => f.Contains("lockoutTime>=1", StringComparison.Ordinal)),
            Arg.Any<string[]>(),
            Arg.Any<bool>(),
            Arg.Any<int>(),
            Arg.Any<int>(),
            Arg.Any<TimeSpan>());
    }

    [Fact]
    public async Task GetDisabledComputersAsync_UsesUacBitwiseFilter()
    {
        _accessor.Search(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string[]>(),
            Arg.Any<bool>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<TimeSpan>())
            .Returns([]);

        await _service.GetDisabledComputersAsync(CancellationToken.None);

        _accessor.Received(1).Search(
            Arg.Any<string>(),
            Arg.Is<string>(f =>
                f.Contains("objectClass=computer", StringComparison.Ordinal) &&
                f.Contains("1.2.840.113556.1.4.803:=2", StringComparison.Ordinal)),
            Arg.Any<string[]>(),
            Arg.Any<bool>(),
            Arg.Any<int>(),
            Arg.Any<int>(),
            Arg.Any<TimeSpan>());
    }

    [Fact]
    public async Task GetStaleComputersAsync_UsesFileTimeThreshold()
    {
        _accessor.Search(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string[]>(),
            Arg.Any<bool>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<TimeSpan>())
            .Returns([]);

        long expectedThreshold = DateTime.UtcNow.AddDays(-30).ToFileTimeUtc();

        await _service.GetStaleComputersAsync(30, CancellationToken.None);

        _accessor.Received(1).Search(
            Arg.Any<string>(),
            Arg.Is<string>(f =>
                f.Contains("objectClass=computer", StringComparison.Ordinal) &&
                f.Contains("lastLogonTimestamp<=", StringComparison.Ordinal)),
            Arg.Any<string[]>(),
            Arg.Any<bool>(),
            Arg.Any<int>(),
            Arg.Any<int>(),
            Arg.Any<TimeSpan>());
    }

    [Fact]
    public async Task GetDomainControllersAsync_UsesUac8192Filter()
    {
        var dcResult = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["dNSHostName"] = "dc01.corp.contoso.com",
            ["distinguishedName"] = "CN=DC01,OU=DCs,DC=corp,DC=contoso,DC=com",
            ["cn"] = "DC01"
        };

        _accessor.Search(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string[]>(),
            Arg.Any<bool>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<TimeSpan>())
            .Returns((IReadOnlyList<Dictionary<string, object?>>)[dcResult]);

        IReadOnlyList<string> dcs = await _service.GetDomainControllersAsync(CancellationToken.None);

        dcs.Should().HaveCount(1);
        dcs[0].Should().Be("dc01.corp.contoso.com");

        _accessor.Received(1).Search(
            Arg.Any<string>(),
            Arg.Is<string>(f => f.Contains("8192", StringComparison.Ordinal)),
            Arg.Any<string[]>(),
            Arg.Any<bool>(),
            Arg.Any<int>(),
            Arg.Any<int>(),
            Arg.Any<TimeSpan>());
    }

    [Fact]
    public async Task BrowseChildrenAsync_UsesOneLevelScope()
    {
        _accessor.Search(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string[]>(),
            Arg.Any<bool>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<TimeSpan>())
            .Returns([]);

        await _service.BrowseChildrenAsync("DC=corp,DC=contoso,DC=com", CancellationToken.None);

        _accessor.Received(1).Search(
            "DC=corp,DC=contoso,DC=com",
            "(objectClass=*)",
            Arg.Any<string[]>(),
            false,
            Arg.Any<int>(),
            Arg.Any<int>(),
            Arg.Any<TimeSpan>());
    }

    [Fact]
    public async Task GetObjectDetailAsync_FormatsAttributesUsingMapper()
    {
        var rawAttrs = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["cn"] = "TestUser",
            ["objectClass"] = "user",
            ["displayName"] = "Test User",
            ["userAccountControl"] = 512
        };

        _accessor.GetAllAttributes("CN=TestUser,DC=corp,DC=contoso,DC=com").Returns(rawAttrs);

        AdObject obj = await _service.GetObjectDetailAsync(
            "CN=TestUser,DC=corp,DC=contoso,DC=com", CancellationToken.None);

        obj.Name.Should().Be("TestUser");
        obj.ObjectClass.Should().Be("user");

        // UAC 512 = NORMAL_ACCOUNT — should be decoded by AdAttributeMapper
        obj.Attributes["userAccountControl"].Should().BeOfType<List<string>>()
            .Which.Should().Contain("NORMAL_ACCOUNT");
    }

    [Fact]
    public async Task GetGroupMembershipAsync_DirectMembership_ReturnsMemberOfValues()
    {
        var rawAttrs = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["memberOf"] = new object[] { "CN=GroupB,OU=Groups,DC=corp,DC=contoso,DC=com", "CN=GroupA,OU=Groups,DC=corp,DC=contoso,DC=com" }
        };

        _accessor.GetAllAttributes("CN=User,DC=corp,DC=contoso,DC=com").Returns(rawAttrs);

        IReadOnlyList<string> groups = await _service.GetGroupMembershipAsync(
            "CN=User,DC=corp,DC=contoso,DC=com", recursive: false, CancellationToken.None);

        groups.Should().HaveCount(2);
        // Should be sorted
        groups[0].Should().Be("CN=GroupA,OU=Groups,DC=corp,DC=contoso,DC=com");
        groups[1].Should().Be("CN=GroupB,OU=Groups,DC=corp,DC=contoso,DC=com");
    }

    [Fact]
    public async Task GetGroupMembershipAsync_DirectMembership_SingleValue_ReturnsSingleGroup()
    {
        var rawAttrs = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["memberOf"] = "CN=SingleGroup,OU=Groups,DC=corp,DC=contoso,DC=com"
        };

        _accessor.GetAllAttributes("CN=User,DC=corp,DC=contoso,DC=com").Returns(rawAttrs);

        IReadOnlyList<string> groups = await _service.GetGroupMembershipAsync(
            "CN=User,DC=corp,DC=contoso,DC=com", recursive: false, CancellationToken.None);

        groups.Should().ContainSingle()
            .Which.Should().Be("CN=SingleGroup,OU=Groups,DC=corp,DC=contoso,DC=com");
    }

    [Fact]
    public async Task GetGroupMembershipAsync_NoMemberOf_ReturnsEmpty()
    {
        var rawAttrs = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["cn"] = "User"
        };

        _accessor.GetAllAttributes("CN=User,DC=corp,DC=contoso,DC=com").Returns(rawAttrs);

        IReadOnlyList<string> groups = await _service.GetGroupMembershipAsync(
            "CN=User,DC=corp,DC=contoso,DC=com", recursive: false, CancellationToken.None);

        groups.Should().BeEmpty();
    }

    [Fact]
    public async Task SearchAsync_CancellationRequested_ThrowsOperationCanceled()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        Func<Task> act = () => _service.SearchAsync("test", cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public void Dispose_CalledTwice_DoesNotThrow()
    {
        var service = new ActiveDirectoryService(_accessor, _logger);
        service.Dispose();

        Action act = service.Dispose;

        act.Should().NotThrow();
    }

    [Fact]
    public void SearchAsync_AfterDispose_ThrowsObjectDisposedException()
    {
        var service = new ActiveDirectoryService(_accessor, _logger);
        service.Dispose();

        Func<Task> act = () => service.SearchAsync("test", CancellationToken.None);

        act.Should().ThrowAsync<ObjectDisposedException>();
    }

    [Fact]
    public async Task SearchAsync_ResultCountReachesMax_HasMoreResultsIsTrue()
    {
        var results = Enumerable.Range(0, AppConstants.MaxResultsPerPage)
            .Select(i => new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["distinguishedName"] = $"CN=User{i},DC=corp,DC=contoso,DC=com",
                ["cn"] = $"User{i}",
                ["objectClass"] = "user"
            })
            .ToList();

        _accessor.Search(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string[]>(),
            Arg.Any<bool>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<TimeSpan>())
            .Returns(results);

        AdSearchResult searchResult = await _service.SearchAsync("User", CancellationToken.None);

        searchResult.HasMoreResults.Should().BeTrue();
        searchResult.TotalResultCount.Should().Be(AppConstants.MaxResultsPerPage);
    }
}
