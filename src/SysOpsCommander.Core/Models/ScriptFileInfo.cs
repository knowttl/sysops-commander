namespace SysOpsCommander.Core.Models;

/// <summary>
/// Represents a discovered PowerShell script file and its associated manifest path.
/// </summary>
/// <param name="FullPath">The absolute path to the .ps1 script file.</param>
/// <param name="ManifestPath">The path to the companion .json manifest, or <see langword="null"/> if none exists.</param>
/// <param name="LastModified">The UTC last-write time of the script file.</param>
public sealed record ScriptFileInfo(
    string FullPath,
    string? ManifestPath,
    DateTime LastModified);
