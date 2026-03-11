using System.Text.Json.Serialization;
using SysOpsCommander.Core.Enums;

namespace SysOpsCommander.Core.Models;

/// <summary>
/// Represents the JSON manifest that accompanies a PowerShell script plugin.
/// </summary>
public sealed class ScriptManifest
{
    /// <summary>
    /// Gets or sets the script name.
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the script description.
    /// </summary>
    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the semantic version (e.g., "1.0.0").
    /// </summary>
    [JsonPropertyName("version")]
    public string Version { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the script author.
    /// </summary>
    [JsonPropertyName("author")]
    public string Author { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the script category for grouping in the UI.
    /// </summary>
    [JsonPropertyName("category")]
    public string Category { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the danger level indicating script risk.
    /// </summary>
    [JsonPropertyName("dangerLevel")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public ScriptDangerLevel DangerLevel { get; set; } = ScriptDangerLevel.Safe;

    /// <summary>
    /// Gets or sets the output rendering hint.
    /// </summary>
    [JsonPropertyName("outputFormat")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public OutputFormat OutputFormat { get; set; } = OutputFormat.Text;

    /// <summary>
    /// Gets or sets the script parameters defined in the manifest.
    /// </summary>
    [JsonPropertyName("parameters")]
    public IReadOnlyList<ScriptParameter> Parameters { get; set; } = [];
}

/// <summary>
/// Represents a single parameter definition within a <see cref="ScriptManifest"/>.
/// </summary>
public sealed class ScriptParameter
{
    /// <summary>
    /// Gets or sets the parameter name (must match the PowerShell param block).
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the display-friendly name for the UI.
    /// </summary>
    [JsonPropertyName("displayName")]
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the parameter description.
    /// </summary>
    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the parameter type ("string", "int", "bool", "choice").
    /// </summary>
    [JsonPropertyName("type")]
    public string Type { get; set; } = "string";

    /// <summary>
    /// Gets or sets a value indicating whether the parameter is required.
    /// </summary>
    [JsonPropertyName("required")]
    public bool Required { get; set; }

    /// <summary>
    /// Gets or sets the default value for the parameter.
    /// </summary>
    [JsonPropertyName("defaultValue")]
    public object? DefaultValue { get; set; }

    /// <summary>
    /// Gets or sets the valid choices when <see cref="Type"/> is "choice".
    /// </summary>
    [JsonPropertyName("choices")]
    public IReadOnlyList<string>? Choices { get; set; }
}
