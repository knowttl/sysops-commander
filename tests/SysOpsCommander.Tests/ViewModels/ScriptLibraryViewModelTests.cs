using FluentAssertions;
using NSubstitute;
using Serilog;
using SysOpsCommander.Core.Enums;
using SysOpsCommander.Core.Interfaces;
using SysOpsCommander.Core.Models;
using SysOpsCommander.ViewModels;

namespace SysOpsCommander.Tests.ViewModels;

public sealed class ScriptLibraryViewModelTests : IDisposable
{
    private readonly IScriptLoaderService _scriptLoaderService = Substitute.For<IScriptLoaderService>();
    private readonly ILogger _logger = Substitute.For<ILogger>();
    private readonly ScriptLibraryViewModel _viewModel;

    private static readonly List<ScriptPlugin> SampleScripts =
    [
        new()
        {
            FilePath = "C:\\scripts\\Get-Process.ps1",
            FileName = "Get-Process.ps1",
            Manifest = new ScriptManifest
            {
                Name = "Get Process",
                Description = "Lists running processes",
                Version = "1.0.0",
                Author = "Admin",
                Category = "Diagnostics"
            }
        },
        new()
        {
            FilePath = "C:\\scripts\\Test-Network.ps1",
            FileName = "Test-Network.ps1",
            Manifest = new ScriptManifest
            {
                Name = "Test Network",
                Description = "Network connectivity test",
                Version = "2.0.0",
                Author = "Admin",
                Category = "Network"
            }
        },
        new()
        {
            FilePath = "C:\\scripts\\NoManifest.ps1",
            FileName = "NoManifest.ps1"
        }
    ];

    public ScriptLibraryViewModelTests()
    {
        _scriptLoaderService.LoadAllScriptsAsync(Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<ScriptPlugin>)[]);

        _viewModel = new ScriptLibraryViewModel(_scriptLoaderService, _logger);
    }

    public void Dispose() => _viewModel.Dispose();

    [Fact]
    public async Task InitializeAsync_LoadsScriptsAndCategories()
    {
        _scriptLoaderService.LoadAllScriptsAsync(Arg.Any<CancellationToken>())
            .Returns(SampleScripts);

        await _viewModel.InitializeCommand.ExecuteAsync(null);

        _viewModel.FilteredScripts.Should().HaveCount(3);
        _viewModel.Categories.Should().Contain("All");
        _viewModel.Categories.Should().Contain("Diagnostics");
        _viewModel.Categories.Should().Contain("Network");
        _viewModel.Categories.Should().Contain("Uncategorized");
        _viewModel.StatusMessage.Should().Contain("3 scripts");
    }

    [Fact]
    public async Task SearchText_FiltersScriptsByFileName()
    {
        _scriptLoaderService.LoadAllScriptsAsync(Arg.Any<CancellationToken>())
            .Returns(SampleScripts);

        await _viewModel.InitializeCommand.ExecuteAsync(null);

        _viewModel.SearchText = "Network";

        _viewModel.FilteredScripts.Should().HaveCount(1);
        _viewModel.FilteredScripts[0].FileName.Should().Be("Test-Network.ps1");
    }

    [Fact]
    public async Task SelectedCategory_FiltersScripts()
    {
        _scriptLoaderService.LoadAllScriptsAsync(Arg.Any<CancellationToken>())
            .Returns(SampleScripts);

        await _viewModel.InitializeCommand.ExecuteAsync(null);

        _viewModel.SelectedCategory = "Diagnostics";

        _viewModel.FilteredScripts.Should().HaveCount(1);
        _viewModel.FilteredScripts[0].FileName.Should().Be("Get-Process.ps1");
    }

    [Fact]
    public async Task SelectedCategory_All_ShowsAllScripts()
    {
        _scriptLoaderService.LoadAllScriptsAsync(Arg.Any<CancellationToken>())
            .Returns(SampleScripts);

        await _viewModel.InitializeCommand.ExecuteAsync(null);

        _viewModel.SelectedCategory = "Diagnostics";
        _viewModel.SelectedCategory = "All";

        _viewModel.FilteredScripts.Should().HaveCount(3);
    }

    [Fact]
    public void SelectedScript_PopulatesDetailPanel()
    {
        var script = new ScriptPlugin
        {
            FilePath = "C:\\scripts\\Test.ps1",
            FileName = "Test.ps1",
            EffectiveDangerLevel = ScriptDangerLevel.Caution,
            Manifest = new ScriptManifest
            {
                Name = "Test Script",
                Description = "A test script",
                Version = "1.2.3",
                Author = "TestAuthor",
                Category = "Testing",
                Parameters =
                [
                    new ScriptParameter
                    {
                        Name = "Param1",
                        Type = "string",
                        Required = true,
                        Description = "First param"
                    }
                ]
            }
        };

        _viewModel.SelectedScript = script;

        _viewModel.ScriptDetailName.Should().Be("Test Script");
        _viewModel.ScriptDetailDescription.Should().Be("A test script");
        _viewModel.ScriptDetailVersion.Should().Be("1.2.3");
        _viewModel.ScriptDetailAuthor.Should().Be("TestAuthor");
        _viewModel.ScriptDetailCategory.Should().Be("Testing");
        _viewModel.ScriptDetailDangerLevel.Should().Be("Caution");
        _viewModel.ScriptDetailParameters.Should().Contain("Param1");
    }

    [Fact]
    public void SelectedScript_Null_ClearsDetailPanel()
    {
        _viewModel.SelectedScript = new ScriptPlugin
        {
            FilePath = "C:\\scripts\\Test.ps1",
            FileName = "Test.ps1"
        };

        _viewModel.SelectedScript = null;

        _viewModel.ScriptDetailName.Should().BeEmpty();
        _viewModel.ScriptDetailDescription.Should().BeEmpty();
        _viewModel.ScriptDetailVersion.Should().BeEmpty();
    }

    [Fact]
    public async Task RefreshAsync_ReloadsScripts()
    {
        _scriptLoaderService.LoadAllScriptsAsync(Arg.Any<CancellationToken>())
            .Returns(SampleScripts);

        await _viewModel.RefreshAsync();

        await _scriptLoaderService.Received(1).RefreshAsync(Arg.Any<CancellationToken>());
        _viewModel.FilteredScripts.Should().HaveCount(3);
        _viewModel.StatusMessage.Should().Contain("Refreshed");
    }

    [Fact]
    public void SelectedScript_WithValidationErrors_ShowsErrors()
    {
        var script = new ScriptPlugin
        {
            FilePath = "C:\\scripts\\Bad.ps1",
            FileName = "Bad.ps1",
            ValidationErrors = ["Syntax error on line 5", "Missing closing brace"],
            ValidationWarnings = ["Consider using approved verb"]
        };

        _viewModel.SelectedScript = script;

        _viewModel.ScriptDetailValidationErrors.Should().Contain("Syntax error");
        _viewModel.ScriptDetailWarnings.Should().Contain("approved verb");
    }

    [Fact]
    public void Constructor_ThrowsOnNullDependencies()
    {
        Action act = () => _ = new ScriptLibraryViewModel(null!, _logger);

        act.Should().Throw<ArgumentNullException>().And.ParamName.Should().Be("scriptLoaderService");
    }
}
