using System.Collections.ObjectModel;
using FluentAssertions;
using NSubstitute;
using Serilog;
using SysOpsCommander.Core.Enums;
using SysOpsCommander.Core.Interfaces;
using SysOpsCommander.Core.Models;
using SysOpsCommander.ViewModels;

namespace SysOpsCommander.Tests.ViewModels;

public sealed class ExecutionViewModelTests : IDisposable
{
    private readonly IRemoteExecutionService _executionService = Substitute.For<IRemoteExecutionService>();
    private readonly IHostTargetingService _hostTargetingService = Substitute.For<IHostTargetingService>();
    private readonly IScriptLoaderService _scriptLoaderService = Substitute.For<IScriptLoaderService>();
    private readonly IExportService _exportService = Substitute.For<IExportService>();
    private readonly IDialogService _dialogService = Substitute.For<IDialogService>();
    private readonly ISettingsService _settingsService = Substitute.For<ISettingsService>();
    private readonly ILogger _logger = Substitute.For<ILogger>();
    private readonly ExecutionViewModel _viewModel;

    public ExecutionViewModelTests()
    {
        _hostTargetingService.Targets.Returns([]);

        _settingsService.GetEffectiveAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(string.Empty);

        _scriptLoaderService.LoadAllScriptsAsync(Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<ScriptPlugin>)[]);

