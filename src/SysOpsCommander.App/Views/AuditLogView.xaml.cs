using System.Windows;
using System.Windows.Controls;
using SysOpsCommander.ViewModels;

namespace SysOpsCommander.App.Views;

/// <summary>
/// Interaction logic for AuditLogView.xaml.
/// </summary>
public partial class AuditLogView : UserControl
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AuditLogView"/> class.
    /// </summary>
    public AuditLogView()
    {
        InitializeComponent();
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is AuditLogViewModel vm)
        {
            await vm.LoadAuditLogCommand.ExecuteAsync(null);
        }
    }
}
