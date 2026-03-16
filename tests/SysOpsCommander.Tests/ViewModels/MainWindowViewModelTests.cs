using FluentAssertions;
using FluentAssertions.Events;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Serilog;
using SysOpsCommander.Core.Interfaces;
using SysOpsCommander.Core.Models;
using SysOpsCommander.ViewModels;

namespace SysOpsCommander.Tests.ViewModels;

public sealed class MainWindowViewModelTests
{
    private readonly IActiveDirectoryService _adService = Substitute.For<IActiveDirectoryService>();
    private readonly IDialogService _dialogService = Substitute.For<IDialogService>();
    private readonly IAutoUpdateService _autoUpdateService = Substitute.For<IAutoUpdateService>();
    private readonly IServiceProvider _serviceProvider;
    private readonly MainWindowViewModel _viewModel;

    public MainWindowViewModelTests()
    {
        var services = new ServiceCollection();

        services.AddSingleton(_adService);
        services.AddSingleton(Substitute.For<IHostTargetingService>());
        services.AddSingleton(Substitute.For<IAuditLogService>());
        services.AddSingleton(Substitute.For<ISettingsService>());
        services.AddSingleton(Substitute.For<IRemoteExecutionService>());
        services.AddSingleton(Substitute.For<IScriptLoaderService>());
        services.AddSingleton(Substitute.For<IExportService>());
        services.AddSingleton(_dialogService);
        services.AddSingleton(_autoUpdateService);
        services.AddSingleton(Substitute.For<ILogger>());

        services.AddTransient<DashboardViewModel>();
        services.AddTransient<AdExplorerViewModel>();
        services.AddTransient<ExecutionViewModel>();
        services.AddTransient<ScriptLibraryViewModel>();
        services.AddTransient<AuditLogViewModel>();
        services.AddTransient<SettingsViewModel>();

        _serviceProvider = services.BuildServiceProvider();

        _adService.GetActiveDomain().Returns(new DomainConnection
        {
            DomainName = "test.local",
            RootDistinguishedName = "DC=test,DC=local"
        });
        _adService.GetAvailableDomainsAsync(Arg.Any<CancellationToken>())
            .Returns([new DomainConnection
            {
                DomainName = "test.local",
                RootDistinguishedName = "DC=test,DC=local"
            }]);

        _viewModel = new MainWindowViewModel(_serviceProvider, _adService, _dialogService, _autoUpdateService);
    }

    [Fact]
    public void NavigateToDashboard_SetsCurrentView_ToDashboardVm()
    {
        _viewModel.NavigateCommand.Execute("Dashboard");

        _viewModel.CurrentView.Should().BeOfType<DashboardViewModel>();
    }

    [Fact]
    public void NavigateToAdExplorer_SetsCurrentView_ToAdExplorerVm()
    {
        _viewModel.NavigateCommand.Execute("ADExplorer");

        _viewModel.CurrentView.Should().BeOfType<AdExplorerViewModel>();
    }

    [Fact]
    public void NavigateToExecution_SetsCurrentView_ToExecutionVm()
    {
        _viewModel.NavigateCommand.Execute("Execution");

        _viewModel.CurrentView.Should().BeOfType<ExecutionViewModel>();
    }

    [Fact]
    public void NavigateToScriptLibrary_SetsCurrentView_ToScriptLibraryVm()
    {
        _viewModel.NavigateCommand.Execute("ScriptLibrary");

        _viewModel.CurrentView.Should().BeOfType<ScriptLibraryViewModel>();
    }

    [Fact]
    public void NavigateToAuditLog_SetsCurrentView_ToAuditLogVm()
    {
        _viewModel.NavigateCommand.Execute("AuditLog");

        _viewModel.CurrentView.Should().BeOfType<AuditLogViewModel>();
    }

    [Fact]
    public void NavigateToSettings_SetsCurrentView_ToSettingsVm()
    {
        _viewModel.NavigateCommand.Execute("Settings");

        _viewModel.CurrentView.Should().BeOfType<SettingsViewModel>();
    }

