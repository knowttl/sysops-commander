namespace SysOpsCommander.Core.Models;

/// <summary>
/// Represents a single segment in a breadcrumb navigation path derived from a Distinguished Name.
/// </summary>
/// <param name="Label">The display label for this segment (e.g., "OU=POWEREX").</param>
/// <param name="DistinguishedName">The full DN from this segment downward.</param>
public sealed record BreadcrumbSegment(string Label, string DistinguishedName);
