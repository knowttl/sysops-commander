using FluentAssertions;
using NSubstitute;
using Serilog;
using SysOpsCommander.Core.Constants;
using SysOpsCommander.Core.Enums;
using SysOpsCommander.Core.Interfaces;
using SysOpsCommander.ViewModels;

namespace SysOpsCommander.Tests.ViewModels;

public sealed class SettingsViewModelTests : IDisposable
{
    private readonly ISettingsService _settingsService = Substitute.For<ISettingsService>();
    private readonly ILogger _logger = Substitute.For<ILogger>();
    private readonly SettingsViewModel _viewModel;

    public SettingsViewModelTests()
    {
        _settingsService.GetOrgDefault(Arg.Any<string>()).Returns(string.Empty);
        _settingsService.GetEffectiveAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(string.Empty);
        _settingsService.GetAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(string.Empty);
        _settingsService.GetTypedAsync("StaleComputerThresholdDays", AppConstants.DefaultStaleComputerDays, Arg.Any<CancellationToken>())
            .Returns(AppConstants.DefaultStaleComputerDays);
        _settingsService.GetTypedAsync("DefaultThrottle", AppConstants.DefaultThrottle, Arg.Any<CancellationToken>())
            .Returns(AppConstants.DefaultThrottle);
        _settingsService.GetTypedAsync("DefaultTimeoutSeconds", AppConstants.DefaultWinRmTimeoutSeconds, Arg.Any<CancellationToken>())
            .Returns(AppConstants.DefaultWinRmTimeoutSeconds);

        _viewModel = new SettingsViewModel(_settingsService, _logger);
    }

    public void Dispose() => _viewModel.Dispose();

    [Fact]
    public async Task LoadSettings_PopulatesAllProperties()
    {
        _settingsService.GetOrgDefault("DefaultDomain").Returns("org.corp.local");
        _settingsService.GetOrgDefault("SharedScriptRepositoryPath").Returns(@"\\server\scripts");
        _settingsService.GetEffectiveAsync("DefaultDomain", Arg.Any<CancellationToken>())
            .Returns("user.corp.local");
        _settingsService.GetEffectiveAsync("DefaultWinRmAuthMethod", Arg.Any<CancellationToken>())
            .Returns("NTLM");
        _settingsService.GetEffectiveAsync("DefaultWinRmTransport", Arg.Any<CancellationToken>())
            .Returns("HTTPS");

        await _viewModel.LoadSettingsCommand.ExecuteAsync(null);

        _viewModel.OrgDefaultDomain.Should().Be("org.corp.local");
        _viewModel.OrgScriptRepositoryPath.Should().Be(@"\\server\scripts");
        _viewModel.DefaultDomain.Should().Be("user.corp.local");
        _viewModel.DefaultAuthMethod.Should().Be(WinRmAuthMethod.NTLM);
        _viewModel.DefaultTransport.Should().Be(WinRmTransport.HTTPS);
        _viewModel.DefaultThrottle.Should().Be(AppConstants.DefaultThrottle);
        _viewModel.DefaultTimeoutSeconds.Should().Be(AppConstants.DefaultWinRmTimeoutSeconds);
    }

    [Fact]
    public async Task LoadSettings_ShowsOrgDefaults()
    {
        _settingsService.GetOrgDefault("DefaultDomain").Returns("corp.contoso.com");
        _settingsService.GetOrgDefault("SharedScriptRepositoryPath").Returns(@"\\fileserver\scripts");

        await _viewModel.LoadSettingsCommand.ExecuteAsync(null);

        _viewModel.OrgDefaultDomain.Should().Be("corp.contoso.com");
        _viewModel.OrgScriptRepositoryPath.Should().Be(@"\\fileserver\scripts");
    }

