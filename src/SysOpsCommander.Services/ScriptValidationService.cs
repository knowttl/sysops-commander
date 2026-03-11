using System.Management.Automation.Language;
using System.Text.Json;
using Serilog;
using SysOpsCommander.Core.Constants;
using SysOpsCommander.Core.Enums;
using SysOpsCommander.Core.Interfaces;
using SysOpsCommander.Core.Models;
using SysOpsCommander.Core.Validation;

namespace SysOpsCommander.Services;

/// <summary>
/// Provides PowerShell script validation: syntax checking, dangerous pattern detection,
/// manifest-script pair validation, and CredSSP availability testing.
/// </summary>
public sealed class ScriptValidationService : IScriptValidationService
{
    private readonly ILogger _logger;

    private static readonly Dictionary<string, DangerousPatternRule> DangerousPatterns = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Remove-Item"] = new("Recursive forced deletion can destroy entire directory trees.", ScriptDangerLevel.Destructive, RequiresSwitches: ["Recurse", "Force"]),
        ["Format-Volume"] = new("Formats disk volumes, destroying all data.", ScriptDangerLevel.Destructive),
        ["Stop-Computer"] = new("Shuts down the remote computer.", ScriptDangerLevel.Destructive),
        ["Restart-Computer"] = new("Restarts the remote computer, causing downtime.", ScriptDangerLevel.Caution),
        ["Clear-EventLog"] = new("Clears event logs, destroying forensic evidence.", ScriptDangerLevel.Destructive),
        ["Set-ExecutionPolicy"] = new("Changes PowerShell execution policy, affecting security posture.", ScriptDangerLevel.Caution),
        ["Disable-NetAdapter"] = new("Disables network adapters, potentially severing remote connectivity.", ScriptDangerLevel.Destructive),
        ["Stop-Service"] = new("Stops a Windows service.", ScriptDangerLevel.Caution, CriticalServiceNames: ["WinRM", "WinDefend", "EventLog", "Netlogon", "NTDS", "DNS"])
    };

    /// <summary>
    /// Initializes a new instance of the <see cref="ScriptValidationService"/> class.
    /// </summary>
    /// <param name="logger">The Serilog logger instance.</param>
    public ScriptValidationService(ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        _logger = logger;
    }

    /// <inheritdoc/>
    public Task<ScriptSyntaxResult> ValidateSyntaxAsync(string scriptPath, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scriptPath);
        _logger.Debug("Validating script syntax: {ScriptPath}", scriptPath);

        if (!File.Exists(scriptPath))
        {
            return Task.FromResult(new ScriptSyntaxResult
            {
                Errors = [new ScriptValidationError { Line = 0, Column = 0, Message = $"Script file not found: {scriptPath}" }]
            });
        }

        try
        {
            _ = Parser.ParseFile(scriptPath, out _, out ParseError[] parseErrors);

            var errors = parseErrors.Select(e => new ScriptValidationError
            {
                Line = e.Extent.StartLineNumber,
                Column = e.Extent.StartColumnNumber,
                Message = e.Message
            }).ToList();

            return Task.FromResult(new ScriptSyntaxResult { Errors = errors });
        }
        catch (UnauthorizedAccessException ex)
        {
            return Task.FromResult(new ScriptSyntaxResult
            {
                Errors = [new ScriptValidationError { Line = 0, Column = 0, Message = $"Permission denied reading script: {ex.Message}" }]
            });
        }
    }

    /// <inheritdoc/>
    public Task<IReadOnlyList<DangerousPatternWarning>> DetectDangerousPatternsAsync(string scriptPath, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scriptPath);
        _logger.Debug("Detecting dangerous patterns in: {ScriptPath}", scriptPath);

        if (!File.Exists(scriptPath))
        {
            return Task.FromResult<IReadOnlyList<DangerousPatternWarning>>([]);
        }

        ScriptBlockAst ast = Parser.ParseFile(scriptPath, out _, out _);
        List<DangerousPatternWarning> warnings = AnalyzeAst(ast);

        if (warnings.Count > 0)
        {
            _logger.Warning("Detected {Count} dangerous pattern(s) in {ScriptPath}", warnings.Count, scriptPath);
        }

        return Task.FromResult<IReadOnlyList<DangerousPatternWarning>>(warnings);
    }

    /// <inheritdoc/>
    public async Task<ManifestValidationResult> ValidateManifestPairAsync(string ps1Path, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(ps1Path);

        string jsonPath = Path.ChangeExtension(ps1Path, ".json");
        string fileName = Path.GetFileName(ps1Path);

        if (!File.Exists(jsonPath))
        {
            return new ManifestValidationResult
            {
                Warnings = [$"No manifest found for {fileName}. Script will load as a simple drop-in."]
            };
        }

        ScriptManifest? manifest;
        try
        {
            string json = await File.ReadAllTextAsync(jsonPath, cancellationToken).ConfigureAwait(false);
            manifest = JsonSerializer.Deserialize<ScriptManifest>(json);
        }
        catch (JsonException ex)
        {
            return new ManifestValidationResult
            {
                Errors = [$"Failed to parse manifest JSON: {ex.Message}"]
            };
        }

        if (manifest is null)
        {
            return new ManifestValidationResult
            {
                Errors = ["Manifest deserialized to null."]
            };
        }

        ManifestValidationResult schemaResult = ManifestSchemaValidator.Validate(manifest);
        List<string> errors = [.. schemaResult.Errors];
        List<string> warnings = [.. schemaResult.Warnings];

        // Compare manifest parameters with script param() block
        if (File.Exists(ps1Path) && manifest.Parameters.Count > 0)
        {
            ScriptBlockAst ast = Parser.ParseFile(ps1Path, out _, out _);
            HashSet<string> scriptParamNames = GetScriptParameterNames(ast);

            foreach (ScriptParameter manifestParam in manifest.Parameters)
            {
                if (!string.IsNullOrWhiteSpace(manifestParam.Name) &&
                    !scriptParamNames.Contains(manifestParam.Name))
                {
                    warnings.Add($"Manifest parameter '{manifestParam.Name}' not found in script param() block.");
                }
            }
        }

        return new ManifestValidationResult
        {
            Errors = errors,
            Warnings = warnings
        };
    }

    /// <inheritdoc/>
    public async Task<ValidationResult> ValidateCredSspAvailabilityAsync(string hostname, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(hostname);
        _logger.Information("Testing CredSSP availability on {Hostname}", hostname);

        try
        {
            using var ps = System.Management.Automation.PowerShell.Create();
            _ = ps.AddCommand("Test-WSMan")
                .AddParameter("ComputerName", hostname)
                .AddParameter("Authentication", "CredSSP");

            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(AppConstants.DefaultWinRmTimeoutSeconds));
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken, timeoutCts.Token);

            // Run in a background task to support cancellation
            System.Collections.ObjectModel.Collection<System.Management.Automation.PSObject> results =
                await Task.Run(ps.Invoke, linkedCts.Token).ConfigureAwait(false);

            return ps.HadErrors
                ? ValidationResult.Failure(
                    $"CredSSP authentication is not configured on {hostname}. " +
                    "This requires GPO configuration on both client and server. " +
                    "See: https://learn.microsoft.com/en-us/powershell/module/microsoft.wsman.management/enable-wsmancredssp")
                : ValidationResult.Success();
        }
        catch (OperationCanceledException)
        {
            return ValidationResult.Failure($"CredSSP availability check on {hostname} timed out.");
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "CredSSP check failed for {Hostname}", hostname);
            return ValidationResult.Failure($"Host {hostname} is not reachable: {ex.Message}");
        }
    }

    /// <summary>
    /// Analyzes a parsed AST for dangerous cmdlet patterns.
    /// </summary>
    internal static List<DangerousPatternWarning> AnalyzeAst(ScriptBlockAst ast)
    {
        List<DangerousPatternWarning> warnings = [];

        IEnumerable<CommandAst> commands = ast.FindAll(static a => a is CommandAst, searchNestedScriptBlocks: true)
            .Cast<CommandAst>();

        foreach (CommandAst cmd in commands)
        {
            string? cmdletName = cmd.GetCommandName();
            if (cmdletName is null || !DangerousPatterns.TryGetValue(cmdletName, out DangerousPatternRule? rule))
            {
                continue;
            }

            if (rule.RequiresSwitches is { Count: > 0 })
            {
                HashSet<string> presentSwitches = GetCommandSwitches(cmd);
                if (!rule.RequiresSwitches.All(presentSwitches.Contains))
                {
                    continue;
                }
            }

            if (rule.CriticalServiceNames is { Count: > 0 })
            {
                string? serviceName = GetServiceNameArgument(cmd);
                if (serviceName is null ||
                    !rule.CriticalServiceNames.Any(s => string.Equals(s, serviceName, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }
            }

            warnings.Add(new DangerousPatternWarning
            {
                PatternName = cmdletName,
                Description = rule.Reason,
                LineNumber = cmd.Extent.StartLineNumber,
                DangerLevel = rule.DangerLevel
            });
        }

        return warnings;
    }

    private static HashSet<string> GetCommandSwitches(CommandAst cmd)
    {
        HashSet<string> switches = new(StringComparer.OrdinalIgnoreCase);
        foreach (CommandElementAst element in cmd.CommandElements)
        {
            if (element is CommandParameterAst paramAst)
            {
                _ = switches.Add(paramAst.ParameterName);
            }
        }

        return switches;
    }

    private static string? GetServiceNameArgument(CommandAst cmd)
    {
        // Look for -Name parameter or first positional argument after the cmdlet name
        bool nextIsName = false;
        foreach (CommandElementAst element in cmd.CommandElements.Skip(1))
        {
            if (element is CommandParameterAst paramAst)
            {
                nextIsName = string.Equals(paramAst.ParameterName, "Name", StringComparison.OrdinalIgnoreCase);
                continue;
            }

            if (nextIsName && element is StringConstantExpressionAst strAst)
            {
                return strAst.Value;
            }

            nextIsName = false;
        }

        return null;
    }

    private static HashSet<string> GetScriptParameterNames(ScriptBlockAst ast)
    {
        HashSet<string> names = new(StringComparer.OrdinalIgnoreCase);

        if (ast.ParamBlock?.Parameters is not null)
        {
            foreach (ParameterAst param in ast.ParamBlock.Parameters)
            {
                _ = names.Add(param.Name.VariablePath.UserPath);
            }
        }

        return names;
    }

    private sealed record DangerousPatternRule(
        string Reason,
        ScriptDangerLevel DangerLevel,
        IReadOnlyList<string>? RequiresSwitches = null,
        IReadOnlyList<string>? CriticalServiceNames = null);
}
