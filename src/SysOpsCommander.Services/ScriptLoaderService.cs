using System.Collections.Concurrent;
using System.Text.Json;
using Serilog;
using SysOpsCommander.Core.Enums;
using SysOpsCommander.Core.Interfaces;
using SysOpsCommander.Core.Models;
using SysOpsCommander.Core.Validation;

namespace SysOpsCommander.Services;

/// <summary>
/// Loads, validates, and caches PowerShell script plugins from configured directories.
/// </summary>
public sealed class ScriptLoaderService : IScriptLoaderService
{
    private readonly IScriptFileProvider _fileProvider;
    private readonly IScriptValidationService _validationService;
    private readonly ILogger _logger;
    private readonly ConcurrentDictionary<string, ScriptPlugin> _cache = new(StringComparer.OrdinalIgnoreCase);

    /// <inheritdoc/>
    public event EventHandler<ScriptLibraryChangedEventArgs>? LibraryChanged;

    /// <summary>
    /// Initializes a new instance of the <see cref="ScriptLoaderService"/> class.
    /// </summary>
    /// <param name="fileProvider">The script file discovery provider.</param>
    /// <param name="validationService">The script validation service.</param>
    /// <param name="logger">The Serilog logger instance.</param>
    public ScriptLoaderService(
        IScriptFileProvider fileProvider,
        IScriptValidationService validationService,
        ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(fileProvider);
        ArgumentNullException.ThrowIfNull(validationService);
        ArgumentNullException.ThrowIfNull(logger);
        _fileProvider = fileProvider;
        _validationService = validationService;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<ScriptPlugin>> LoadAllScriptsAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<ScriptFileInfo> files = await _fileProvider
            .ScanForScriptsAsync(cancellationToken)
            .ConfigureAwait(false);

        var plugins = new ConcurrentBag<ScriptPlugin>();

        await Parallel.ForEachAsync(files, new ParallelOptions
        {
            MaxDegreeOfParallelism = Environment.ProcessorCount,
            CancellationToken = cancellationToken
        }, async (file, token) =>
        {
            if (IsCacheValid(file))
            {
                plugins.Add(_cache[file.FullPath]);
                return;
            }

            try
            {
                ScriptPlugin plugin = await LoadScriptAsync(file.FullPath, token)
                    .ConfigureAwait(false);
                plugins.Add(plugin);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.Error(ex, "Failed to load script: {ScriptPath}", file.FullPath);
            }
        }).ConfigureAwait(false);

        _logger.Information("Loaded {Count} script plugin(s)", plugins.Count);
        return [.. plugins];
    }

