namespace SysOpsCommander.Core.Validation;

/// <summary>
/// Represents the result of a validation operation.
/// </summary>
public sealed class ValidationResult
{
    /// <summary>
    /// Gets a value indicating whether the validation passed.
    /// </summary>
    public bool IsValid { get; private init; }

    /// <summary>
    /// Gets the error message when validation fails.
    /// </summary>
    public string? ErrorMessage { get; private init; }

    /// <summary>
    /// Creates a successful validation result.
    /// </summary>
    /// <returns>A <see cref="ValidationResult"/> indicating success.</returns>
    public static ValidationResult Success() => new() { IsValid = true };

    /// <summary>
    /// Creates a failed validation result with an error message.
    /// </summary>
    /// <param name="message">The error message describing the validation failure.</param>
    /// <returns>A <see cref="ValidationResult"/> indicating failure.</returns>
    public static ValidationResult Failure(string message) =>
        new() { IsValid = false, ErrorMessage = message };
}
