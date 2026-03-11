using CommunityToolkit.Mvvm.ComponentModel;
using SysOpsCommander.Core.Enums;

namespace SysOpsCommander.Core.Models;

/// <summary>
/// Represents a target host for remote execution, with observable properties for UI binding.
/// </summary>
public sealed partial class HostTarget : ObservableObject
{
    /// <summary>
    /// Gets the validated hostname (NetBIOS, FQDN, or IPv4).
    /// </summary>
    public required string Hostname { get; init; }

    [ObservableProperty]
    private HostStatus _status = HostStatus.Pending;

    /// <summary>
    /// Gets a value indicating whether the hostname passed validation.
    /// </summary>
    public bool IsValidated { get; init; }

    /// <summary>
    /// Gets the validation error message, if validation failed.
    /// </summary>
    public string? ValidationError { get; init; }
}
