using System.Windows;
using System.Windows.Controls;
using SysOpsCommander.ViewModels;

namespace SysOpsCommander.App.Views;

/// <summary>
/// Interaction logic for SettingsView.xaml.
/// </summary>
public partial class SettingsView : UserControl
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SettingsView"/> class.
    /// </summary>
    public SettingsView()
    {
        InitializeComponent();
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is SettingsViewModel vm)
        {
            await vm.LoadSettingsCommand.ExecuteAsync(null);
        }
    }
}
