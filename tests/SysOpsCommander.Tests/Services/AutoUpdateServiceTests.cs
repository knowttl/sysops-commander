using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using FluentAssertions;
using NSubstitute;
using Serilog;
using SysOpsCommander.Core.Interfaces;
using SysOpsCommander.Services;

namespace SysOpsCommander.Tests.Services;

public sealed class AutoUpdateServiceTests : IDisposable
{
    private readonly ISettingsService _settingsService = Substitute.For<ISettingsService>();
    private readonly ILogger _logger = Substitute.For<ILogger>();
    private readonly AutoUpdateService _service;
    private readonly string _testSharePath;

    public AutoUpdateServiceTests()
    {
        _testSharePath = Path.Combine(Path.GetTempPath(), $"SysOpsUpdaterTest_{Guid.NewGuid():N}");
        _ = Directory.CreateDirectory(_testSharePath);

        _settingsService.GetEffectiveAsync("UpdateNetworkSharePath", Arg.Any<CancellationToken>())
            .Returns(_testSharePath);

        _service = new AutoUpdateService(_settingsService, _logger);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testSharePath))
        {
            Directory.Delete(_testSharePath, true);
        }

        // Clean up any staged update files created during tests
        string updatesFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SysOpsCommander", "Updates");
        if (Directory.Exists(updatesFolder))
        {
            Directory.Delete(updatesFolder, true);
        }
    }

    [Fact]
    public async Task CheckForUpdate_NewerVersion_ReturnsAvailable()
    {
        WriteVersionJson("99.0.0", "2026-04-15", "New features", null, null);

        UpdateCheckResult result = await _service.CheckForUpdateAsync(CancellationToken.None);

        result.IsUpdateAvailable.Should().BeTrue();
        result.LatestVersion.Should().Be("99.0.0");
        result.ReleaseNotes.Should().Be("New features");
        result.ReleaseDate.Should().Be("2026-04-15");
    }

    [Fact]
    public async Task CheckForUpdate_SameVersion_ReturnsNotAvailable()
    {
        // Current version will be something like 1.0.0 — use that
        string currentVersion = System.Reflection.Assembly.GetEntryAssembly()?.GetName().Version?.ToString() ?? "1.0.0";
        WriteVersionJson(currentVersion, null, null, null, null);

        UpdateCheckResult result = await _service.CheckForUpdateAsync(CancellationToken.None);

        result.IsUpdateAvailable.Should().BeFalse();
    }

    [Fact]
    public async Task CheckForUpdate_OlderVersion_ReturnsNotAvailable()
    {
        WriteVersionJson("0.0.1", null, null, null, null);

        UpdateCheckResult result = await _service.CheckForUpdateAsync(CancellationToken.None);

        result.IsUpdateAvailable.Should().BeFalse();
    }

    [Fact]
    public async Task CheckForUpdate_EmptySharePath_ReturnsFalse()
    {
        _settingsService.GetEffectiveAsync("UpdateNetworkSharePath", Arg.Any<CancellationToken>())
            .Returns(string.Empty);

        UpdateCheckResult result = await _service.CheckForUpdateAsync(CancellationToken.None);

        result.IsUpdateAvailable.Should().BeFalse();
    }

    [Fact]
    public async Task CheckForUpdate_MissingVersionJson_ReturnsFalse()
    {
        // Don't write version.json — share path exists but no file
        UpdateCheckResult result = await _service.CheckForUpdateAsync(CancellationToken.None);

        result.IsUpdateAvailable.Should().BeFalse();
    }

    [Fact]
    public async Task CheckForUpdate_MalformedJson_ReturnsFalse()
    {
        File.WriteAllText(Path.Combine(_testSharePath, "version.json"), "not valid json {{{");

        UpdateCheckResult result = await _service.CheckForUpdateAsync(CancellationToken.None);

        result.IsUpdateAvailable.Should().BeFalse();
    }

    [Fact]
    public async Task DownloadAndStage_MissingZip_ReturnsFailure()
    {
        WriteVersionJson("99.0.0", null, null, null, null);
        // No SysOpsCommander.zip created

        UpdateDownloadResult result = await _service.DownloadAndStageAsync(CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("SysOpsCommander.zip not found");
    }

    [Fact]
    public async Task DownloadAndStage_Sha256Mismatch_ReturnsFailure()
    {
        WriteVersionJson("99.0.0", null, null, null, "BADHASH0000000000000000000000000000000000000000000000000000000000");
        CreateDummyZip();

        UpdateDownloadResult result = await _service.DownloadAndStageAsync(CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("SHA256 hash mismatch");
    }

    [Fact]
    public async Task DownloadAndStage_ValidFile_StagesSuccessfully()
    {
        string zipPath = CreateDummyZip();
        string hash = ComputeFileHash(zipPath);
        WriteVersionJson("99.0.0", null, null, null, hash);

        UpdateDownloadResult result = await _service.DownloadAndStageAsync(CancellationToken.None);

        result.Success.Should().BeTrue();
        result.StagedPath.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task DownloadAndStage_NoSha256InManifest_SkipsHashCheck()
    {
        _ = CreateDummyZip();
        WriteVersionJson("99.0.0", null, null, null, null);

        UpdateDownloadResult result = await _service.DownloadAndStageAsync(CancellationToken.None);

        result.Success.Should().BeTrue();
    }

    [Fact]
    public void HasPendingUpdate_NoPendingFile_ReturnsFalse() =>
        _service.HasPendingUpdate().Should().BeFalse();

    private void WriteVersionJson(string version, string? releaseDate, string? releaseNotes, string? minimumVersion, string? sha256)
    {
        var versionInfo = new
        {
            version,
            releaseDate,
            releaseNotes,
            minimumVersion,
            sha256
        };

        string json = JsonSerializer.Serialize(versionInfo);
        File.WriteAllText(Path.Combine(_testSharePath, "version.json"), json);
    }

    private string CreateDummyZip()
    {
        string zipPath = Path.Combine(_testSharePath, "SysOpsCommander.zip");
        string tempDir = Path.Combine(_testSharePath, "zipcontents");
        _ = Directory.CreateDirectory(tempDir);
        File.WriteAllText(Path.Combine(tempDir, "dummy.txt"), "test content");
        System.IO.Compression.ZipFile.CreateFromDirectory(tempDir, zipPath);
        Directory.Delete(tempDir, true);
        return zipPath;
    }

    private static string ComputeFileHash(string filePath)
    {
        using FileStream stream = File.OpenRead(filePath);
        byte[] hash = SHA256.HashData(stream);
        return Convert.ToHexString(hash);
    }
}
