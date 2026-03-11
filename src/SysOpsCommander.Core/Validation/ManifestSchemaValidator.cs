using System.Text.RegularExpressions;
using SysOpsCommander.Core.Models;

namespace SysOpsCommander.Core.Validation;

/// <summary>
/// Validates <see cref="ScriptManifest"/> instances against the required schema rules,
/// checking required fields, version format, category validity, parameter constraints, and more.
/// </summary>
public static partial class ManifestSchemaValidator
{
    private static readonly string[] AllowedCategories =
    [
        "Security", "Inventory", "Diagnostics", "Remediation",
        "Compliance", "Network", "Uncategorized"
    ];

    private static readonly string[] AllowedParameterTypes =
    [
        "string", "int", "bool", "choice"
    ];

    /// <summary>
    /// Validates a <see cref="ScriptManifest"/> against the expected schema.
    /// </summary>
    /// <param name="manifest">The manifest to validate.</param>
    /// <returns>A <see cref="ManifestValidationResult"/> containing any errors and warnings.</returns>
    public static ManifestValidationResult Validate(ScriptManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);

        List<string> errors = [];
        List<string> warnings = [];

        ValidateRequiredFields(manifest, errors);
        ValidateVersion(manifest.Version, errors);
        ValidateCategory(manifest.Category, errors);
        ValidateDangerLevel(manifest, warnings);
        ValidateOutputFormat(manifest, warnings);
        ValidateParameters(manifest.Parameters, errors);

        return new ManifestValidationResult
        {
            Errors = errors,
            Warnings = warnings
        };
    }

    private static void ValidateRequiredFields(ScriptManifest manifest, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(manifest.Name))
        {
            errors.Add("Required field 'Name' is missing or empty.");
        }

        if (string.IsNullOrWhiteSpace(manifest.Description))
        {
            errors.Add("Required field 'Description' is missing or empty.");
        }

        if (string.IsNullOrWhiteSpace(manifest.Version))
        {
            errors.Add("Required field 'Version' is missing or empty.");
        }

        if (string.IsNullOrWhiteSpace(manifest.Author))
        {
            errors.Add("Required field 'Author' is missing or empty.");
        }

        if (string.IsNullOrWhiteSpace(manifest.Category))
        {
            errors.Add("Required field 'Category' is missing or empty.");
        }
    }

    private static void ValidateVersion(string version, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(version))
        {
            return; // Already caught by required fields check
        }

        if (!SemverPattern().IsMatch(version))
        {
            errors.Add($"Version '{version}' is not in semver format (major.minor.patch).");
        }
    }

    private static void ValidateCategory(string category, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(category))
        {
            return;
        }

        if (!AllowedCategories.Contains(category, StringComparer.OrdinalIgnoreCase))
        {
            string allowed = string.Join(", ", AllowedCategories);
            errors.Add($"Category '{category}' is not recognized. Allowed values: {allowed}");
        }
    }

    private static void ValidateDangerLevel(ScriptManifest manifest, List<string> warnings)
    {
        if (!Enum.IsDefined(manifest.DangerLevel))
        {
            warnings.Add($"DangerLevel value '{manifest.DangerLevel}' is not valid. Defaulting to Safe.");
        }
    }

    private static void ValidateOutputFormat(ScriptManifest manifest, List<string> warnings)
    {
        if (!Enum.IsDefined(manifest.OutputFormat))
        {
            warnings.Add($"OutputFormat value '{manifest.OutputFormat}' is not valid. Defaulting to Text.");
        }
    }

    private static void ValidateParameters(IReadOnlyList<ScriptParameter> parameters, List<string> errors)
    {
        if (parameters.Count == 0)
        {
            return;
        }

        HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase);

        foreach (ScriptParameter param in parameters)
        {
            if (string.IsNullOrWhiteSpace(param.Name))
            {
                errors.Add("A parameter has an empty or missing name.");
                continue;
            }

            if (!seen.Add(param.Name))
            {
                errors.Add($"Duplicate parameter name '{param.Name}' found.");
            }

            if (!AllowedParameterTypes.Contains(param.Type, StringComparer.OrdinalIgnoreCase))
            {
                errors.Add($"Parameter '{param.Name}' has invalid type '{param.Type}'. Allowed: {string.Join(", ", AllowedParameterTypes)}");
            }

            if (string.Equals(param.Type, "choice", StringComparison.OrdinalIgnoreCase) &&
                (param.Choices is null || param.Choices.Count == 0))
            {
                errors.Add($"Parameter '{param.Name}' is type 'choice' but has no choices defined.");
            }
        }
    }

    [GeneratedRegex(@"^\d+\.\d+\.\d+$")]
    private static partial Regex SemverPattern();
}
