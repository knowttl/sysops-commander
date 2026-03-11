namespace SysOpsCommander.Core.Validation;

/// <summary>
/// Represents the result of PowerShell script syntax validation.
/// </summary>
public sealed class ScriptSyntaxResult
{
    /// <summary>
    /// Gets a value indicating whether the script has no syntax errors.
    /// </summary>
    public bool IsValid => Errors.Count == 0;

    /// <summary>
    /// Gets the syntax errors found during parsing.
    /// </summary>
    public IReadOnlyList<ScriptValidationError> Errors { get; init; } = [];
}
