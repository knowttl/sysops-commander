using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SysOpsCommander.Core.Enums;

namespace SysOpsCommander.ViewModels.Dialogs;

/// <summary>
/// ViewModel for the credential dialog. Supports domain, username, auth method selection.
/// Password is handled via code-behind for <c>PasswordBox</c> security.
/// </summary>
public partial class CredentialDialogViewModel : ObservableObject
{
    [ObservableProperty]
    private string _domain = string.Empty;

    [ObservableProperty]
    private string _username = string.Empty;

    [ObservableProperty]
    private WinRmAuthMethod _selectedAuthMethod = WinRmAuthMethod.Kerberos;

    [ObservableProperty]
    private bool _isConfirmed;

    /// <summary>
    /// Gets the available authentication methods.
    /// </summary>
    public IReadOnlyList<WinRmAuthMethod> AuthMethods { get; } =
        Enum.GetValues<WinRmAuthMethod>();

    /// <summary>
    /// Initializes a new instance of the <see cref="CredentialDialogViewModel"/> class.
    /// </summary>
    /// <param name="defaultDomain">The default domain to pre-populate.</param>
    /// <param name="defaultAuth">The default authentication method to pre-select.</param>
    public CredentialDialogViewModel(string? defaultDomain = null, WinRmAuthMethod? defaultAuth = null)
    {
        if (!string.IsNullOrWhiteSpace(defaultDomain))
        {
            _domain = defaultDomain;
        }

        if (defaultAuth.HasValue)
        {
            _selectedAuthMethod = defaultAuth.Value;
        }
    }

    /// <summary>
    /// Confirms the dialog selection.
    /// </summary>
    [RelayCommand]
    private void Confirm() => IsConfirmed = true;
}
