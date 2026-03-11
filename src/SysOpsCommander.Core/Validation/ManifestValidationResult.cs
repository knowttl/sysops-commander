namespace SysOpsCommander.Core.Validation;

/// <summary>
/// Represents the result of a script manifest validation operation.
/// </summary>
public sealed class ManifestValidationResult
{
    /// <summary>
    /// Gets a value indicating whether the manifest is valid (no errors; warnings are acceptable).
    /// </summary>
    public bool IsValid => Errors.Count == 0;

    /// <summary>
    /// Gets the list of validation errors.
    /// </summary>
    public IReadOnlyList<string> Errors { get; init; } = [];

    /// <summary>
    /// Gets the list of validation warnings.
    /// </summary>
    public IReadOnlyList<string> Warnings { get; init; } = [];
}
