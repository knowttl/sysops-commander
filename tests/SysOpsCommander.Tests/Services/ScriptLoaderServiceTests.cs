using System.IO;
using System.Text.Json;
using FluentAssertions;
using NSubstitute;
using Serilog;
using SysOpsCommander.Core.Enums;
using SysOpsCommander.Core.Interfaces;
using SysOpsCommander.Core.Models;
using SysOpsCommander.Core.Validation;
using SysOpsCommander.Services;

namespace SysOpsCommander.Tests.Services;

/// <summary>
/// Tests for <see cref="ScriptLoaderService"/> covering loading, validation,
/// manifest parsing, danger level computation, and refresh logic.
/// </summary>
public sealed class ScriptLoaderServiceTests : IDisposable
{
    private readonly IScriptFileProvider _fileProvider = Substitute.For<IScriptFileProvider>();
    private readonly IScriptValidationService _validationService = Substitute.For<IScriptValidationService>();
    private readonly ILogger _logger = Substitute.For<ILogger>();
    private readonly ScriptLoaderService _service;
    private readonly string _tempDir;

    public ScriptLoaderServiceTests()
    {
        _service = new ScriptLoaderService(_fileProvider, _validationService, _logger);
        _tempDir = Path.Combine(Path.GetTempPath(), $"SysOpsTests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }

    [Fact]
    public void Constructor_NullFileProvider_ThrowsArgumentNull()
    {
        Action act = () => _ = new ScriptLoaderService(null!, _validationService, _logger);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("fileProvider");
    }

    [Fact]
    public void Constructor_NullValidationService_ThrowsArgumentNull()
    {
        Action act = () => _ = new ScriptLoaderService(_fileProvider, null!, _logger);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("validationService");
    }

    [Fact]
    public void Constructor_NullLogger_ThrowsArgumentNull()
    {
        Action act = () => _ = new ScriptLoaderService(_fileProvider, _validationService, null!);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("logger");
    }

    [Fact]
    public async Task LoadScriptAsync_NullPath_ThrowsArgumentNull()
    {
        Func<Task> act = () => _service.LoadScriptAsync(null!, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task LoadScriptAsync_FileNotFound_ThrowsFileNotFound()
    {
        string fakePath = Path.Combine(_tempDir, "nonexistent.ps1");

        Func<Task> act = () => _service.LoadScriptAsync(fakePath, CancellationToken.None);

        await act.Should().ThrowAsync<FileNotFoundException>();
    }

    [Fact]
    public async Task LoadScriptAsync_WithManifest_ParsesCorrectly()
    {
        string scriptPath = CreateTempScript("Write-Output 'hello'");
        var manifest = new ScriptManifest
        {
            Name = "TestScript",
            Description = "A test script",
            Version = "1.0.0",
            Author = "Tester",
            Category = "Security",
            DangerLevel = ScriptDangerLevel.Safe,
            OutputFormat = OutputFormat.Text,
            Parameters = []
        };
        CreateManifestFor(scriptPath, manifest);
        SetupFullValidationMock(scriptPath);

        ScriptPlugin result = await _service.LoadScriptAsync(scriptPath, CancellationToken.None);

        result.HasManifest.Should().BeTrue();
        result.Manifest!.Name.Should().Be("TestScript");
        result.Manifest.Category.Should().Be("Security");
        result.IsValidated.Should().BeTrue();
        result.FileName.Should().Be(Path.GetFileName(scriptPath));
        result.Content.Should().Contain("Write-Output");
    }

    [Fact]
    public async Task LoadScriptAsync_WithoutManifest_LoadsAsDropIn()
    {
        string scriptPath = CreateTempScript("Get-Process");
        SetupFullValidationMock(scriptPath);

        ScriptPlugin result = await _service.LoadScriptAsync(scriptPath, CancellationToken.None);

        result.HasManifest.Should().BeFalse();
        result.Manifest.Should().BeNull();
        result.IsValidated.Should().BeTrue();
        result.EffectiveDangerLevel.Should().Be(ScriptDangerLevel.Safe);
    }

    [Fact]
    public async Task LoadScriptAsync_SyntaxError_MarkedInvalid()
    {
        string scriptPath = CreateTempScript("function { broken syntax");

        _validationService.ValidateScriptFullAsync(scriptPath, Arg.Any<ScriptManifest?>(), Arg.Any<CancellationToken>())
            .Returns(new ScriptFullValidationResult
            {
                SyntaxResult = new ScriptSyntaxResult
                {
                    Errors = [new ScriptValidationError { Line = 1, Column = 10, Message = "Unexpected token" }]
                }
            });

        ScriptPlugin result = await _service.LoadScriptAsync(scriptPath, CancellationToken.None);

        result.IsValidated.Should().BeFalse();
        result.ValidationErrors.Should().ContainSingle()
            .Which.Should().Contain("Unexpected token");
    }

    [Fact]
    public async Task LoadScriptAsync_DangerousPattern_SetsEffectiveDangerLevel()
    {
        string scriptPath = CreateTempScript("Remove-Item -Recurse -Force C:\\");

        _validationService.ValidateScriptFullAsync(scriptPath, Arg.Any<ScriptManifest?>(), Arg.Any<CancellationToken>())
            .Returns(new ScriptFullValidationResult
            {
                DangerousPatterns =
                [
                    new DangerousPatternWarning
                    {
                        PatternName = "Remove-Item",
                        Description = "Deletes files or directories",
                        LineNumber = 1,
                        DangerLevel = ScriptDangerLevel.Destructive
                    }
                ]
            });

        ScriptPlugin result = await _service.LoadScriptAsync(scriptPath, CancellationToken.None);

        result.EffectiveDangerLevel.Should().Be(ScriptDangerLevel.Destructive);
        result.DangerousPatterns.Should().HaveCount(1);
    }

    [Fact]
    public async Task LoadScriptAsync_ManifestDangerHigherThanDetected_UsesManifestLevel()
    {
        string scriptPath = CreateTempScript("Write-Output 'safe script'");
        var manifest = new ScriptManifest
        {
            Name = "DangerousMarked",
            Description = "Marked destructive in manifest",
            Version = "1.0.0",
            Author = "Tester",
            Category = "Security",
            DangerLevel = ScriptDangerLevel.Destructive,
            OutputFormat = OutputFormat.Text,
            Parameters = []
        };
        CreateManifestFor(scriptPath, manifest);
        SetupFullValidationMock(scriptPath);

        ScriptPlugin result = await _service.LoadScriptAsync(scriptPath, CancellationToken.None);

        result.EffectiveDangerLevel.Should().Be(ScriptDangerLevel.Destructive);
    }

    [Fact]
    public async Task LoadScriptAsync_InvalidManifestJson_ReportsErrors()
    {
        string scriptPath = CreateTempScript("Write-Output 'test'");
        string jsonPath = Path.ChangeExtension(scriptPath, ".json");
        await File.WriteAllTextAsync(jsonPath, "{ not valid json }}}");
        SetupFullValidationMock(scriptPath);

        ScriptPlugin result = await _service.LoadScriptAsync(scriptPath, CancellationToken.None);

        result.HasManifest.Should().BeFalse();
        result.IsValidated.Should().BeFalse();
        result.ValidationErrors.Should().ContainSingle()
            .Which.Should().Contain("Failed to parse manifest JSON");
    }

    [Fact]
    public async Task LoadAllScriptsAsync_MultipleScripts_LoadsAll()
    {
        string script1 = CreateTempScript("Write-Output 'one'");
        string script2 = CreateTempScript("Write-Output 'two'");

        _fileProvider.ScanForScriptsAsync(Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<ScriptFileInfo>)
            [
                new ScriptFileInfo(script1, null, DateTime.UtcNow),
                new ScriptFileInfo(script2, null, DateTime.UtcNow)
            ]);

        SetupFullValidationMock(script1);
        SetupFullValidationMock(script2);

        IReadOnlyList<ScriptPlugin> results = await _service.LoadAllScriptsAsync(CancellationToken.None);

        results.Should().HaveCount(2);
    }

    [Fact]
    public async Task LoadAllScriptsAsync_OneScriptFails_StillLoadsOthers()
    {
        string goodScript = CreateTempScript("Write-Output 'good'");
        string badPath = Path.Combine(_tempDir, "deleted.ps1");

        _fileProvider.ScanForScriptsAsync(Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<ScriptFileInfo>)
            [
                new ScriptFileInfo(badPath, null, DateTime.UtcNow),
                new ScriptFileInfo(goodScript, null, DateTime.UtcNow)
            ]);

        SetupFullValidationMock(goodScript);

        IReadOnlyList<ScriptPlugin> results = await _service.LoadAllScriptsAsync(CancellationToken.None);

        results.Should().HaveCount(1);
        results[0].FilePath.Should().Be(goodScript);
    }

    [Fact]
    public async Task RefreshAsync_DetectsAddedAndRemovedScripts()
    {
        string script1 = CreateTempScript("Write-Output 'original'");
        SetupFullValidationMock(script1);

        _fileProvider.ScanForScriptsAsync(Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<ScriptFileInfo>)
            [
                new ScriptFileInfo(script1, null, DateTime.UtcNow)
            ]);

        await _service.LoadAllScriptsAsync(CancellationToken.None);

        string script2 = CreateTempScript("Write-Output 'added'");
        SetupFullValidationMock(script2);

        _fileProvider.ScanForScriptsAsync(Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<ScriptFileInfo>)
            [
                new ScriptFileInfo(script2, null, DateTime.UtcNow)
            ]);

        ScriptLibraryChangedEventArgs? eventArgs = null;
        _service.LibraryChanged += (_, args) => eventArgs = args;

        await _service.RefreshAsync(CancellationToken.None);

        eventArgs.Should().NotBeNull();
        eventArgs!.Added.Should().ContainSingle(p => p.FilePath == script2);
        eventArgs.Removed.Should().ContainSingle(path => path == script1);
    }