        _viewModel = new ExecutionViewModel(
            _executionService,
            _hostTargetingService,
            _scriptLoaderService,
            _exportService,
            _dialogService,
            _settingsService,
            _logger);
    }

    public void Dispose() => _viewModel.Dispose();

    [Fact]
    public async Task InitializeAsync_LoadsScriptsAndSettings()
    {
        List<ScriptPlugin> scripts =
        [
            new()
            {
                FilePath = "C:\\scripts\\Test.ps1",
                FileName = "Test.ps1"
            }
        ];

        _scriptLoaderService.LoadAllScriptsAsync(Arg.Any<CancellationToken>())
            .Returns(scripts);

        _settingsService.GetEffectiveAsync("DefaultWinRmAuthMethod", Arg.Any<CancellationToken>())
            .Returns("NTLM");

        _settingsService.GetEffectiveAsync("DefaultWinRmTransport", Arg.Any<CancellationToken>())
            .Returns("HTTPS");

        await _viewModel.InitializeCommand.ExecuteAsync(null);

        _viewModel.AvailableScripts.Should().HaveCount(1);
        _viewModel.SelectedAuthMethod.Should().Be(WinRmAuthMethod.NTLM);
        _viewModel.SelectedTransport.Should().Be(WinRmTransport.HTTPS);
        _viewModel.StatusMessage.Should().Contain("1 scripts");
    }

    [Fact]
    public void AddHost_ValidHostname_AddsToTargets()
    {
        _viewModel.NewHostname = "server01";

        _viewModel.AddHostCommand.Execute(null);

        _hostTargetingService.Received(1).AddFromHostnames(
            Arg.Is<IEnumerable<string>>(h => h.Contains("server01")));
        _viewModel.NewHostname.Should().BeEmpty();
    }

    [Fact]
    public void AddHost_EmptyHostname_ShowsMessage()
    {
        _viewModel.NewHostname = "";

        _viewModel.AddHostCommand.Execute(null);

        _hostTargetingService.DidNotReceive().AddFromHostnames(Arg.Any<IEnumerable<string>>());
        _viewModel.StatusMessage.Should().Contain("hostname");
    }

    [Fact]
    public void AddHost_InvalidHostname_ShowsValidationError()
    {
        _viewModel.NewHostname = "invalid!@#host";

        _viewModel.AddHostCommand.Execute(null);

        _hostTargetingService.DidNotReceive().AddFromHostnames(Arg.Any<IEnumerable<string>>());
        _viewModel.StatusMessage.Should().Contain("Invalid hostname");
    }

    [Fact]
    public async Task ImportCsvAsync_UserSelectsFile_CallsService()
    {
        _dialogService.ShowOpenFileDialogAsync(Arg.Any<string>())
            .Returns("C:\\test\\hosts.csv");

        await _viewModel.ImportCsvCommand.ExecuteAsync(null);

        await _hostTargetingService.Received(1).AddFromCsvFileAsync(
            "C:\\test\\hosts.csv", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ImportCsvAsync_UserCancels_NoServiceCall()
    {
        _dialogService.ShowOpenFileDialogAsync(Arg.Any<string>())
            .Returns((string?)null);

        await _viewModel.ImportCsvCommand.ExecuteAsync(null);

        await _hostTargetingService.DidNotReceive().AddFromCsvFileAsync(
            Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_NoTargets_ShowsMessage()
    {
        _viewModel.IsAdHocMode = true;
        _viewModel.AdHocScriptContent = "Get-Process";

        await _viewModel.ExecuteCommand.ExecuteAsync(null);

        _viewModel.StatusMessage.Should().Contain("target host");
        await _executionService.DidNotReceive().ExecuteAsync(
            Arg.Any<ExecutionJob>(), Arg.Any<IProgress<HostResult>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_NoScript_ShowsMessage()
    {
        _hostTargetingService.Targets.Returns([new HostTarget { Hostname = "server01" }]);

        ExecutionViewModel vm = new(
            _executionService,
            _hostTargetingService,
            _scriptLoaderService,
            _exportService,
            _dialogService,
            _settingsService,
            _logger);

        await vm.ExecuteCommand.ExecuteAsync(null);

        vm.StatusMessage.Should().Contain("script");
        await _executionService.DidNotReceive().ExecuteAsync(
            Arg.Any<ExecutionJob>(), Arg.Any<IProgress<HostResult>>(), Arg.Any<CancellationToken>());
        vm.Dispose();
    }

    [Fact]
    public async Task ExecuteAsync_WithAdHocScriptAndTargets_CallsService()
    {
        var targets = new ObservableCollection<HostTarget>
        {
            new() { Hostname = "server01" }
        };
        _hostTargetingService.Targets.Returns(targets);

        ExecutionViewModel vm = new(
            _executionService,
            _hostTargetingService,
            _scriptLoaderService,
            _exportService,
            _dialogService,
            _settingsService,
            _logger)
        {
            IsAdHocMode = true,
            AdHocScriptContent = "Get-Process"
        };

        _executionService.ExecuteAsync(
            Arg.Any<ExecutionJob>(), Arg.Any<IProgress<HostResult>>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                ExecutionJob job = callInfo.Arg<ExecutionJob>();
                job.Status = ExecutionStatus.Completed;
                return job;
            });

        await vm.ExecuteCommand.ExecuteAsync(null);

        await _executionService.Received(1).ExecuteAsync(
            Arg.Is<ExecutionJob>(j =>
                j.ScriptContent == "Get-Process" &&
                j.ScriptName == "Ad-Hoc Script"),
            Arg.Any<IProgress<HostResult>>(),
            Arg.Any<CancellationToken>());

        vm.StatusMessage.Should().Contain("Completed successfully");
        vm.Dispose();
    }

    [Fact]
    public async Task CancelExecutionAsync_NoCurrentJob_DoesNotThrow()
    {
        await _viewModel.CancelExecutionCommand.ExecuteAsync(null);

        await _executionService.DidNotReceive().CancelExecutionAsync(Arg.Any<Guid>());
    }

    [Fact]
    public void SelectedScript_WithDangerLevel_SetsWarning()
    {
        var script = new ScriptPlugin
        {
            FilePath = "C:\\scripts\\Danger.ps1",
            FileName = "Danger.ps1",
            EffectiveDangerLevel = ScriptDangerLevel.Destructive,
            Manifest = new ScriptManifest
            {
                Name = "Danger Script",
                Description = "Destructive script",
                Version = "1.0.0",
                Author = "Test",
                Category = "Test",
                DangerLevel = ScriptDangerLevel.Destructive
            }
        };

        _viewModel.SelectedScript = script;

        _viewModel.ScriptDangerWarning.Should().Contain("DESTRUCTIVE");
    }

    [Fact]
    public void SelectedScript_WithParameters_PopulatesParameterEntries()
    {
        var script = new ScriptPlugin
        {
            FilePath = "C:\\scripts\\Param.ps1",
            FileName = "Param.ps1",
            Manifest = new ScriptManifest
            {
                Name = "Param Script",
                Description = "Script with params",
                Version = "1.0.0",
                Author = "Test",
                Category = "Test",
                Parameters =
                [
                    new ScriptParameter
                    {
                        Name = "Server",
                        DisplayName = "Server Name",
                        Description = "Target server",
                        Type = "string",
                        Required = true
                    },
                    new ScriptParameter
                    {
                        Name = "Debug",
                        Type = "bool",
                        Required = false,
                        DefaultValue = "false"
                    }
                ]
            }
        };

        _viewModel.SelectedScript = script;

        _viewModel.ScriptParameters.Should().HaveCount(2);
        _viewModel.ScriptParameters[0].Name.Should().Be("Server");
        _viewModel.ScriptParameters[0].DisplayName.Should().Be("Server Name");
        _viewModel.ScriptParameters[0].IsRequired.Should().BeTrue();
        _viewModel.ScriptParameters[1].Name.Should().Be("Debug");
        _viewModel.ScriptParameters[1].Value.Should().Be("false");
    }

    [Fact]
    public void SelectedAuthMethod_CredSSP_ShowsWarning()
    {
        _viewModel.SelectedAuthMethod = WinRmAuthMethod.CredSSP;

        _viewModel.CredSspWarning.Should().Contain("CredSSP");
    }

    [Fact]
    public void SelectedAuthMethod_Kerberos_ClearsWarning()
    {
        _viewModel.SelectedAuthMethod = WinRmAuthMethod.CredSSP;
        _viewModel.SelectedAuthMethod = WinRmAuthMethod.Kerberos;

        _viewModel.CredSspWarning.Should().BeEmpty();
    }

    [Fact]
    public void SelectedResult_PopulatesDetailPanel()
    {
        var result = HostResult.Success("server01", "Output text", TimeSpan.FromSeconds(3));

        _viewModel.SelectedResult = result;

        _viewModel.ResultDetailOutput.Should().Be("Output text");
    }

    [Fact]
    public void SelectedResult_Null_ClearsDetailPanel()
    {
        _viewModel.SelectedResult = HostResult.Success("server01", "Output", TimeSpan.FromSeconds(1));
        _viewModel.SelectedResult = null;

        _viewModel.ResultDetailOutput.Should().BeEmpty();
        _viewModel.ResultErrors.Should().BeEmpty();
        _viewModel.ResultWarnings.Should().BeEmpty();
    }

    [Fact]
    public async Task ExportToCsvAsync_NoResults_ShowsMessage()
    {
        await _viewModel.ExportToCsvCommand.ExecuteAsync(null);

        _viewModel.StatusMessage.Should().Contain("No results");
        await _exportService.DidNotReceive().ExportToCsvAsync(
            Arg.Any<IEnumerable<HostResult>>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public void ClearHosts_ClearsTargetsAndShowsMessage()
    {
        _viewModel.ClearHostsCommand.Execute(null);

        _hostTargetingService.Received(1).ClearTargets();
        _viewModel.StatusMessage.Should().Contain("cleared");
    }

    [Fact]
    public void RemoveHost_CallsService()
    {
        _viewModel.RemoveHostCommand.Execute("server01");

        _hostTargetingService.Received(1).RemoveTarget("server01");
    }

    [Fact]
    public void ToggleAdHocMode_TogglesState()
    {
        _viewModel.IsAdHocMode.Should().BeFalse();

        _viewModel.ToggleAdHocModeCommand.Execute(null);

        _viewModel.IsAdHocMode.Should().BeTrue();
    }

    [Fact]
    public void Constructor_ThrowsOnNullDependencies()
    {
        Action act = () => _ = new ExecutionViewModel(
            null!, _hostTargetingService, _scriptLoaderService,
            _exportService, _dialogService, _settingsService, _logger);

        act.Should().Throw<ArgumentNullException>().And.ParamName.Should().Be("executionService");
    }
}
