namespace SysOpsCommander.Core.Interfaces;

/// <summary>
/// Checks for and applies application updates from a network share.
/// </summary>
public interface IAutoUpdateService
{
    /// <summary>
    /// Checks whether an update is available.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The result of the update check.</returns>
    Task<UpdateCheckResult> CheckForUpdateAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Downloads the update and stages it for installation.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The result of the download and staging operation.</returns>
    Task<UpdateDownloadResult> DownloadAndStageAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Gets a value indicating whether a staged update is ready for installation.
    /// </summary>
    /// <returns><see langword="true"/> if an update is staged; otherwise, <see langword="false"/>.</returns>
    bool HasPendingUpdate();

    /// <summary>
    /// Launches the updater process and exits the current application.
    /// </summary>
    void LaunchUpdaterAndExit();
}

/// <summary>
/// Represents the result of an update availability check.
/// </summary>
/// <param name="IsUpdateAvailable">Whether a newer version is available.</param>
/// <param name="LatestVersion">The latest version string, if available.</param>
/// <param name="ReleaseNotes">The release notes for the latest version.</param>
/// <param name="ReleaseDate">The release date of the latest version.</param>
/// <param name="MinimumVersion">The minimum required version for this update.</param>
public record UpdateCheckResult(
    bool IsUpdateAvailable,
    string? LatestVersion,
    string? ReleaseNotes,
    string? ReleaseDate,
    string? MinimumVersion);

/// <summary>
/// Represents the result of an update download and staging operation.
/// </summary>
/// <param name="Success">Whether the download completed successfully.</param>
/// <param name="StagedPath">The path to the staged update files.</param>
/// <param name="ErrorMessage">An error message if the download failed.</param>
public record UpdateDownloadResult(
    bool Success,
    string? StagedPath,
    string? ErrorMessage);