    [Fact]
    public async Task LoadScriptAsync_ManifestValidationErrors_PropagatedToPlugin()
    {
        string scriptPath = CreateTempScript("Write-Output 'test'");
        var manifest = new ScriptManifest
        {
            Name = "TestScript",
            Description = "Test",
            Version = "1.0.0",
            Author = "Tester",
            Category = "Security",
            DangerLevel = ScriptDangerLevel.Safe,
            OutputFormat = OutputFormat.Text,
            Parameters = []
        };
        CreateManifestFor(scriptPath, manifest);

        _validationService.ValidateScriptFullAsync(scriptPath, Arg.Any<ScriptManifest?>(), Arg.Any<CancellationToken>())
            .Returns(new ScriptFullValidationResult
            {
                ManifestResult = new ManifestValidationResult
                {
                    Errors = ["Parameter 'Foo' in manifest not found in script"],
                    Warnings = ["Missing description for parameter 'Bar'"]
                }
            });

        ScriptPlugin result = await _service.LoadScriptAsync(scriptPath, CancellationToken.None);

        result.IsValidated.Should().BeFalse();
        result.ValidationErrors.Should().Contain(e => e.Contains("Parameter 'Foo'"));
        result.ValidationWarnings.Should().Contain(w => w.Contains("Missing description"));
    }