    /// <inheritdoc/>
    public async Task<ScriptPlugin> LoadScriptAsync(string filePath, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(filePath);

        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"Script file not found: {filePath}", filePath);
        }

        _logger.Debug("Loading script: {ScriptPath}", filePath);

        string scriptContent = await File.ReadAllTextAsync(filePath, cancellationToken)
            .ConfigureAwait(false);

        string jsonPath = Path.ChangeExtension(filePath, ".json");
        ScriptManifest? manifest = null;
        List<string> validationErrors = [];
        List<string> validationWarnings = [];

        if (File.Exists(jsonPath))
        {
            (manifest, List<string> errors, List<string> warnings) =
                await ReadManifestAsync(jsonPath, cancellationToken).ConfigureAwait(false);
            validationErrors.AddRange(errors);
            validationWarnings.AddRange(warnings);
        }

        // Single-pass validation: parse AST once for syntax, dangerous patterns, and manifest parameter alignment
        ScriptFullValidationResult validation = await _validationService
            .ValidateScriptFullAsync(filePath, manifest, cancellationToken)
            .ConfigureAwait(false);

        if (!validation.SyntaxResult.IsValid)
        {
            validationErrors.AddRange(validation.SyntaxResult.Errors.Select(static e =>
                $"Line {e.Line}, Col {e.Column}: {e.Message}"));
        }

        validationErrors.AddRange(validation.ManifestResult.Errors);
        validationWarnings.AddRange(validation.ManifestResult.Warnings);

        ScriptDangerLevel effectiveLevel = ComputeEffectiveDangerLevel(manifest, validation.DangerousPatterns);

        var plugin = new ScriptPlugin
        {
            FilePath = filePath,
            FileName = Path.GetFileName(filePath),
            Manifest = manifest,
            IsValidated = validation.SyntaxResult.IsValid && validationErrors.Count == 0,
            ValidationErrors = validationErrors,
            ValidationWarnings = validationWarnings,
            DangerousPatterns = validation.DangerousPatterns,
            EffectiveDangerLevel = effectiveLevel,
            Content = scriptContent,
            LastModified = File.GetLastWriteTimeUtc(filePath)
        };

        _ = _cache.AddOrUpdate(filePath, plugin, static (_, newPlugin) => newPlugin);

        return plugin;
    }

    /// <inheritdoc/>
    public async Task RefreshAsync(CancellationToken cancellationToken)
    {
        var previousPaths = new HashSet<string>(_cache.Keys, StringComparer.OrdinalIgnoreCase);

        IReadOnlyList<ScriptPlugin> freshPlugins = await LoadAllScriptsAsync(cancellationToken)
            .ConfigureAwait(false);

        var currentPaths = new HashSet<string>(freshPlugins.Select(static p => p.FilePath), StringComparer.OrdinalIgnoreCase);

        var removed = previousPaths.Except(currentPaths, StringComparer.OrdinalIgnoreCase).ToList();
        foreach (string path in removed)
        {
            _ = _cache.TryRemove(path, out _);
        }

        var added = freshPlugins
            .Where(p => !previousPaths.Contains(p.FilePath))
            .ToList();

        var modified = freshPlugins
            .Where(p => previousPaths.Contains(p.FilePath))
            .ToList();

        LibraryChanged?.Invoke(this, new ScriptLibraryChangedEventArgs
        {
            Added = added,
            Removed = removed,
            Modified = modified
        });

        _logger.Information("Script library refreshed: {Added} added, {Removed} removed",
            added.Count, removed.Count);
    }

    private bool IsCacheValid(ScriptFileInfo file)
    {
        if (!_cache.TryGetValue(file.FullPath, out ScriptPlugin? cached))
        {
            return false;
        }

        if (cached.LastModified < file.LastModified)
        {
            return false;
        }

        // Also invalidate if the manifest file has been modified since the script was cached
        return file.ManifestPath is null || !File.Exists(file.ManifestPath) ||
               File.GetLastWriteTimeUtc(file.ManifestPath) <= cached.LastModified;
    }

    private static async Task<(ScriptManifest? Manifest, List<string> Errors, List<string> Warnings)> ReadManifestAsync(
        string jsonPath,
        CancellationToken cancellationToken)
    {
        try
        {
            string json = await File.ReadAllTextAsync(jsonPath, cancellationToken)
                .ConfigureAwait(false);

            ScriptManifest? manifest = JsonSerializer.Deserialize<ScriptManifest>(json);
            return manifest is null
                ? (null, ["Manifest deserialized to null."], [])
                : (manifest, [], []);
        }
        catch (JsonException ex)
        {
            return (null, [$"Failed to parse manifest JSON: {ex.Message}"], []);
        }
    }

    private static ScriptDangerLevel ComputeEffectiveDangerLevel(
        ScriptManifest? manifest,
        IReadOnlyList<DangerousPatternWarning> patterns)
    {
        ScriptDangerLevel manifestLevel = manifest?.DangerLevel ?? ScriptDangerLevel.Safe;

        ScriptDangerLevel detectedLevel = patterns.Count > 0
            ? patterns.Max(static p => p.DangerLevel)
            : ScriptDangerLevel.Safe;

        return manifestLevel > detectedLevel ? manifestLevel : detectedLevel;
    }
}
