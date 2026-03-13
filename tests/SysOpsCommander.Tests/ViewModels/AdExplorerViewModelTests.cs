using FluentAssertions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Serilog;
using SysOpsCommander.Core.Constants;
using SysOpsCommander.Core.Interfaces;
using SysOpsCommander.Core.Models;
using SysOpsCommander.ViewModels;

namespace SysOpsCommander.Tests.ViewModels;

public sealed class AdExplorerViewModelTests : IDisposable
{
    private readonly IActiveDirectoryService _adService = Substitute.For<IActiveDirectoryService>();
    private readonly IHostTargetingService _hostTargetingService = Substitute.For<IHostTargetingService>();
    private readonly ISettingsService _settingsService = Substitute.For<ISettingsService>();
    private readonly IDialogService _dialogService = Substitute.For<IDialogService>();
    private readonly ILogger _logger = Substitute.For<ILogger>();
    private readonly AdExplorerViewModel _viewModel;

    public AdExplorerViewModelTests()
    {
        _adService.GetActiveDomain().Returns(new DomainConnection
        {
            DomainName = "test.local",
            RootDistinguishedName = "DC=test,DC=local"
        });

        _viewModel = new AdExplorerViewModel(
            _adService,
            _hostTargetingService,
            _settingsService,
            _dialogService,
            _logger);
    }

    public void Dispose() => _viewModel.Dispose();

    [Fact]
    public async Task LoadTreeRoot_SetsActiveDomainDisplay()
    {
        _adService.BrowseChildrenAsync("DC=test,DC=local", Arg.Any<CancellationToken>())
            .Returns([]);

        await _viewModel.LoadTreeRootCommand.ExecuteAsync(null);

        _viewModel.ActiveDomainDisplay.Should().Be("test.local");
    }

    [Fact]
    public async Task LoadTreeRoot_PopulatesTreeNodes()
    {
        List<AdObject> children =
        [
            new() { Name = "Users", DistinguishedName = "CN=Users,DC=test,DC=local", ObjectClass = "container" },
            new() { Name = "Computers", DistinguishedName = "OU=Computers,DC=test,DC=local", ObjectClass = "organizationalUnit" }
        ];
        _adService.BrowseChildrenAsync("DC=test,DC=local", Arg.Any<CancellationToken>())
            .Returns(children);

        await _viewModel.LoadTreeRootCommand.ExecuteAsync(null);

        _viewModel.TreeNodes.Should().HaveCount(2);
        _viewModel.TreeNodes[0].Name.Should().Be("Users");
        _viewModel.TreeNodes[1].Name.Should().Be("Computers");
    }

    [Fact]
    public async Task ExpandNode_LoadsChildren()
    {
        var node = new AdTreeNode
        {
            Name = "Users",
            DistinguishedName = "CN=Users,DC=test,DC=local",
            ObjectClass = "container",
            HasDummyChild = true
        };
        List<AdObject> children =
        [
            new() { Name = "Admin", DistinguishedName = "CN=Admin,CN=Users,DC=test,DC=local", ObjectClass = "user" }
        ];
        _adService.BrowseChildrenAsync("CN=Users,DC=test,DC=local", Arg.Any<CancellationToken>())
            .Returns(children);

        await _viewModel.ExpandNodeCommand.ExecuteAsync(node);

        node.Children.Should().HaveCount(1);
        node.Children[0].Name.Should().Be("Admin");
        node.HasDummyChild.Should().BeFalse();
    }

