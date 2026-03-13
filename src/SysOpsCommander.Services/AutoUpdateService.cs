using System.Diagnostics;
using System.IO.Compression;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using Serilog;
using SysOpsCommander.Core.Constants;
using SysOpsCommander.Core.Interfaces;

namespace SysOpsCommander.Services;

/// <summary>
/// Checks for and applies application updates from a network share.
/// Reads <c>version.json</c> from the configured share path and stages updates locally.
/// </summary>
public sealed class AutoUpdateService : IAutoUpdateService
{
    private readonly ISettingsService _settingsService;
    private readonly ILogger _logger;

    private static readonly string UpdatesFolder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        AppConstants.AppDataFolder, "Updates");

    private static readonly string PendingUpdatePath = Path.Combine(UpdatesFolder, "pending-update.json");
    private static readonly string StagedFolder = Path.Combine(UpdatesFolder, "staged");

    /// <summary>
    /// Initializes a new instance of the <see cref="AutoUpdateService"/> class.
    /// </summary>
    /// <param name="settingsService">The settings service for retrieving the update share path.</param>
    /// <param name="logger">The Serilog logger.</param>
    public AutoUpdateService(ISettingsService settingsService, ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(settingsService);
        ArgumentNullException.ThrowIfNull(logger);

        _settingsService = settingsService;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<UpdateCheckResult> CheckForUpdateAsync(CancellationToken cancellationToken)
    {
        try
        {
            string sharePath = await _settingsService.GetEffectiveAsync("UpdateNetworkSharePath", cancellationToken);
            if (string.IsNullOrWhiteSpace(sharePath))
            {
                return new UpdateCheckResult(false, null, null, null, null);
            }

            string versionFilePath = Path.Combine(sharePath, "version.json");
            if (!File.Exists(versionFilePath))
            {
                _logger.Debug("Update version.json not found at {Path}", versionFilePath);
                return new UpdateCheckResult(false, null, null, null, null);
            }

            string json = await File.ReadAllTextAsync(versionFilePath, cancellationToken);
            VersionInfo? versionInfo = JsonSerializer.Deserialize<VersionInfo>(json, JsonSerializerOptions);

            if (versionInfo is null || string.IsNullOrWhiteSpace(versionInfo.Version))
            {
                _logger.Warning("version.json is malformed at {Path}", versionFilePath);
                return new UpdateCheckResult(false, null, null, null, null);
            }

            Version currentVersion = GetCurrentVersion();
            if (!Version.TryParse(versionInfo.Version, out Version? remoteVersion))
            {
                _logger.Warning("Cannot parse remote version '{Version}'", versionInfo.Version);
                return new UpdateCheckResult(false, null, null, null, null);
            }

            bool isNewer = remoteVersion > currentVersion;
            return new UpdateCheckResult(
                isNewer,
                versionInfo.Version,
                versionInfo.ReleaseNotes,
                versionInfo.ReleaseDate,
                versionInfo.MinimumVersion);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.Debug(ex, "Update check failed — continuing with current version");
            return new UpdateCheckResult(false, null, null, null, null);
        }
    }

    /// <inheritdoc />
    public async Task<UpdateDownloadResult> DownloadAndStageAsync(CancellationToken cancellationToken)
    {
        try
        {
            string sharePath = await _settingsService.GetEffectiveAsync("UpdateNetworkSharePath", cancellationToken);
            if (string.IsNullOrWhiteSpace(sharePath))
            {
                return new UpdateDownloadResult(false, null, "Update share path is not configured.");
            }

            string versionFilePath = Path.Combine(sharePath, "version.json");
            string json = await File.ReadAllTextAsync(versionFilePath, cancellationToken);
            VersionInfo? versionInfo = JsonSerializer.Deserialize<VersionInfo>(json, JsonSerializerOptions);

            if (versionInfo is null || string.IsNullOrWhiteSpace(versionInfo.Version))
            {
                return new UpdateDownloadResult(false, null, "version.json is malformed.");
            }

            string remoteZipPath = Path.Combine(sharePath, "SysOpsCommander.zip");
            if (!File.Exists(remoteZipPath))
            {
                return new UpdateDownloadResult(false, null, "SysOpsCommander.zip not found on update share.");
            }

            _ = Directory.CreateDirectory(UpdatesFolder);
            string localZipPath = Path.Combine(UpdatesFolder, "SysOpsCommander.zip");

            await CopyFileAsync(remoteZipPath, localZipPath, cancellationToken);

            FileInfo downloadedFile = new(localZipPath);
            if (downloadedFile.Length == 0)
            {
                File.Delete(localZipPath);
                return new UpdateDownloadResult(false, null, "Download failed: empty file.");
            }

            if (!string.IsNullOrWhiteSpace(versionInfo.Sha256))
            {
                string actualHash = await ComputeSha256Async(localZipPath, cancellationToken);
                if (!actualHash.Equals(versionInfo.Sha256, StringComparison.OrdinalIgnoreCase))
                {
                    File.Delete(localZipPath);
                    return new UpdateDownloadResult(false, null, "SHA256 hash mismatch — download may be corrupted.");
                }
            }

            if (Directory.Exists(StagedFolder))
            {
                Directory.Delete(StagedFolder, true);
            }

            ZipFile.ExtractToDirectory(localZipPath, StagedFolder);
            File.Delete(localZipPath);

            PendingUpdate pending = new()
            {
                StagedPath = StagedFolder,
                Version = versionInfo.Version,
                DownloadedAt = DateTime.UtcNow.ToString("o")
            };

            string pendingJson = JsonSerializer.Serialize(pending, JsonSerializerOptions);
            await File.WriteAllTextAsync(PendingUpdatePath, pendingJson, cancellationToken);

            _logger.Information("Update {Version} staged at {Path}", versionInfo.Version, StagedFolder);
            return new UpdateDownloadResult(true, StagedFolder, null);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to download and stage update");
            return new UpdateDownloadResult(false, null, ex.Message);
        }
    }

    /// <inheritdoc />
    public bool HasPendingUpdate()
    {
        if (!File.Exists(PendingUpdatePath))
        {
            return false;
        }

        try
        {
            string json = File.ReadAllText(PendingUpdatePath);
            PendingUpdate? pending = JsonSerializer.Deserialize<PendingUpdate>(json, JsonSerializerOptions);
            return pending?.StagedPath is not null && Directory.Exists(pending.StagedPath);
        }
        catch
        {
            return false;
        }
    }

    /// <inheritdoc />
    public void LaunchUpdaterAndExit()
    {
        string updaterPath = Path.Combine(AppContext.BaseDirectory, "SysOpsUpdater.exe");
        if (!File.Exists(updaterPath))
        {
            _logger.Error("Updater not found at {Path}", updaterPath);
            return;
        }

        _ = Process.Start(new ProcessStartInfo
        {
            FileName = updaterPath,
            Arguments = $"\"{StagedFolder}\" \"{AppContext.BaseDirectory}\" {Environment.ProcessId}",
            UseShellExecute = false
        });

        Environment.Exit(0);
    }

    private static Version GetCurrentVersion() =>
        Assembly.GetEntryAssembly()?.GetName().Version ?? new Version(1, 0, 0);

    private static async Task<string> ComputeSha256Async(string filePath, CancellationToken cancellationToken)
    {
        using FileStream stream = File.OpenRead(filePath);
        byte[] hash = await SHA256.HashDataAsync(stream, cancellationToken);
        return Convert.ToHexString(hash);
    }

    private static async Task CopyFileAsync(string source, string destination, CancellationToken cancellationToken)
    {
        const int bufferSize = 81920;
        using FileStream sourceStream = new(source, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize, useAsync: true);
        using FileStream destStream = new(destination, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize, useAsync: true);
        await sourceStream.CopyToAsync(destStream, bufferSize, cancellationToken);
    }

    private static readonly JsonSerializerOptions JsonSerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private sealed class VersionInfo
    {
        public string Version { get; set; } = string.Empty;
        public string? ReleaseDate { get; set; }
        public string? ReleaseNotes { get; set; }
        public string? MinimumVersion { get; set; }
        public string? Sha256 { get; set; }
    }

    private sealed class PendingUpdate
    {
        public string? StagedPath { get; set; }
        public string? Version { get; set; }
        public string? DownloadedAt { get; set; }
    }
}