    [Fact]
    public async Task LoadAllScriptsAsync_CachedUnchangedScript_ServesFromCache()
    {
        string scriptPath = CreateTempScript("Write-Output 'cached'");
        DateTime fileTime = File.GetLastWriteTimeUtc(scriptPath);
        SetupFullValidationMock(scriptPath);

        _fileProvider.ScanForScriptsAsync(Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<ScriptFileInfo>)
            [
                new ScriptFileInfo(scriptPath, null, fileTime)
            ]);

        IReadOnlyList<ScriptPlugin> first = await _service.LoadAllScriptsAsync(CancellationToken.None);
        first.Should().HaveCount(1);

        // Second call with same timestamp should use cache
        IReadOnlyList<ScriptPlugin> second = await _service.LoadAllScriptsAsync(CancellationToken.None);
        second.Should().HaveCount(1);
        second[0].FilePath.Should().Be(scriptPath);

        // ValidateScriptFullAsync should have been called only once (first load)
        await _validationService.Received(1)
            .ValidateScriptFullAsync(scriptPath, Arg.Any<ScriptManifest?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task LoadAllScriptsAsync_ModifiedScript_ReloadsFromDisk()
    {
        string scriptPath = CreateTempScript("Write-Output 'v1'");
        DateTime originalTime = File.GetLastWriteTimeUtc(scriptPath);
        SetupFullValidationMock(scriptPath);

        _fileProvider.ScanForScriptsAsync(Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<ScriptFileInfo>)
            [
                new ScriptFileInfo(scriptPath, null, originalTime)
            ]);

        await _service.LoadAllScriptsAsync(CancellationToken.None);

        // Simulate file modification with a newer timestamp
        DateTime newerTime = originalTime.AddSeconds(1);

        _fileProvider.ScanForScriptsAsync(Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<ScriptFileInfo>)
            [
                new ScriptFileInfo(scriptPath, null, newerTime)
            ]);

        await _service.LoadAllScriptsAsync(CancellationToken.None);

        // ValidateScriptFullAsync should have been called twice (original + reload)
        await _validationService.Received(2)
            .ValidateScriptFullAsync(scriptPath, Arg.Any<ScriptManifest?>(), Arg.Any<CancellationToken>());
    }

    private string CreateTempScript(string content)
    {
        string fileName = $"{Guid.NewGuid():N}.ps1";
        string filePath = Path.Combine(_tempDir, fileName);
        File.WriteAllText(filePath, content);
        return filePath;
    }

    private static void CreateManifestFor(string scriptPath, ScriptManifest manifest)
    {
        string jsonPath = Path.ChangeExtension(scriptPath, ".json");
        string json = JsonSerializer.Serialize(manifest);
        File.WriteAllText(jsonPath, json);
    }

    private void SetupFullValidationMock(string scriptPath)
    {
        _validationService.ValidateScriptFullAsync(scriptPath, Arg.Any<ScriptManifest?>(), Arg.Any<CancellationToken>())
            .Returns(new ScriptFullValidationResult());
    }
}
