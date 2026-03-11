namespace SysOpsCommander.Core.Models;

/// <summary>
/// Represents a per-user setting stored in the SQLite database.
/// </summary>
public sealed class UserSettings
{
    /// <summary>
    /// Gets or sets the setting key identifier.
    /// </summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the serialized setting value.
    /// </summary>
    public string Value { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the UTC timestamp when this setting was last modified.
    /// </summary>
    public DateTime LastModified { get; set; }
}
