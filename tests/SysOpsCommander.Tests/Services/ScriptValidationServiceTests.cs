using System.IO;
using System.Management.Automation.Language;
using FluentAssertions;
using NSubstitute;
using Serilog;
using SysOpsCommander.Core.Enums;
using SysOpsCommander.Core.Models;
using SysOpsCommander.Core.Validation;
using SysOpsCommander.Services;

namespace SysOpsCommander.Tests.Services;

public sealed class ScriptValidationServiceTests : IDisposable
{
    private readonly ILogger _logger = Substitute.For<ILogger>();
    private readonly ScriptValidationService _service;
    private readonly string _tempDir;

    public ScriptValidationServiceTests()
    {
        _service = new ScriptValidationService(_logger);
        _tempDir = Path.Combine(Path.GetTempPath(), $"SysOpsTests_{Guid.NewGuid():N}");
        _ = Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, true);
        }
    }

    [Fact]
    public async Task ValidateSyntax_ValidScript_ReturnsNoErrors()
    {
        string scriptPath = CreateTempScript("Get-Process | Select-Object Name, Id");

        ScriptSyntaxResult result = await _service.ValidateSyntaxAsync(scriptPath, CancellationToken.None);

        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public async Task ValidateSyntax_SyntaxError_ReturnsErrorWithLineNumber()
    {
        string scriptPath = CreateTempScript("if ($true) {");

        ScriptSyntaxResult result = await _service.ValidateSyntaxAsync(scriptPath, CancellationToken.None);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle();
        result.Errors[0].Line.Should().BeGreaterThan(0);
        result.Errors[0].Message.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task ValidateSyntax_FileNotFound_ReturnsError()
    {
        ScriptSyntaxResult result = await _service.ValidateSyntaxAsync(
            Path.Combine(_tempDir, "nonexistent.ps1"), CancellationToken.None);

        result.IsValid.Should().BeFalse();
        result.Errors[0].Message.Should().Contain("not found");
    }

    [Fact]
    public async Task DetectDangerousPatterns_RemoveItemRecurseForce_DetectsDestructive()
    {
        string scriptPath = CreateTempScript("Remove-Item -Path C:\\Temp -Recurse -Force");

        IReadOnlyList<DangerousPatternWarning> warnings = await _service.DetectDangerousPatternsAsync(scriptPath, CancellationToken.None);

        warnings.Should().ContainSingle();
        warnings[0].PatternName.Should().Be("Remove-Item");
        warnings[0].DangerLevel.Should().Be(ScriptDangerLevel.Destructive);
    }

    [Fact]
    public async Task DetectDangerousPatterns_StopComputer_DetectsDestructive()
    {
        string scriptPath = CreateTempScript("Stop-Computer -ComputerName $env:COMPUTERNAME");

        IReadOnlyList<DangerousPatternWarning> warnings = await _service.DetectDangerousPatternsAsync(scriptPath, CancellationToken.None);

        warnings.Should().ContainSingle();
        warnings[0].PatternName.Should().Be("Stop-Computer");
        warnings[0].DangerLevel.Should().Be(ScriptDangerLevel.Destructive);
    }

    [Fact]
    public async Task DetectDangerousPatterns_SafeScript_NoWarnings()
    {
        string scriptPath = CreateTempScript("Get-Process | Select-Object Name, Id");

        IReadOnlyList<DangerousPatternWarning> warnings = await _service.DetectDangerousPatternsAsync(scriptPath, CancellationToken.None);

        warnings.Should().BeEmpty();
    }

    [Fact]
    public async Task DetectDangerousPatterns_RemoveItemWithoutForce_NoWarnings()
    {
        string scriptPath = CreateTempScript("Remove-Item -Path C:\\Temp\\file.txt");

        IReadOnlyList<DangerousPatternWarning> warnings = await _service.DetectDangerousPatternsAsync(scriptPath, CancellationToken.None);

        warnings.Should().BeEmpty();
    }

    [Fact]
    public async Task ValidateManifestPair_NoManifest_ReturnsWarning()
    {
        string scriptPath = CreateTempScript("Get-Process");

        ManifestValidationResult result = await _service.ValidateManifestPairAsync(scriptPath, CancellationToken.None);

        result.IsValid.Should().BeTrue();
        result.Warnings.Should().ContainSingle(w => w.Contains("No manifest found"));
    }

    [Fact]
    public async Task ValidateManifestPair_ParameterMismatch_ReturnsWarning()
    {
        string scriptPath = CreateTempScript("param([string]$ComputerName)\nGet-Process");
        string jsonPath = Path.ChangeExtension(scriptPath, ".json");
        File.WriteAllText(jsonPath, """
        {
            "name": "Test-Script",
            "description": "Test script",
            "version": "1.0.0",
            "author": "Admin",
            "category": "Diagnostics",
            "parameters": [
                { "name": "ComputerName", "type": "string" },
                { "name": "Verbose2", "type": "bool" }
            ]
        }
        """);

        ManifestValidationResult result = await _service.ValidateManifestPairAsync(scriptPath, CancellationToken.None);

        result.Warnings.Should().Contain(w => w.Contains("Verbose2") && w.Contains("not found in script"));
    }

    [Fact]
    public void AnalyzeAst_StopServiceCritical_DetectsCaution()
    {
        ScriptBlockAst ast = Parser.ParseInput(
            "Stop-Service -Name WinRM", out _, out _);

        IReadOnlyList<DangerousPatternWarning> warnings = ScriptValidationService.AnalyzeAst(ast);

        warnings.Should().ContainSingle();
        warnings[0].PatternName.Should().Be("Stop-Service");
        warnings[0].DangerLevel.Should().Be(ScriptDangerLevel.Caution);
    }

    [Fact]
    public void AnalyzeAst_StopServiceNonCritical_NoWarnings()
    {
        ScriptBlockAst ast = Parser.ParseInput(
            "Stop-Service -Name MyAppService", out _, out _);

        IReadOnlyList<DangerousPatternWarning> warnings = ScriptValidationService.AnalyzeAst(ast);

        warnings.Should().BeEmpty();
    }

    [Fact]
    public async Task ValidateScriptFullAsync_ValidScriptWithManifest_ReturnsCombinedResult()
    {
        string scriptPath = CreateTempScript("param([string]$ComputerName)\nGet-Process");
        var manifest = new ScriptManifest
        {
            Name = "Test-Script",
            Description = "Test script",
            Version = "1.0.0",
            Author = "Admin",
            Category = "Diagnostics",
            Parameters =
            [
                new ScriptParameter { Name = "ComputerName", Type = "string" }
            ]
        };

        ScriptFullValidationResult result = await _service.ValidateScriptFullAsync(
            scriptPath, manifest, CancellationToken.None);

        result.SyntaxResult.IsValid.Should().BeTrue();
        result.DangerousPatterns.Should().BeEmpty();
        result.ManifestResult.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateScriptFullAsync_ParsesOnce_DetectsAllIssues()
    {
        string scriptPath = CreateTempScript("param([string]$ComputerName)\nStop-Computer -Force");
        var manifest = new ScriptManifest
        {
            Name = "Test-Script",
            Description = "Test script",
            Version = "1.0.0",
            Author = "Admin",
            Category = "Diagnostics",
            Parameters =
            [
                new ScriptParameter { Name = "ComputerName", Type = "string" },
                new ScriptParameter { Name = "MissingParam", Type = "string" }
            ]
        };

        ScriptFullValidationResult result = await _service.ValidateScriptFullAsync(
            scriptPath, manifest, CancellationToken.None);

        result.SyntaxResult.IsValid.Should().BeTrue();
        result.DangerousPatterns.Should().ContainSingle(w => w.PatternName == "Stop-Computer");
        result.ManifestResult.Warnings.Should().Contain(w => w.Contains("MissingParam") && w.Contains("not found in script"));
    }

    [Fact]
    public async Task ValidateScriptFullAsync_NullManifest_ReturnsEmptyManifestResult()
    {
        string scriptPath = CreateTempScript("Get-Process");

        ScriptFullValidationResult result = await _service.ValidateScriptFullAsync(
            scriptPath, null, CancellationToken.None);

        result.SyntaxResult.IsValid.Should().BeTrue();
        result.ManifestResult.IsValid.Should().BeTrue();
        result.ManifestResult.Errors.Should().BeEmpty();
    }

    [Fact]
    public async Task ValidateScriptFullAsync_FileNotFound_ReturnsSyntaxError()
    {
        ScriptFullValidationResult result = await _service.ValidateScriptFullAsync(
            Path.Combine(_tempDir, "nonexistent.ps1"), null, CancellationToken.None);

        result.SyntaxResult.IsValid.Should().BeFalse();
        result.SyntaxResult.Errors[0].Message.Should().Contain("not found");
    }

    [Fact]
    public async Task ValidateScriptFullAsync_SyntaxError_ReturnsInSyntaxResult()
    {
        string scriptPath = CreateTempScript("if ($true) {");

        ScriptFullValidationResult result = await _service.ValidateScriptFullAsync(
            scriptPath, null, CancellationToken.None);

        result.SyntaxResult.IsValid.Should().BeFalse();
        result.SyntaxResult.Errors.Should().NotBeEmpty();
    }

    private string CreateTempScript(string content)
    {
        string path = Path.Combine(_tempDir, $"{Guid.NewGuid():N}.ps1");
        File.WriteAllText(path, content);
        return path;
    }
}
