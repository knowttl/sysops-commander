using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using SysOpsCommander.Core.Models;
using SysOpsCommander.ViewModels;

namespace SysOpsCommander.App.Views;

/// <summary>
/// Interaction logic for AdExplorerView.xaml.
/// </summary>
public partial class AdExplorerView : UserControl
{
    private GridLength _zone1ExpandedWidth = new(280);
    private GridLength _zone3ExpandedWidth = new(320);
    private GridLength _savedZone1Width;
    private static readonly GridLength CollapsedWidth = new(40);

    /// <summary>
    /// Initializes a new instance of the <see cref="AdExplorerView"/> class.
    /// </summary>
    public AdExplorerView()
    {
        InitializeComponent();
        AdTreeView.AddHandler(TreeViewItem.ExpandedEvent, new RoutedEventHandler(OnTreeViewItemExpanded));
        AdTreeView.SelectedItemChanged += OnTreeViewSelectedItemChanged;
        DataContextChanged += OnDataContextChanged;

        _ = InputBindings.Add(new KeyBinding(new RelayFocusCommand(SearchTextBox), Key.F, ModifierKeys.Control));
        _ = InputBindings.Add(new KeyBinding(new RelayFocusCommand(TreeFilterTextBox), Key.L, ModifierKeys.Control));
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
        else if (e.PropertyName == nameof(AdExplorerViewModel.IsInspectorExpanded))
        {
            UpdateInspectorOverlay();
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

    private void UpdateInspectorOverlay()
    {
        if (DataContext is not AdExplorerViewModel vm)
        {
            return;
        }

        if (vm.IsInspectorExpanded)
        {
            _savedZone1Width = Zone1Column.Width;

            Zone1Column.Width = new GridLength(0);
            Zone1Column.MinWidth = 0;
            Zone1Column.MaxWidth = 0;
            Splitter1Column.Width = new GridLength(0);
            Zone2Column.Width = new GridLength(0);
            Splitter3Column.Width = new GridLength(0);
            Zone3Column.Width = new GridLength(1, GridUnitType.Star);
            Zone3Column.MinWidth = 0;
            Zone3Column.MaxWidth = double.PositiveInfinity;
        }
        else
        {
            Zone1Column.MinWidth = 200;
            Zone1Column.MaxWidth = 450;
            Zone1Column.Width = _savedZone1Width;
            Splitter1Column.Width = GridLength.Auto;
            Zone2Column.Width = new GridLength(1, GridUnitType.Star);
            Splitter3Column.Width = GridLength.Auto;
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

    private void OnTreeViewSelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (e.NewValue is AdTreeNode node && DataContext is AdExplorerViewModel vm)
        {
            vm.SetScopeCommand.Execute(node.DistinguishedName);
        }
    }

    private void RenameSavedSearch_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem { DataContext: SavedSearch search }
            && DataContext is AdExplorerViewModel vm)
        {
            vm.RenameSavedSearchCommand.Execute(search);
        }
    }

    private void DeleteSavedSearch_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem { DataContext: SavedSearch search }
            && DataContext is AdExplorerViewModel vm)
        {
            vm.DeleteSavedSearchCommand.Execute(search);
        }
    }
}

/// <summary>
/// Simple ICommand that focuses a UIElement when executed.
/// </summary>
internal sealed class RelayFocusCommand(UIElement target) : ICommand
{
#pragma warning disable CS0067 // Required by ICommand interface
    public event EventHandler? CanExecuteChanged;
#pragma warning restore CS0067

    public bool CanExecute(object? parameter) => true;

    public void Execute(object? parameter) => target.Focus();
}
