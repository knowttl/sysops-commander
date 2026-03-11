using SysOpsCommander.Core.Models;
using SysOpsCommander.Core.Validation;

namespace SysOpsCommander.Core.Interfaces;

/// <summary>
/// Provides PowerShell script validation capabilities including syntax checking,
/// dangerous pattern detection, manifest-script pair validation, and CredSSP availability checks.
/// </summary>
public interface IScriptValidationService
{
    /// <summary>
    /// Validates the syntax of a PowerShell script file using the PowerShell AST parser.
    /// </summary>
    /// <param name="scriptPath">The full path to the .ps1 script file.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A <see cref="ScriptSyntaxResult"/> containing any parse errors.</returns>
    Task<ScriptSyntaxResult> ValidateSyntaxAsync(string scriptPath, CancellationToken cancellationToken);

    /// <summary>
    /// Detects potentially dangerous cmdlet patterns in a PowerShell script via AST analysis.
    /// </summary>
    /// <param name="scriptPath">The full path to the .ps1 script file.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A list of dangerous patterns found in the script.</returns>
    Task<IReadOnlyList<DangerousPatternWarning>> DetectDangerousPatternsAsync(string scriptPath, CancellationToken cancellationToken);

    /// <summary>
    /// Validates that a .ps1 script and its companion .json manifest are consistent,
    /// checking schema validity and parameter alignment.
    /// </summary>
    /// <param name="ps1Path">The full path to the .ps1 script file.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A <see cref="ManifestValidationResult"/> with errors and warnings.</returns>
    Task<ManifestValidationResult> ValidateManifestPairAsync(string ps1Path, CancellationToken cancellationToken);

    /// <summary>
    /// Checks whether CredSSP authentication is available on a remote host.
    /// </summary>
    /// <param name="hostname">The target hostname to test.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A <see cref="ValidationResult"/> indicating CredSSP availability.</returns>
    Task<ValidationResult> ValidateCredSspAvailabilityAsync(string hostname, CancellationToken cancellationToken);
}
