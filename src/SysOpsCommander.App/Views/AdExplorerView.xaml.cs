using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using SysOpsCommander.ViewModels;

namespace SysOpsCommander.App.Views;

/// <summary>
/// Interaction logic for AdExplorerView.xaml.
/// </summary>
public partial class AdExplorerView : UserControl
{
    private GridLength _zone1ExpandedWidth = new(280);
    private GridLength _zone3ExpandedWidth = new(320);
    private static readonly GridLength CollapsedWidth = new(40);

    /// <summary>
    /// Initializes a new instance of the <see cref="AdExplorerView"/> class.
    /// </summary>
    public AdExplorerView()
    {
        InitializeComponent();
        AdTreeView.AddHandler(TreeViewItem.ExpandedEvent, new RoutedEventHandler(OnTreeViewItemExpanded));
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is INotifyPropertyChanged oldVm)
        {
            oldVm.PropertyChanged -= OnViewModelPropertyChanged;
        }

        if (e.NewValue is INotifyPropertyChanged newVm)
        {
            newVm.PropertyChanged += OnViewModelPropertyChanged;
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(AdExplorerViewModel.IsZone1Collapsed))
        {
            UpdateZone1Width();
        }
        else if (e.PropertyName == nameof(AdExplorerViewModel.IsZone3Collapsed))
        {
            UpdateZone3Width();
        }
    }

    private void UpdateZone1Width()
    {
        if (DataContext is not AdExplorerViewModel vm)
        {
            return;
        }

        if (vm.IsZone1Collapsed)
        {
            _zone1ExpandedWidth = Zone1Column.Width;
            Zone1Column.Width = CollapsedWidth;
            Zone1Column.MinWidth = 40;
            Zone1Column.MaxWidth = 40;
        }
        else
        {
            Zone1Column.MinWidth = 200;
            Zone1Column.MaxWidth = 450;
            Zone1Column.Width = _zone1ExpandedWidth;
        }
    }

    private void UpdateZone3Width()
    {
        if (DataContext is not AdExplorerViewModel vm)
        {
            return;
        }

        if (vm.IsZone3Collapsed)
        {
            _zone3ExpandedWidth = Zone3Column.Width;
            Zone3Column.Width = CollapsedWidth;
            Zone3Column.MinWidth = 40;
            Zone3Column.MaxWidth = 40;
        }
        else
        {
            Zone3Column.MinWidth = 250;
            Zone3Column.MaxWidth = 600;
            Zone3Column.Width = _zone3ExpandedWidth;
        }
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
