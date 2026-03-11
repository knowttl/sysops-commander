namespace SysOpsCommander.Core.Validation;

/// <summary>
/// Represents a single syntax error found during PowerShell script parsing.
/// </summary>
public sealed class ScriptValidationError
{
    /// <summary>
    /// Gets the line number where the error was detected.
    /// </summary>
    public required int Line { get; init; }

    /// <summary>
    /// Gets the column number where the error was detected.
    /// </summary>
    public required int Column { get; init; }

    /// <summary>
    /// Gets the error message from the PowerShell parser.
    /// </summary>
    public required string Message { get; init; }
}
