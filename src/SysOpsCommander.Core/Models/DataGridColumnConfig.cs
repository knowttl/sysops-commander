using CommunityToolkit.Mvvm.ComponentModel;

namespace SysOpsCommander.Core.Models;

/// <summary>
/// Represents a configurable DataGrid column with bindable visibility.
/// </summary>
public partial class DataGridColumnConfig : ObservableObject
{
    /// <summary>
    /// Gets the column header text.
    /// </summary>
    public required string Header { get; init; }

    /// <summary>
    /// Gets the property name or attribute key to bind to.
    /// </summary>
    public required string PropertyName { get; init; }

    /// <summary>
    /// Gets or sets a value indicating whether this column is visible.
    /// </summary>
    [ObservableProperty]
    private bool _isVisible = true;
}
