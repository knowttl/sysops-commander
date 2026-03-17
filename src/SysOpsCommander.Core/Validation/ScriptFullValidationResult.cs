using SysOpsCommander.Core.Models;

namespace SysOpsCommander.Core.Validation;

/// <summary>
/// Combined result from single-pass script validation: syntax, dangerous patterns, and manifest checks.
/// </summary>
public sealed class ScriptFullValidationResult
{
    /// <summary>
    /// Gets the syntax validation result from the AST parse.
    /// </summary>
    public ScriptSyntaxResult SyntaxResult { get; init; } = new();

    /// <summary>
    /// Gets the dangerous patterns detected via AST analysis.
    /// </summary>
    public IReadOnlyList<DangerousPatternWarning> DangerousPatterns { get; init; } = [];

    /// <summary>
    /// Gets the manifest schema and parameter alignment validation result.
    /// </summary>
    public ManifestValidationResult ManifestResult { get; init; } = new();
}