    [Fact]
    public async Task SaveSettings_PersistsAllSettings()
    {
        await _viewModel.LoadSettingsCommand.ExecuteAsync(null);

        _viewModel.DefaultDomain = "new.domain.com";
        _viewModel.DefaultAuthMethod = WinRmAuthMethod.CredSSP;
        _viewModel.DefaultTransport = WinRmTransport.HTTPS;
        _viewModel.StaleComputerThresholdDays = 120;
        _viewModel.DefaultThrottle = 10;
        _viewModel.DefaultTimeoutSeconds = 120;
        _viewModel.LogLevel = "Debug";

        await _viewModel.SaveSettingsCommand.ExecuteAsync(null);

        await _settingsService.Received().SetAsync("DefaultDomain", "new.domain.com", Arg.Any<CancellationToken>());
        await _settingsService.Received().SetAsync("DefaultWinRmAuthMethod", "CredSSP", Arg.Any<CancellationToken>());
        await _settingsService.Received().SetAsync("DefaultWinRmTransport", "HTTPS", Arg.Any<CancellationToken>());
        await _settingsService.Received().SetAsync("StaleComputerThresholdDays", "120", Arg.Any<CancellationToken>());
        await _settingsService.Received().SetAsync("DefaultThrottle", "10", Arg.Any<CancellationToken>());
        await _settingsService.Received().SetAsync("DefaultTimeoutSeconds", "120", Arg.Any<CancellationToken>());
        await _settingsService.Received().SetAsync("LogLevel", "Debug", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SaveSettings_ClearsUnsavedFlag()
    {
        await _viewModel.LoadSettingsCommand.ExecuteAsync(null);

        _viewModel.DefaultDomain = "changed.domain";
        _viewModel.HasUnsavedChanges.Should().BeTrue();

        await _viewModel.SaveSettingsCommand.ExecuteAsync(null);

        _viewModel.HasUnsavedChanges.Should().BeFalse();
    }

    [Fact]
    public async Task PropertyChange_SetsUnsavedFlag()
    {
        await _viewModel.LoadSettingsCommand.ExecuteAsync(null);
        _viewModel.HasUnsavedChanges.Should().BeFalse();

        _viewModel.DefaultThrottle = 15;

        _viewModel.HasUnsavedChanges.Should().BeTrue();
    }

    [Fact]
    public async Task LoadSettings_ClearsUnsavedFlag()
    {
        await _viewModel.LoadSettingsCommand.ExecuteAsync(null);
        _viewModel.DefaultDomain = "changed";
        _viewModel.HasUnsavedChanges.Should().BeTrue();

        await _viewModel.LoadSettingsCommand.ExecuteAsync(null);

        _viewModel.HasUnsavedChanges.Should().BeFalse();
    }

    [Fact]
    public async Task ResetSettings_ReloadsFromService()
    {
        await _viewModel.LoadSettingsCommand.ExecuteAsync(null);
        _viewModel.DefaultDomain = "changed";

        await _viewModel.ResetSettingsCommand.ExecuteAsync(null);

        await _settingsService.Received(2).GetEffectiveAsync("DefaultDomain", Arg.Any<CancellationToken>());
        _viewModel.StatusMessage.Should().Contain("reset");
    }

    [Fact]
    public async Task SaveSettings_WithUserScriptPath_PersistsPath()
    {
        await _viewModel.LoadSettingsCommand.ExecuteAsync(null);

        _viewModel.HasUserScriptPathOverride = true;
        _viewModel.UserScriptRepositoryPath = @"C:\MyScripts";

        await _viewModel.SaveSettingsCommand.ExecuteAsync(null);

        await _settingsService.Received().SetAsync("UserScriptRepositoryPath", @"C:\MyScripts", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SaveSettings_WithoutUserScriptPath_ClearsPath()
    {
        await _viewModel.LoadSettingsCommand.ExecuteAsync(null);

        _viewModel.HasUserScriptPathOverride = false;

        await _viewModel.SaveSettingsCommand.ExecuteAsync(null);

        await _settingsService.Received().SetAsync("UserScriptRepositoryPath", string.Empty, Arg.Any<CancellationToken>());
    }
}
