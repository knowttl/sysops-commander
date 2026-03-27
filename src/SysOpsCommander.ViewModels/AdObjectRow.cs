using CommunityToolkit.Mvvm.ComponentModel;
using SysOpsCommander.Core.Enums;
using SysOpsCommander.Core.Models;

namespace SysOpsCommander.ViewModels;

/// <summary>
/// Wraps an <see cref="AdObject"/> with observable IP resolution properties for DataGrid binding.
/// </summary>
public partial class AdObjectRow : ObservableObject
{
    /// <summary>
    /// Gets the underlying AD object.
    /// </summary>
    public AdObject AdObject { get; }

    /// <summary>
    /// Gets the common name of the AD object.
    /// </summary>
    public string Name => AdObject.Name;

    /// <summary>
    /// Gets the object class (e.g., "user", "computer", "group").
    /// </summary>
    public string ObjectClass => AdObject.ObjectClass;

    /// <summary>
    /// Gets the description of the AD object.
    /// </summary>
    public string? Description => AdObject.Description;

    /// <summary>
    /// Gets the full distinguished name of the AD object.
    /// </summary>
    public string DistinguishedName => AdObject.DistinguishedName;

    /// <summary>
    /// Gets the display name of the AD object.
    /// </summary>
    public string? DisplayName => AdObject.DisplayName;

    /// <summary>
    /// Gets all loaded AD attributes.
    /// </summary>
    public IReadOnlyDictionary<string, object?> Attributes => AdObject.Attributes;

    /// <summary>
    /// Gets the display text for the IP address column (e.g., "Resolving...", "10.0.1.50", "N/A").
    /// </summary>
    [ObservableProperty]
    private string? _ipAddress;

    /// <summary>
    /// Gets the tooltip text for the IP address column (hostname on success, error message on failure).
    /// </summary>
    [ObservableProperty]
    private string? _ipTooltip;

    /// <summary>
    /// Gets the current IP resolution status.
    /// </summary>
    [ObservableProperty]
    private IpResolutionStatus _ipResolutionStatus = IpResolutionStatus.NotStarted;

    /// <summary>
    /// Gets all resolved IP addresses for the inspector detail view.
    /// </summary>
    [ObservableProperty]
    private IReadOnlyList<string>? _allIpAddresses;

    /// <summary>
    /// Gets a value indicating whether this object is a computer.
    /// </summary>
    public bool IsComputer => ObjectClass.Equals("computer", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Gets a value indicating whether this account is disabled based on the userAccountControl attribute.
    /// </summary>
    public bool IsDisabled
    {
        get
        {
            if (!AdObject.Attributes.TryGetValue("userAccountControl", out object? uacValue) || uacValue is null)
            {
                return false;
            }

            int uac = uacValue switch
            {
                int i => i,
                long l => (int)l,
                string s when int.TryParse(s, out int parsed) => parsed,
                _ => 0
            };

            return (uac & 0x0002) != 0;
        }
    }

    /// <summary>
    /// Gets the DNS hostname from the AD attributes, if available.
    /// </summary>
    public string? DnsHostName =>
        AdObject.Attributes.TryGetValue("dNSHostName", out object? value) ? value?.ToString() : null;

    /// <summary>
    /// Initializes a new instance of the <see cref="AdObjectRow"/> class.
    /// </summary>
    /// <param name="adObject">The AD object to wrap.</param>
    public AdObjectRow(AdObject adObject)
    {
        ArgumentNullException.ThrowIfNull(adObject);
        AdObject = adObject;
    }

    /// <summary>
    /// Updates all observable IP properties from a resolution result.
    /// </summary>
    /// <param name="result">The DNS resolution result.</param>
    public void UpdateFromResolutionResult(IpResolutionResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        IpResolutionStatus = result.Status;

        switch (result.Status)
        {
            case IpResolutionStatus.Resolved:
                IpAddress = result.PrimaryIPv4 ?? (result.AllAddresses.Count > 0 ? result.AllAddresses[0] : null);
                IpTooltip = result.Hostname;
                AllIpAddresses = result.AllAddresses;
                break;

            case IpResolutionStatus.Failed:
                IpAddress = "N/A";
                IpTooltip = result.ErrorMessage;
                AllIpAddresses = null;
                break;

            case IpResolutionStatus.Resolving:
                IpAddress = "Resolving...";
                IpTooltip = null;
                AllIpAddresses = null;
                break;

            case IpResolutionStatus.NotStarted:
            case IpResolutionStatus.NotApplicable:
            default:
                IpAddress = null;
                IpTooltip = null;
                AllIpAddresses = null;
                break;
        }
    }
}
