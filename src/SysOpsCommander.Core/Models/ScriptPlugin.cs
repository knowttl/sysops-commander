using SysOpsCommander.Core.Constants;
using SysOpsCommander.Core.Enums;

namespace SysOpsCommander.Core.Models;

/// <summary>
/// Represents a loaded PowerShell script plugin with optional manifest metadata.
/// </summary>
public sealed class ScriptPlugin
{
    /// <summary>
    /// Gets the full file path to the .ps1 script.
    /// </summary>
    public required string FilePath { get; init; }

    /// <summary>
    /// Gets the filename without the directory path.
    /// </summary>
    public required string FileName { get; init; }

    /// <summary>
    /// Gets the parsed JSON manifest, or <see langword="null"/> if no manifest was found.
    /// </summary>
    public ScriptManifest? Manifest { get; init; }

    /// <summary>
    /// Gets a value indicating whether this script has an associated manifest.
    /// </summary>
    public bool HasManifest => Manifest is not null;

    /// <summary>
    /// Gets a value indicating whether validation has been run on this script.
    /// </summary>
    public bool IsValidated { get; init; }

    /// <summary>
    /// Gets the validation errors, if any. Empty when valid.
    /// </summary>
    public IReadOnlyList<string> ValidationErrors { get; init; } = [];

    /// <summary>
    /// Gets the validation warnings, if any.
    /// </summary>
    public IReadOnlyList<string> ValidationWarnings { get; init; } = [];

    /// <summary>
    /// Gets the dangerous patterns detected by AST analysis.
    /// </summary>
    public IReadOnlyList<DangerousPatternWarning> DangerousPatterns { get; init; } = [];

    /// <summary>
    /// Gets the effective danger level — the higher of the manifest level and detected pattern level.
    /// </summary>
    public ScriptDangerLevel EffectiveDangerLevel { get; init; } = ScriptDangerLevel.Safe;

    /// <summary>
    /// Gets the category from the manifest, or <see cref="AppConstants.DefaultScriptCategory"/>.
    /// </summary>
    public string Category => Manifest?.Category is { Length: > 0 } category
        ? category
        : AppConstants.DefaultScriptCategory;

    /// <summary>
    /// Gets or sets the lazily loaded script content. <see langword="null"/> until first access.
    /// </summary>
    public string? Content { get; set; }

    /// <summary>
    /// Gets the file system last-modified timestamp for detecting stale cached scripts.
    /// </summary>
    public DateTime LastModified { get; init; }
}

/// <summary>
/// Represents a dangerous pattern detected during script AST analysis.
/// </summary>
public sealed class DangerousPatternWarning
{
    /// <summary>
    /// Gets the name of the pattern detected.
    /// </summary>
    public required string PatternName { get; init; }

    /// <summary>
    /// Gets a description of why this pattern is considered dangerous.
    /// </summary>
    public required string Description { get; init; }

    /// <summary>
    /// Gets the line number where the pattern was detected.
    /// </summary>
    public int LineNumber { get; init; }

    /// <summary>
    /// Gets the danger level associated with this pattern.
    /// </summary>
    public ScriptDangerLevel DangerLevel { get; init; } = ScriptDangerLevel.Caution;
}
