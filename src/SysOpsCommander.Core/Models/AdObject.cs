namespace SysOpsCommander.Core.Models;

/// <summary>
/// Represents an Active Directory object retrieved from a directory query.
/// </summary>
public sealed class AdObject
{
    /// <summary>
    /// Gets the full distinguished name of the AD object.
    /// </summary>
    public required string DistinguishedName { get; init; }

    /// <summary>
    /// Gets the object class (e.g., "user", "computer", "group").
    /// </summary>
    public required string ObjectClass { get; init; }

    /// <summary>
    /// Gets the common name (cn) of the AD object.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets the display-friendly name, if available.
    /// </summary>
    public string? DisplayName { get; init; }

    /// <summary>
    /// Gets the description of the AD object, if available.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Gets all loaded AD attributes as a read-only dictionary.
    /// </summary>
    public IReadOnlyDictionary<string, object?> Attributes { get; init; } =
        new Dictionary<string, object?>();
}
