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
    private readonly IExportService _exportService = Substitute.For<IExportService>();
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
            _exportService,
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
        _viewModel.TreeNodes[0].Name.Should().Be("Computers");
        _viewModel.TreeNodes[1].Name.Should().Be("Users");
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
            new() { Name = "Admins OU", DistinguishedName = "OU=Admins,CN=Users,DC=test,DC=local", ObjectClass = "organizationalUnit" }
        ];
        _adService.BrowseChildrenAsync("CN=Users,DC=test,DC=local", Arg.Any<CancellationToken>())
            .Returns(children);

        await _viewModel.ExpandNodeCommand.ExecuteAsync(node);

        node.Children.Should().HaveCount(1);
        node.Children[0].Name.Should().Be("Admins OU");
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
        Action act = () => _ = new AdExplorerViewModel(null!, _hostTargetingService, _settingsService, _dialogService, _exportService, _logger);

        act.Should().Throw<ArgumentNullException>().And.ParamName.Should().Be("adService");
    }

    [Fact]
    public async Task Search_RecordsSearchHistory()
    {
        var result = new AdSearchResult
        {
            Query = "testuser",
            TotalResultCount = 3,
            HasMoreResults = false,
            ExecutionTime = TimeSpan.FromMilliseconds(20),
            Results =
            [
                new AdObject { Name = "User1", DistinguishedName = "CN=User1,DC=test,DC=local", ObjectClass = "user" },
                new AdObject { Name = "User2", DistinguishedName = "CN=User2,DC=test,DC=local", ObjectClass = "user" },
                new AdObject { Name = "User3", DistinguishedName = "CN=User3,DC=test,DC=local", ObjectClass = "user" }
            ]
        };
        _adService.SearchScopedAsync("testuser", Arg.Any<string?>(), Arg.Any<IReadOnlyList<string>?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(result);

        _viewModel.SearchText = "testuser";
        await _viewModel.SearchCommand.ExecuteAsync(null);

        _viewModel.SearchHistory.Should().HaveCount(1);
        _viewModel.SearchHistory[0].QueryText.Should().Be("testuser");
        _viewModel.SearchHistory[0].ResultCount.Should().Be(3);
    }

    [Fact]
    public async Task SearchHistory_TrimmedToMaxCount()
    {
        var result = new AdSearchResult
        {
            Query = "q",
            TotalResultCount = 1,
            HasMoreResults = false,
            ExecutionTime = TimeSpan.FromMilliseconds(5),
            Results = [new AdObject { Name = "Obj", DistinguishedName = "CN=Obj,DC=test,DC=local", ObjectClass = "user" }]
        };
        _adService.SearchScopedAsync(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<IReadOnlyList<string>?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(result);

        for (int i = 0; i < 30; i++)
        {
            _viewModel.SearchText = $"query{i}";
            await _viewModel.SearchCommand.ExecuteAsync(null);
        }

        _viewModel.SearchHistory.Should().HaveCount(AppConstants.MaxSearchHistoryCount);
    }

    [Fact]
    public void ToggleSearchHistory_TogglesIsSearchHistoryOpen()
    {
        _viewModel.IsSearchHistoryOpen.Should().BeFalse();

        _viewModel.ToggleSearchHistoryCommand.Execute(null);

        _viewModel.IsSearchHistoryOpen.Should().BeTrue();
    }

    [Fact]
    public async Task ClearSearchHistory_ClearsCollection()
    {
        var result = new AdSearchResult
        {
            Query = "q",
            TotalResultCount = 1,
            HasMoreResults = false,
            ExecutionTime = TimeSpan.FromMilliseconds(5),
            Results = [new AdObject { Name = "Obj", DistinguishedName = "CN=Obj,DC=test,DC=local", ObjectClass = "user" }]
        };
        _adService.SearchScopedAsync("q", Arg.Any<string?>(), Arg.Any<IReadOnlyList<string>?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(result);

        _viewModel.SearchText = "q";
        await _viewModel.SearchCommand.ExecuteAsync(null);
        _viewModel.SearchHistory.Should().NotBeEmpty();

        await _viewModel.ClearSearchHistoryCommand.ExecuteAsync(null);

        _viewModel.SearchHistory.Should().BeEmpty();
    }

    [Fact]
    public async Task SaveCurrentSearch_PromptsThenAddsToSavedSearches()
    {
        _dialogService.ShowInputDialogAsync("Save Search", Arg.Any<string>(), Arg.Any<string>())
            .Returns("My saved search");

        _viewModel.SearchText = "admins";

        await _viewModel.SaveCurrentSearchCommand.ExecuteAsync(null);

        _viewModel.SavedSearches.Should().HaveCount(1);
        _viewModel.SavedSearches[0].Name.Should().Be("My saved search");
        _viewModel.SavedSearches[0].QueryText.Should().Be("admins");
    }

    [Fact]
    public async Task SaveCurrentSearch_EmptyQuery_ShowsInfo()
    {
        _viewModel.SearchText = "";
        await _viewModel.SaveCurrentSearchCommand.ExecuteAsync(null);

        _dialogService.Received(1).ShowInfo("Nothing to Save", Arg.Any<string>());
        _viewModel.SavedSearches.Should().BeEmpty();
    }

    [Fact]
    public async Task DeleteSavedSearch_RemovesFromCollection()
    {
        _dialogService.ShowInputDialogAsync("Save Search", Arg.Any<string>(), Arg.Any<string>())
            .Returns("test");
        _viewModel.SearchText = "admins";
        await _viewModel.SaveCurrentSearchCommand.ExecuteAsync(null);
        _viewModel.SavedSearches.Should().HaveCount(1);

        SavedSearch saved = _viewModel.SavedSearches[0];
        await _viewModel.DeleteSavedSearchCommand.ExecuteAsync(saved);

        _viewModel.SavedSearches.Should().BeEmpty();
    }

    [Fact]
    public async Task RenameSavedSearch_UpdatesName()
    {
        _dialogService.ShowInputDialogAsync("Save Search", Arg.Any<string>(), Arg.Any<string>())
            .Returns("original");
        _viewModel.SearchText = "test";
        await _viewModel.SaveCurrentSearchCommand.ExecuteAsync(null);

        _dialogService.ShowInputDialogAsync("Rename Search", Arg.Any<string>(), "original")
            .Returns("renamed");

        await _viewModel.RenameSavedSearchCommand.ExecuteAsync(_viewModel.SavedSearches[0]);

        _viewModel.SavedSearches[0].Name.Should().Be("renamed");
    }

    [Fact]
    public void ToggleLdapFilterMode_PreservesSimpleSearchState()
    {
        _viewModel.SearchText = "myquery";
        _viewModel.SelectedAttribute = "sAMAccountName";
        _viewModel.ToggleFilterCommand.Execute("user");

        _viewModel.ToggleLdapFilterModeCommand.Execute(null);

        _viewModel.IsLdapFilterMode.Should().BeTrue();

        _viewModel.ToggleLdapFilterModeCommand.Execute(null);

        _viewModel.IsLdapFilterMode.Should().BeFalse();
        _viewModel.SearchText.Should().Be("myquery");
        _viewModel.SelectedAttribute.Should().Be("sAMAccountName");
        _viewModel.FilterUsers.Should().BeTrue();
    }

    [Fact]
    public async Task Search_InLdapMode_UsesSearchWithFilterAsync()
    {
        var result = new AdSearchResult
        {
            Query = "(objectClass=user)",
            TotalResultCount = 5,
            HasMoreResults = false,
            ExecutionTime = TimeSpan.FromMilliseconds(10),
            Results =
            [
                new AdObject { Name = "U1", DistinguishedName = "CN=U1,DC=test,DC=local", ObjectClass = "user" }
            ]
        };
        _adService.SearchWithFilterAsync("(objectClass=user)", Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(result);

        _viewModel.ToggleLdapFilterModeCommand.Execute(null);
        _viewModel.RawLdapFilter = "(objectClass=user)";
        await _viewModel.SearchCommand.ExecuteAsync(null);

        await _adService.Received(1).SearchWithFilterAsync("(objectClass=user)", Arg.Any<string?>(), Arg.Any<CancellationToken>());
        _viewModel.SearchResults.Should().HaveCount(1);
    }

    [Fact]
    public async Task ConfirmExport_CallsExportService()
    {
        _viewModel.SearchResults.Add(new AdObject
        {
            Name = "Obj1",
            DistinguishedName = "CN=Obj1,DC=test,DC=local",
            ObjectClass = "user",
            DisplayName = "Object 1"
        });

        _dialogService.ShowSaveFileDialogAsync(".csv", Arg.Any<string>()).Returns("test.csv");

        _viewModel.StartCsvExportCommand.Execute(null);
        await _viewModel.ConfirmExportCommand.ExecuteAsync(null);

        await _exportService.Received(1).ExportAdObjectsToCsvAsync(
            Arg.Any<IEnumerable<AdObject>>(),
            "test.csv",
            Arg.Any<IReadOnlyList<string>>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public void CopyToClipboard_SetsClipboardText()
    {
        _viewModel.SearchResults.Add(new AdObject
        {
            Name = "PC1",
            DistinguishedName = "CN=PC1,DC=test,DC=local",
            ObjectClass = "computer",
            Description = "Test Computer"
        });

        _viewModel.CopyToClipboardCommand.Execute(null);

        _dialogService.Received(1).SetClipboardText(Arg.Is<string>(s => s.Contains("PC1")));
        _viewModel.ResultStatus.Should().Contain("Copied 1 objects");
    }

    [Fact]
    public void CopyToClipboard_EmptyResults_DoesNothing()
    {
        _viewModel.CopyToClipboardCommand.Execute(null);

        _dialogService.DidNotReceive().SetClipboardText(Arg.Any<string>());
    }

    [Fact]
    public void SearchEntireDomain_DefaultsToTrue() =>
        _viewModel.SearchEntireDomain.Should().BeTrue();

    [Fact]
    public void DataGridColumns_InitializedWithFourColumns()
    {
        _viewModel.DataGridColumns.Should().HaveCount(4);
        _viewModel.DataGridColumns[0].Header.Should().Be("Name");
        _viewModel.DataGridColumns[1].Header.Should().Be("Class");
        _viewModel.DataGridColumns[2].Header.Should().Be("Description");
        _viewModel.DataGridColumns[3].Header.Should().Be("Distinguished Name");
    }

    [Fact]
    public void DataGridColumns_AllVisibleByDefault() =>
        _viewModel.DataGridColumns.Should().OnlyContain(c => c.IsVisible);

    [Fact]
    public async Task GoBack_RestoresPreviousSearchState()
    {
        _viewModel.SearchResults.Add(new AdObject
        {
            Name = "Original",
            DistinguishedName = "CN=Original,DC=test,DC=local",
            ObjectClass = "user"
        });
        _viewModel.ResultStatus = "1 results";

        var result = new AdSearchResult
        {
            Query = "new",
            TotalResultCount = 2,
            HasMoreResults = false,
            ExecutionTime = TimeSpan.FromMilliseconds(10),
            Results =
            [
                new AdObject { Name = "New1", DistinguishedName = "CN=New1,DC=test,DC=local", ObjectClass = "user" },
                new AdObject { Name = "New2", DistinguishedName = "CN=New2,DC=test,DC=local", ObjectClass = "user" }
            ]
        };
        _adService.SearchScopedAsync("new", Arg.Any<string?>(), Arg.Any<IReadOnlyList<string>?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(result);

        _viewModel.SearchText = "new";
        await _viewModel.SearchCommand.ExecuteAsync(null);

        _viewModel.SearchResults.Should().HaveCount(2);
        _viewModel.CanGoBack.Should().BeTrue();

        _viewModel.GoBackCommand.Execute(null);

        _viewModel.SearchResults.Should().HaveCount(1);
        _viewModel.SearchResults[0].Name.Should().Be("Original");
    }

    [Fact]
    public void GoBack_EmptyStack_DoesNothing()
    {
        _viewModel.CanGoBack.Should().BeFalse();
        _viewModel.GoBackCommand.Execute(null);
        _viewModel.CanGoBack.Should().BeFalse();
    }

    [Fact]
    public async Task GoBack_LimitedToMaxUndoDepth()
    {
        var result = new AdSearchResult
        {
            Query = "q",
            TotalResultCount = 1,
            HasMoreResults = false,
            ExecutionTime = TimeSpan.FromMilliseconds(5),
            Results = [new AdObject { Name = "Obj", DistinguishedName = "CN=Obj,DC=test,DC=local", ObjectClass = "user" }]
        };
        _adService.SearchScopedAsync(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<IReadOnlyList<string>?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(result);

        for (int i = 0; i < 8; i++)
        {
            _viewModel.SearchText = $"query{i}";
            await _viewModel.SearchCommand.ExecuteAsync(null);
        }

        int backCount = 0;
        while (_viewModel.CanGoBack)
        {
            _viewModel.GoBackCommand.Execute(null);
            backCount++;
        }

        backCount.Should().BeLessThanOrEqualTo(AppConstants.MaxSearchUndoDepth);
    }

    [Fact]
    public void GroupFilterText_FiltersGroups()
    {
        _viewModel.SelectedObjectGroups = new System.Collections.ObjectModel.ObservableCollection<string>(
            ["Domain Admins", "Enterprise Admins", "Domain Users", "Backup Operators"]);

        _viewModel.GroupFilterText = "Admin";

        _viewModel.FilteredGroups.Should().HaveCount(2);
        _viewModel.FilteredGroups.Should().Contain("Domain Admins");
        _viewModel.FilteredGroups.Should().Contain("Enterprise Admins");
    }

    [Fact]
    public void GroupFilterText_EmptyString_ShowsAll()
    {
        _viewModel.SelectedObjectGroups = new System.Collections.ObjectModel.ObservableCollection<string>(
            ["Group1", "Group2", "Group3"]);

        _viewModel.GroupFilterText = "";

        _viewModel.FilteredGroups.Should().HaveCount(3);
    }

    [Fact]
    public async Task ShowGroupMembersFromList_PopulatesResults()
    {
        var result = new AdSearchResult
        {
            Query = "members",
            TotalResultCount = 2,
            HasMoreResults = false,
            ExecutionTime = TimeSpan.FromMilliseconds(15),
            Results =
            [
                new AdObject { Name = "Member1", DistinguishedName = "CN=Member1,DC=test,DC=local", ObjectClass = "user" },
                new AdObject { Name = "Member2", DistinguishedName = "CN=Member2,DC=test,DC=local", ObjectClass = "user" }
            ]
        };
        _adService.GetGroupMembersAsync("CN=Admins,DC=test,DC=local", true, Arg.Any<CancellationToken>())
            .Returns(result);

        await _viewModel.ShowGroupMembersFromListCommand.ExecuteAsync("CN=Admins,DC=test,DC=local");

        _viewModel.SearchResults.Should().HaveCount(2);
        _viewModel.ResultStatus.Should().Contain("2 members found");
    }

    [Fact]
    public async Task SelectObject_LoadsPermissions()
    {
        List<AdAccessControlEntry> acl =
        [
            new("BUILTIN\\Administrators", "Allow", "Full Control", false, null),
            new("SELF", "Allow", "Read Property", true, "parent")
        ];
        _adService.GetObjectDetailAsync("CN=User1,DC=test,DC=local", Arg.Any<CancellationToken>())
            .Returns(new AdObject { Name = "User1", DistinguishedName = "CN=User1,DC=test,DC=local", ObjectClass = "user" });
        _adService.GetGroupMembershipAsync("CN=User1,DC=test,DC=local", Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns([]);
        _adService.GetObjectAclAsync("CN=User1,DC=test,DC=local", Arg.Any<CancellationToken>())
            .Returns(acl);

        _viewModel.SelectedObject = new AdObject
        {
            Name = "User1",
            DistinguishedName = "CN=User1,DC=test,DC=local",
            ObjectClass = "user"
        };

        await Task.Delay(200);

        _viewModel.SelectedObjectPermissions.Should().HaveCount(2);
        _viewModel.SelectedObjectPermissions[0].Identity.Should().Be("BUILTIN\\Administrators");
        _viewModel.SelectedObjectPermissions[1].IsInherited.Should().BeTrue();
    }

    [Fact]
    public async Task DeselectObject_ClearsPermissions()
    {
        List<AdAccessControlEntry> acl =
        [
            new("BUILTIN\\Administrators", "Allow", "Full Control", false, null)
        ];
        _adService.GetObjectDetailAsync("CN=User1,DC=test,DC=local", Arg.Any<CancellationToken>())
            .Returns(new AdObject { Name = "User1", DistinguishedName = "CN=User1,DC=test,DC=local", ObjectClass = "user" });
        _adService.GetGroupMembershipAsync("CN=User1,DC=test,DC=local", Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns([]);
        _adService.GetObjectAclAsync("CN=User1,DC=test,DC=local", Arg.Any<CancellationToken>())
            .Returns(acl);

        _viewModel.SelectedObject = new AdObject
        {
            Name = "User1",
            DistinguishedName = "CN=User1,DC=test,DC=local",
            ObjectClass = "user"
        };
        await Task.Delay(200);
        _viewModel.SelectedObjectPermissions.Should().NotBeEmpty();

        _viewModel.SelectedObject = null;

        _viewModel.SelectedObjectPermissions.Should().BeEmpty();
    }

    [Fact]
    public async Task SelectObject_AclAccessDenied_ReturnsEmptyPermissions()
    {
        _adService.GetObjectDetailAsync("CN=User1,DC=test,DC=local", Arg.Any<CancellationToken>())
            .Returns(new AdObject { Name = "User1", DistinguishedName = "CN=User1,DC=test,DC=local", ObjectClass = "user" });
        _adService.GetGroupMembershipAsync("CN=User1,DC=test,DC=local", Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns([]);
        _adService.GetObjectAclAsync("CN=User1,DC=test,DC=local", Arg.Any<CancellationToken>())
            .Returns([]);

        _viewModel.SelectedObject = new AdObject
        {
            Name = "User1",
            DistinguishedName = "CN=User1,DC=test,DC=local",
            ObjectClass = "user"
        };

        await Task.Delay(200);

        _viewModel.SelectedObjectPermissions.Should().BeEmpty();
    }

    [Fact]
    public async Task LoadTreeRoot_SortsNodesAlphabetically()
    {
        List<AdObject> children =
        [
            new() { Name = "Zebra OU", DistinguishedName = "OU=Zebra,DC=test,DC=local", ObjectClass = "organizationalUnit" },
            new() { Name = "Alpha OU", DistinguishedName = "OU=Alpha,DC=test,DC=local", ObjectClass = "organizationalUnit" },
            new() { Name = "Middle OU", DistinguishedName = "OU=Middle,DC=test,DC=local", ObjectClass = "organizationalUnit" }
        ];
        _adService.BrowseChildrenAsync("DC=test,DC=local", Arg.Any<CancellationToken>())
            .Returns(children);

        await _viewModel.LoadTreeRootCommand.ExecuteAsync(null);

        _viewModel.TreeNodes.Should().HaveCount(3);
        _viewModel.TreeNodes[0].Name.Should().Be("Alpha OU");
        _viewModel.TreeNodes[1].Name.Should().Be("Middle OU");
        _viewModel.TreeNodes[2].Name.Should().Be("Zebra OU");
    }

    [Fact]
    public async Task TreeFilterText_FiltersTreeNodes()
    {
        List<AdObject> children =
        [
            new() { Name = "Finance", DistinguishedName = "OU=Finance,DC=test,DC=local", ObjectClass = "organizationalUnit" },
            new() { Name = "IT", DistinguishedName = "OU=IT,DC=test,DC=local", ObjectClass = "organizationalUnit" },
            new() { Name = "HR", DistinguishedName = "OU=HR,DC=test,DC=local", ObjectClass = "organizationalUnit" }
        ];
        _adService.BrowseChildrenAsync("DC=test,DC=local", Arg.Any<CancellationToken>())
            .Returns(children);

        await _viewModel.LoadTreeRootCommand.ExecuteAsync(null);

        _viewModel.TreeFilterText = "Fin";

        // Tree filter uses a 200ms debounce — wait for it to apply
        await Task.Delay(350);

        _viewModel.FilteredTreeNodes.Should().HaveCount(1);
        _viewModel.FilteredTreeNodes[0].Name.Should().Be("Finance");
    }

    [Fact]
    public void CopyOuPath_SetsClipboard()
    {
        _viewModel.CopyOuPathCommand.Execute("OU=Finance,DC=test,DC=local");

        _dialogService.Received(1).SetClipboardText("OU=Finance,DC=test,DC=local");
        _viewModel.ResultStatus.Should().Contain("Copied OU path");
    }
}
