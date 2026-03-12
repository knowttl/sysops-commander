using System.Windows;
using System.Windows.Controls;
using SysOpsCommander.ViewModels;

namespace SysOpsCommander.App.Views;

/// <summary>
/// Interaction logic for AdExplorerView.xaml.
/// </summary>
public partial class AdExplorerView : UserControl
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AdExplorerView"/> class.
    /// </summary>
    public AdExplorerView()
    {
        InitializeComponent();
        AdTreeView.AddHandler(TreeViewItem.ExpandedEvent, new RoutedEventHandler(OnTreeViewItemExpanded));
    }

    private void OnTreeViewItemExpanded(object sender, RoutedEventArgs e)
    {
        if (e.OriginalSource is TreeViewItem { DataContext: AdTreeNode node }
            && DataContext is AdExplorerViewModel vm)
        {
            vm.ExpandNodeCommand.Execute(node);
        }
    }
}