    [Fact]
    public async Task ExpandNode_WhenAlreadyExpanded_DoesNotReload()
    {
        var node = new AdTreeNode
        {
            Name = "Users",
            DistinguishedName = "CN=Users,DC=test,DC=local",
            ObjectClass = "container",
            HasDummyChild = false
        };

        await _viewModel.ExpandNodeCommand.ExecuteAsync(node);

        await _adService.DidNotReceive().BrowseChildrenAsync(
            Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Search_PopulatesSearchResults()
    {
        var result = new AdSearchResult
        {
            Query = "admin",
            TotalResultCount = 1,
            HasMoreResults = false,
            ExecutionTime = TimeSpan.FromMilliseconds(42),
            Results = [new AdObject { Name = "Admin", DistinguishedName = "CN=Admin,DC=test,DC=local", ObjectClass = "user" }]
        };
        _adService.SearchScopedAsync("admin", Arg.Any<string?>(), Arg.Any<IReadOnlyList<string>?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>()).Returns(result);

        _viewModel.SearchText = "admin";
        await _viewModel.SearchCommand.ExecuteAsync(null);

        _viewModel.SearchResults.Should().HaveCount(1);
        _viewModel.SearchResults[0].Name.Should().Be("Admin");
        _viewModel.ResultStatus.Should().Contain("1 results found");
    }

    [Fact]
    public async Task Search_WithEmptyText_DoesNotCallService()
    {
        _viewModel.SearchText = "";
        await _viewModel.SearchCommand.ExecuteAsync(null);

        await _adService.DidNotReceive().SearchScopedAsync(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<IReadOnlyList<string>?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Search_WhenFails_SetsErrorStatus()
    {
        _adService.SearchScopedAsync("fail", Arg.Any<string?>(), Arg.Any<IReadOnlyList<string>?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("AD unreachable"));

        _viewModel.SearchText = "fail";
        await _viewModel.SearchCommand.ExecuteAsync(null);

        _viewModel.ResultStatus.Should().Contain("Search failed");
        _viewModel.IsSearching.Should().BeFalse();
    }

    [Fact]
    public async Task GetLockedAccounts_PopulatesResults()
    {
        var result = new AdSearchResult
        {
            Query = "locked",
            TotalResultCount = 2,
            HasMoreResults = false,
            ExecutionTime = TimeSpan.FromMilliseconds(15),
            Results =
            [
                new AdObject { Name = "User1", DistinguishedName = "CN=User1,DC=test,DC=local", ObjectClass = "user" },
                new AdObject { Name = "User2", DistinguishedName = "CN=User2,DC=test,DC=local", ObjectClass = "user" }
            ]
        };
        _adService.GetLockedAccountsAsync(Arg.Any<CancellationToken>()).Returns(result);

        await _viewModel.GetLockedAccountsCommand.ExecuteAsync(null);

        _viewModel.SearchResults.Should().HaveCount(2);
        _viewModel.ResultStatus.Should().Contain("Locked Accounts");
    }

    [Fact]
    public async Task GetStaleComputers_ReadsThresholdFromSettings()
    {
        _settingsService.GetTypedAsync("StaleComputerThresholdDays", AppConstants.DefaultStaleComputerDays, Arg.Any<CancellationToken>())
            .Returns(60);
        var result = new AdSearchResult
        {
            Query = "stale",
            TotalResultCount = 0,
            HasMoreResults = false,
            ExecutionTime = TimeSpan.FromMilliseconds(10),
            Results = []
        };
        _adService.GetStaleComputersAsync(60, Arg.Any<CancellationToken>()).Returns(result);

        await _viewModel.GetStaleComputersCommand.ExecuteAsync(null);

        _viewModel.StaleThresholdDays.Should().Be(60);
        await _adService.Received(1).GetStaleComputersAsync(60, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetDomainControllers_PopulatesResults()
    {
        _adService.GetDomainControllersAsync(Arg.Any<CancellationToken>())
            .Returns(["DC1.test.local", "DC2.test.local"]);

        await _viewModel.GetDomainControllersCommand.ExecuteAsync(null);

        _viewModel.SearchResults.Should().HaveCount(2);
        _viewModel.ResultStatus.Should().Contain("2 domain controllers found");
    }

    [Fact]
    public void SendToExecutionTargets_AddsComputerObjects()
    {
        _viewModel.SearchResults.Add(new AdObject
        {
            Name = "PC1",
            DistinguishedName = "CN=PC1,DC=test,DC=local",
            ObjectClass = "computer"
        });
        _viewModel.SearchResults.Add(new AdObject
        {
            Name = "User1",
            DistinguishedName = "CN=User1,DC=test,DC=local",
            ObjectClass = "user"
        });

        _viewModel.SendToExecutionTargetsCommand.Execute(null);

        _hostTargetingService.Received(1).AddFromAdSearchResults(
            Arg.Is<IEnumerable<AdObject>>(objs => objs.Count() == 1));
        _viewModel.ResultStatus.Should().Contain("Sent 1 computers");
    }

    [Fact]
    public void SendToExecutionTargets_NoComputers_ShowsInfo()
    {
        _viewModel.SearchResults.Add(new AdObject
        {
            Name = "User1",
            DistinguishedName = "CN=User1,DC=test,DC=local",
            ObjectClass = "user"
        });

        _viewModel.SendToExecutionTargetsCommand.Execute(null);

        _dialogService.Received(1).ShowInfo("No Computers", Arg.Any<string>());
    }

    [Fact]
    public async Task RefreshAsync_ReloadsTreeRoot()
    {
        _adService.BrowseChildrenAsync("DC=test,DC=local", Arg.Any<CancellationToken>())
            .Returns([]);

        await _viewModel.RefreshAsync();

        await _adService.Received(1).BrowseChildrenAsync("DC=test,DC=local", Arg.Any<CancellationToken>());
    }

    [Fact]
    public void Constructor_ThrowsOnNullDependencies()
    {
        Action act = () => _ = new AdExplorerViewModel(null!, _hostTargetingService, _settingsService, _dialogService, _logger);

        act.Should().Throw<ArgumentNullException>().And.ParamName.Should().Be("adService");
    }
}
