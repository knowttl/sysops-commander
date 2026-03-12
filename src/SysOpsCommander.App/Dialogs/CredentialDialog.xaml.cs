using System.Security;
using SysOpsCommander.Core.Models;
using SysOpsCommander.ViewModels.Dialogs;

namespace SysOpsCommander.App.Dialogs;

/// <summary>
/// Interaction logic for CredentialDialog.xaml.
/// Password is extracted from <c>PasswordBox</c> in code-behind for security.
/// </summary>
public partial class CredentialDialog : Wpf.Ui.Controls.FluentWindow
{
    /// <summary>
    /// Gets the credential result when the dialog is confirmed, or <see langword="null"/> if cancelled.
    /// </summary>
    public CredentialDialogResult? CredentialResult { get; private set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="CredentialDialog"/> class.
    /// </summary>
    /// <param name="viewModel">The credential dialog ViewModel.</param>
    public CredentialDialog(CredentialDialogViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        DataContext = viewModel;
        InitializeComponent();
    }

    private void OnOkClicked(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is CredentialDialogViewModel vm)
        {
            vm.ConfirmCommand.Execute(null);

            SecureString securePassword = PasswordInput.SecurePassword.Copy();
            securePassword.MakeReadOnly();

            CredentialResult = new CredentialDialogResult
            {
                Domain = vm.Domain,
                Username = vm.Username,
                Password = securePassword,
                AuthMethod = vm.SelectedAuthMethod
            };
        }

        DialogResult = true;
        Close();
    }

    private void OnCancelClicked(object sender, System.Windows.RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
