using Serilog;
using SysOpsCommander.Core.Interfaces;
using SysOpsCommander.Core.Models;

namespace SysOpsCommander.Infrastructure.FileSystem;

/// <summary>
/// Discovers PowerShell script files from configured directories, resolving paths
/// through the three-tier settings hierarchy.
/// </summary>
public sealed class ScriptFileProvider : IScriptFileProvider
{
    private readonly ISettingsService _settingsService;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ScriptFileProvider"/> class.
    /// </summary>
    /// <param name="settingsService">The settings service for resolving script paths.</param>
    /// <param name="logger">The Serilog logger instance.</param>
    public ScriptFileProvider(ISettingsService settingsService, ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(settingsService);
        ArgumentNullException.ThrowIfNull(logger);
        _settingsService = settingsService;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<string>> GetScriptDirectoriesAsync(CancellationToken cancellationToken)
    {
        List<string> directories = [];

        string effectivePath = await _settingsService
            .GetEffectiveAsync("SharedScriptRepositoryPath", cancellationToken)
            .ConfigureAwait(false);

        if (!string.IsNullOrWhiteSpace(effectivePath))
        {
            directories.Add(effectivePath);
        }

        string builtInPath = GetBuiltInScriptsPath();
        directories.Add(builtInPath);

        return directories;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<ScriptFileInfo>> ScanForScriptsAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<string> directories = await GetScriptDirectoriesAsync(cancellationToken)
            .ConfigureAwait(false);

        List<ScriptFileInfo> scripts = [];

        foreach (string directory in directories)
        {
            IEnumerable<ScriptFileInfo> found = ScanDirectory(directory);
            scripts.AddRange(found);
        }

        _logger.Information("Discovered {Count} script(s) across {DirCount} director(ies)",
            scripts.Count, directories.Count);

        return scripts;
    }

    private List<ScriptFileInfo> ScanDirectory(string directory)
    {
        if (!Directory.Exists(directory))
        {
            _logger.Warning("Script directory does not exist, skipping: {Directory}", directory);
            return [];
        }

        try
        {
            return Directory.EnumerateFiles(directory, "*.ps1", SearchOption.AllDirectories)
                .Select(ps1Path =>
                {
                    string jsonPath = Path.ChangeExtension(ps1Path, ".json");
                    string? manifestPath = File.Exists(jsonPath) ? jsonPath : null;
                    DateTime lastModified = File.GetLastWriteTimeUtc(ps1Path);

                    return new ScriptFileInfo(ps1Path, manifestPath, lastModified);
                })
                .ToList();
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.Warning(ex, "Access denied scanning script directory: {Directory}", directory);
            return [];
        }
        catch (DirectoryNotFoundException ex)
        {
            _logger.Warning(ex, "Script directory not found during scan: {Directory}", directory);
            return [];
        }
    }

    private static string GetBuiltInScriptsPath()
    {
        string appDir = AppDomain.CurrentDomain.BaseDirectory;
        return Path.Combine(appDir, "scripts", "examples");
    }
}
