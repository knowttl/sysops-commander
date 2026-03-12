using SysOpsCommander.ViewModels.Dialogs;

namespace SysOpsCommander.App.Dialogs;

/// <summary>
/// Interaction logic for DomainSelectorDialog.xaml.
/// </summary>
public partial class DomainSelectorDialog : Wpf.Ui.Controls.FluentWindow
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DomainSelectorDialog"/> class.
    /// </summary>
    /// <param name="viewModel">The domain selector ViewModel.</param>
    public DomainSelectorDialog(DomainSelectorViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        DataContext = viewModel;
        InitializeComponent();
    }

    private void OnOkClicked(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is DomainSelectorViewModel vm)
        {
            vm.ConfirmCommand.Execute(null);
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
