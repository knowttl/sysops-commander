using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SysOpsCommander.Core.Constants;

namespace SysOpsCommander.ViewModels;

/// <summary>
/// Provides the main window ViewModel with sidebar navigation support.
/// </summary>
public partial class MainWindowViewModel : ObservableObject
{
    [ObservableProperty]
    private string _title = AppConstants.AppName;

    [ObservableProperty]
    private ObservableObject? _currentView;

    /// <summary>
    /// Initializes a new instance of the <see cref="MainWindowViewModel"/> class.
    /// </summary>
    public MainWindowViewModel()
    {
    }

    /// <summary>
    /// Navigates to the specified view by name.
    /// </summary>
    /// <param name="viewName">The name of the view to navigate to.</param>
    [RelayCommand]
    private void Navigate(string? viewName) =>
        // Placeholder — subsequent phases will register view factories and resolve ViewModels here.
        _ = CurrentView;
}
