using FluentAssertions;
using NSubstitute;
using Serilog;
using SysOpsCommander.Core.Interfaces;
using SysOpsCommander.Core.Models;
using SysOpsCommander.ViewModels;

namespace SysOpsCommander.Tests.ViewModels;

public sealed class DashboardViewModelTests : IDisposable
{
    private readonly IActiveDirectoryService _adService = Substitute.For<IActiveDirectoryService>();
    private readonly IHostTargetingService _hostTargetingService = Substitute.For<IHostTargetingService>();
    private readonly IAuditLogService _auditLogService = Substitute.For<IAuditLogService>();
    private readonly ILogger _logger = Substitute.For<ILogger>();
    private readonly DashboardViewModel _viewModel;

    public DashboardViewModelTests()
    {
        _adService.GetActiveDomain().Returns(new DomainConnection
        {
            DomainName = "test.local",
            RootDistinguishedName = "DC=test,DC=local"
        });

        _viewModel = new DashboardViewModel(
            _adService,
            _hostTargetingService,
            _auditLogService,
            _logger);
    }

    public void Dispose() => _viewModel.Dispose();

    [Fact]
    public async Task QuickConnect_ValidHostname_AddsToHostTargeting()
    {
        _viewModel.QuickConnectHostname = "server01";

        await _viewModel.QuickConnectCommand.ExecuteAsync(null);

        _hostTargetingService.Received(1).AddFromHostnames(
            Arg.Is<IEnumerable<string>>(h => h.Contains("server01")));
        _viewModel.QuickConnectResult.Should().Contain("server01");
    }

    [Fact]
    public async Task QuickConnect_InvalidHostname_ShowsError()
    {
        _viewModel.QuickConnectHostname = "invalid host!@#";

        await _viewModel.QuickConnectCommand.ExecuteAsync(null);

        _hostTargetingService.DidNotReceive().AddFromHostnames(Arg.Any<IEnumerable<string>>());
        _viewModel.QuickConnectResult.Should().NotBeEmpty();
    }

    [Fact]
    public async Task QuickConnect_EmptyHostname_ShowsMessage()
    {
        _viewModel.QuickConnectHostname = "";

        await _viewModel.QuickConnectCommand.ExecuteAsync(null);

        _hostTargetingService.DidNotReceive().AddFromHostnames(Arg.Any<IEnumerable<string>>());
        _viewModel.QuickConnectResult.Should().NotBeEmpty();
    }

    [Fact]
    public async Task LoadRecentExecutions_PopulatesCollection()
    {
        List<AuditLogEntry> entries =
        [
            new()
            {
                Id = 1,
                Timestamp = DateTime.UtcNow,
                UserName = "admin",
                MachineName = "WS01",
                ScriptName = "Test.ps1",
                TargetHosts = "server01",
                TargetHostCount = 1,
                SuccessCount = 1,
                FailureCount = 0,
                Status = Core.Enums.ExecutionStatus.Completed,
                Duration = TimeSpan.FromSeconds(5),
                CorrelationId = Guid.NewGuid()
            }
        ];
        _auditLogService.QueryAsync(Arg.Any<AuditLogFilter>(), Arg.Any<CancellationToken>())
            .Returns(entries);

        await _viewModel.LoadRecentExecutionsCommand.ExecuteAsync(null);

        _viewModel.RecentExecutions.Should().HaveCount(1);
        _viewModel.RecentExecutions[0].ScriptName.Should().Be("Test.ps1");
    }

    [Fact]
    public async Task RefreshAsync_LoadsDomainAndRecentExecutions()
    {
        _auditLogService.QueryAsync(Arg.Any<AuditLogFilter>(), Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<AuditLogEntry>)[]);

        await _viewModel.RefreshAsync();

        _viewModel.ActiveDomainName.Should().Be("test.local");
        await _auditLogService.Received(1).QueryAsync(
            Arg.Any<AuditLogFilter>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public void Constructor_SetsCurrentUserName() =>
        _viewModel.CurrentUserName.Should().Be(Environment.UserName);

    [Fact]
    public void Constructor_ThrowsOnNullDependencies()
    {
        Action act = () => _ = new DashboardViewModel(null!, _hostTargetingService, _auditLogService, _logger);

        act.Should().Throw<ArgumentNullException>().And.ParamName.Should().Be("adService");
    }
}
