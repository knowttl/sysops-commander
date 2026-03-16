using FluentAssertions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using SysOpsCommander.Core.Interfaces;
using SysOpsCommander.Core.Models;
using SysOpsCommander.ViewModels.Dialogs;

namespace SysOpsCommander.Tests.ViewModels.Dialogs;

public sealed class DomainSelectorViewModelTests
{
    private readonly IActiveDirectoryService _adService = Substitute.For<IActiveDirectoryService>();
    private readonly DomainSelectorViewModel _viewModel;

    public DomainSelectorViewModelTests()
    {
        _viewModel = new DomainSelectorViewModel(_adService);
    }

    [Fact]
    public void Constructor_ThrowsOnNullAdService()
    {
        Action act = () => _ = new DomainSelectorViewModel(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task TestConnectionAsync_WhenDomainNameEmpty_SetsFailureStatus()
    {
        _viewModel.DomainName = "";

        await _viewModel.TestConnectionCommand.ExecuteAsync(null);

        _viewModel.TestStatus.Should().Contain("required");
        _viewModel.IsTestSuccessful.Should().BeFalse();
    }

    [Fact]
    public async Task TestConnectionAsync_WhenSucceeds_SetsSuccessStatus()
    {
        _viewModel.DomainName = "corp.contoso.com";

        await _viewModel.TestConnectionCommand.ExecuteAsync(null);

        _viewModel.TestStatus.Should().Contain("Connected successfully");
        _viewModel.IsTestSuccessful.Should().BeTrue();
        await _adService.Received(1).SetActiveDomainAsync(
            Arg.Is<DomainConnection>(d => d.DomainName == "corp.contoso.com"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task TestConnectionAsync_WhenFails_SetsFailureStatus()
    {
        _adService.SetActiveDomainAsync(Arg.Any<DomainConnection>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("LDAP bind failed"));

        _viewModel.DomainName = "bad.domain.com";

        await _viewModel.TestConnectionCommand.ExecuteAsync(null);

        _viewModel.TestStatus.Should().Contain("Failed");
        _viewModel.TestStatus.Should().Contain("LDAP bind failed");
        _viewModel.IsTestSuccessful.Should().BeFalse();
    }

    [Fact]
    public async Task TestConnectionAsync_WhenTimesOut_SetsFailureStatus()
    {
        _adService.SetActiveDomainAsync(Arg.Any<DomainConnection>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new OperationCanceledException());

        _viewModel.DomainName = "slow.domain.com";

        await _viewModel.TestConnectionCommand.ExecuteAsync(null);

        _viewModel.TestStatus.Should().Contain("Failed");
        _viewModel.IsTestSuccessful.Should().BeFalse();
    }

    [Fact]
    public async Task TestConnectionAsync_PassesCancellationToken()
    {
        _viewModel.DomainName = "test.local";

        await _viewModel.TestConnectionCommand.ExecuteAsync(null);

        await _adService.Received(1).SetActiveDomainAsync(
            Arg.Any<DomainConnection>(),
            Arg.Is<CancellationToken>(ct => ct.CanBeCanceled));
    }

    [Fact]
    public async Task TestConnectionAsync_WithDomainControllerFqdn_IncludesInConnection()
    {
        _viewModel.DomainName = "corp.contoso.com";
        _viewModel.DomainControllerFqdn = "dc01.corp.contoso.com";

        await _viewModel.TestConnectionCommand.ExecuteAsync(null);

        await _adService.Received(1).SetActiveDomainAsync(
            Arg.Is<DomainConnection>(d => d.DomainControllerFqdn == "dc01.corp.contoso.com"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public void Result_WhenNotConfirmed_ReturnsNull()
    {
        _viewModel.DomainName = "test.local";

        _viewModel.Result.Should().BeNull();
    }

    [Fact]
    public async Task Result_WhenConfirmedAndTestSucceeded_ReturnsDomainConnection()
    {
        _viewModel.DomainName = "corp.contoso.com";
        await _viewModel.TestConnectionCommand.ExecuteAsync(null);
        _viewModel.ConfirmCommand.Execute(null);

        DomainConnection? result = _viewModel.Result;

        result.Should().NotBeNull();
        result!.DomainName.Should().Be("corp.contoso.com");
        result.RootDistinguishedName.Should().Be("DC=corp,DC=contoso,DC=com");
        result.IsCurrentDomain.Should().BeFalse();
    }

    [Fact]
    public async Task Result_WhenConfirmedButTestFailed_ReturnsNull()
    {
        _adService.SetActiveDomainAsync(Arg.Any<DomainConnection>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Failed"));

        _viewModel.DomainName = "bad.domain.com";
        await _viewModel.TestConnectionCommand.ExecuteAsync(null);
        _viewModel.ConfirmCommand.Execute(null);

        _viewModel.Result.Should().BeNull();
    }
}