    [Fact]
    public async Task Initialize_SetsCurrentUser()
    {
        await _viewModel.InitializeAsync();

        _viewModel.CurrentUserName.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Initialize_DetectsCurrentDomain()
    {
        await _viewModel.InitializeAsync();

        _viewModel.CurrentDomainName.Should().Be("test.local");
        _viewModel.ConnectionStatus.Should().Be("Connected");
    }

    [Fact]
    public async Task Initialize_WhenDomainDetectionFails_SetsDisconnected()
    {
        _adService.GetActiveDomain().Throws(new InvalidOperationException("No domain"));

        await _viewModel.InitializeAsync();

        _viewModel.CurrentDomainName.Should().Be(Environment.UserDomainName);
        _viewModel.ConnectionStatus.Should().Be("Disconnected");
    }

    [Fact]
    public async Task Initialize_NavigatesToDashboard()
    {
        await _viewModel.InitializeAsync();

        _viewModel.CurrentView.Should().BeOfType<DashboardViewModel>();
    }

    [Fact]
    public async Task Initialize_LoadsAvailableDomains()
    {
        await _viewModel.InitializeAsync();

        _viewModel.AvailableDomains.Should().HaveCount(1);
        _viewModel.AvailableDomains[0].DomainName.Should().Be("test.local");
    }

    [Fact]
    public void PropertyChanged_FiredForNavigation()
    {
        using IMonitor<MainWindowViewModel> monitor = _viewModel.Monitor();

        _viewModel.NavigateCommand.Execute("Dashboard");

        monitor.Should().RaisePropertyChangeFor(vm => vm.CurrentView);
    }

    [Fact]
    public async Task DomainSwitch_UpdatesCurrentDomainName()
    {
        DomainConnection newDomain = new()
        {
            DomainName = "new.domain.com",
            RootDistinguishedName = "DC=new,DC=domain,DC=com"
        };

        await _viewModel.InitializeAsync();
        _viewModel.SelectedDomain = newDomain;

        // Allow the async switch to complete
        await Task.Delay(100);

        _viewModel.CurrentDomainName.Should().Be("new.domain.com");
        _viewModel.ConnectionStatus.Should().Be("Connected");
        _viewModel.SelectedDomain!.DomainName.Should().Be("new.domain.com");
    }

    [Fact]
    public async Task DomainSwitch_WhenFails_ShowsError()
    {
        _adService.SetActiveDomainAsync(Arg.Any<DomainConnection>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Connection failed"));

        DomainConnection failDomain = new()
        {
            DomainName = "fail.domain.com",
            RootDistinguishedName = "DC=fail,DC=domain,DC=com"
        };

        await _viewModel.InitializeAsync();
        _viewModel.SelectedDomain = failDomain;

        await Task.Delay(100);

        _viewModel.ConnectionStatus.Should().Be("Connected");
        _dialogService.Received().ShowError(Arg.Any<string>(), Arg.Is<string>(s => s.Contains("fail.domain.com")));
    }

    [Fact]
    public async Task DomainSwitch_WhenTimesOut_ShowsTimeoutError()
    {
        _adService.SetActiveDomainAsync(Arg.Any<DomainConnection>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new OperationCanceledException());

        DomainConnection slowDomain = new()
        {
            DomainName = "slow.domain.com",
            RootDistinguishedName = "DC=slow,DC=domain,DC=com"
        };

        await _viewModel.InitializeAsync();
        _viewModel.SelectedDomain = slowDomain;

        await Task.Delay(100);

        _viewModel.CurrentDomainName.Should().Be("test.local");
        _viewModel.ConnectionStatus.Should().Be("Connected");
        _viewModel.IsBusy.Should().BeFalse();
        _dialogService.Received().ShowError(
            Arg.Is<string>(s => s.Contains("Timed Out")),
            Arg.Is<string>(s => s.Contains("slow.domain.com")));
    }

    [Fact]
    public async Task DomainSwitch_WhenFails_RestoresPreviousDomain()
    {
        await _viewModel.InitializeAsync();

        DomainConnection newDomain = new()
        {
            DomainName = "new.domain.com",
            RootDistinguishedName = "DC=new,DC=domain,DC=com"
        };

        _viewModel.SelectedDomain = newDomain;
        await Task.Delay(100);

        _viewModel.CurrentDomainName.Should().Be("new.domain.com");

        _adService.SetActiveDomainAsync(Arg.Any<DomainConnection>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Failed"));

        _adService.GetActiveDomain().Returns(newDomain);

        DomainConnection badDomain = new()
        {
            DomainName = "bad.domain.com",
            RootDistinguishedName = "DC=bad,DC=domain,DC=com"
        };

        _viewModel.SelectedDomain = badDomain;
        await Task.Delay(100);

        _viewModel.CurrentDomainName.Should().Be("new.domain.com");
        _viewModel.SelectedDomain.Should().Be(newDomain);
        _viewModel.IsBusy.Should().BeFalse();
    }

    [Fact]
    public async Task DomainSwitch_ClearsBusyState_AfterSuccess()
    {
        DomainConnection newDomain = new()
        {
            DomainName = "new.domain.com",
            RootDistinguishedName = "DC=new,DC=domain,DC=com"
        };

        await _viewModel.InitializeAsync();
        _viewModel.SelectedDomain = newDomain;

        await Task.Delay(100);

        _viewModel.IsBusy.Should().BeFalse();
        _viewModel.BusyMessage.Should().BeEmpty();
    }

    [Fact]
    public async Task OpenDomainSelector_WhenDomainSelected_SwitchesDomain()
    {
        DomainConnection selectedDomain = new()
        {
            DomainName = "selected.domain.com",
            RootDistinguishedName = "DC=selected,DC=domain,DC=com"
        };
        _dialogService.ShowDomainSelectorAsync().Returns(selectedDomain);

        await _viewModel.InitializeAsync();
        await _viewModel.OpenDomainSelectorCommand.ExecuteAsync(null);

        _viewModel.CurrentDomainName.Should().Be("selected.domain.com");
        _viewModel.SelectedDomain!.DomainName.Should().Be("selected.domain.com");
        _viewModel.AvailableDomains.Should().Contain(d => d.DomainName == "selected.domain.com");
    }

    [Fact]
    public async Task OpenDomainSelector_OriginalDomainRemainsInDropdown()
    {
        DomainConnection selectedDomain = new()
        {
            DomainName = "other.domain.com",
            RootDistinguishedName = "DC=other,DC=domain,DC=com"
        };
        _dialogService.ShowDomainSelectorAsync().Returns(selectedDomain);

        await _viewModel.InitializeAsync();
        await _viewModel.OpenDomainSelectorCommand.ExecuteAsync(null);

        _viewModel.AvailableDomains.Should().Contain(d => d.DomainName == "test.local");
        _viewModel.AvailableDomains.Should().Contain(d => d.DomainName == "other.domain.com");
    }

    [Fact]
    public async Task OpenDomainSelector_WhenCancelled_DoesNotChangeDomain()
    {
        _dialogService.ShowDomainSelectorAsync().Returns((DomainConnection?)null);

        await _viewModel.InitializeAsync();
        string originalDomain = _viewModel.CurrentDomainName;

        await _viewModel.OpenDomainSelectorCommand.ExecuteAsync(null);

        _viewModel.CurrentDomainName.Should().Be(originalDomain);
    }

    [Fact]
    public async Task RefreshCurrentView_WhenRefreshable_CallsRefresh()
    {
        IRefreshable refreshable = Substitute.For<IRefreshable>();
        _viewModel.CurrentView = refreshable;

        // CurrentView setter auto-calls RefreshAsync once, then the explicit command calls it again
        await _viewModel.RefreshCurrentViewCommand.ExecuteAsync(null);

        await refreshable.Received(2).RefreshAsync();
    }

    [Fact]
    public async Task RefreshCurrentView_WhenNotRefreshable_DoesNotThrow()
    {
        _viewModel.NavigateCommand.Execute("Dashboard");

        Func<Task> act = async () => await _viewModel.RefreshCurrentViewCommand.ExecuteAsync(null);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public void Constructor_ThrowsOnNullServiceProvider()
    {
        Action act = () => _ = new MainWindowViewModel(null!, _adService, _dialogService, _autoUpdateService);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_ThrowsOnNullAdService()
    {
        Action act = () => _ = new MainWindowViewModel(_serviceProvider, null!, _dialogService, _autoUpdateService);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_ThrowsOnNullDialogService()
    {
        Action act = () => _ = new MainWindowViewModel(_serviceProvider, _adService, null!, _autoUpdateService);

        act.Should().Throw<ArgumentNullException>();
    }
}
