using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SysOpsCommander.Core.Interfaces;
using SysOpsCommander.Core.Models;

namespace SysOpsCommander.ViewModels.Dialogs;

/// <summary>
/// ViewModel for the domain selector dialog. Supports manual domain entry and connection testing.
/// </summary>
public partial class DomainSelectorViewModel : ObservableObject
{
    private readonly IActiveDirectoryService _adService;

    [ObservableProperty]
    private string _domainName = string.Empty;

    [ObservableProperty]
    private string _domainControllerFqdn = string.Empty;

    [ObservableProperty]
    private string _testStatus = string.Empty;

    [ObservableProperty]
    private bool _isTestSuccessful;

    [ObservableProperty]
    private bool _isConfirmed;

    /// <summary>
    /// Initializes a new instance of the <see cref="DomainSelectorViewModel"/> class.
    /// </summary>
    /// <param name="adService">The Active Directory service for connection testing.</param>
    public DomainSelectorViewModel(IActiveDirectoryService adService)
    {
        ArgumentNullException.ThrowIfNull(adService);
        _adService = adService;
    }

    /// <summary>
    /// Gets the resulting <see cref="DomainConnection"/> when the dialog is confirmed, or <see langword="null"/> if not confirmed or test failed.
    /// </summary>
    public DomainConnection? Result =>
        IsConfirmed && IsTestSuccessful
            ? new DomainConnection
            {
                DomainName = DomainName,
                DomainControllerFqdn = string.IsNullOrWhiteSpace(DomainControllerFqdn) ? null : DomainControllerFqdn,
                RootDistinguishedName = ConvertDomainToDistinguishedName(DomainName),
                IsCurrentDomain = false
            }
            : null;

    /// <summary>
    /// Tests the connection to the specified domain.
    /// </summary>
    [RelayCommand]
    private async Task TestConnectionAsync()
    {
        if (string.IsNullOrWhiteSpace(DomainName))
        {
            TestStatus = "✗ Domain name is required";
            IsTestSuccessful = false;
            return;
        }

        TestStatus = "Testing...";
        try
        {
            var domain = new DomainConnection
            {
                DomainName = DomainName,
                DomainControllerFqdn = string.IsNullOrWhiteSpace(DomainControllerFqdn) ? null : DomainControllerFqdn,
                RootDistinguishedName = ConvertDomainToDistinguishedName(DomainName),
                IsCurrentDomain = false
            };
            await _adService.SetActiveDomainAsync(domain, CancellationToken.None);
            TestStatus = "✓ Connected successfully";
            IsTestSuccessful = true;
        }
        catch (Exception ex)
        {
            TestStatus = $"✗ Failed: {ex.Message}";
            IsTestSuccessful = false;
        }
    }

    /// <summary>
    /// Confirms the dialog selection.
    /// </summary>
    [RelayCommand]
    private void Confirm() => IsConfirmed = true;

    private static string ConvertDomainToDistinguishedName(string domainName) =>
        "DC=" + domainName.Replace(".", ",DC=", StringComparison.Ordinal);
}
