using CommunityToolkit.Mvvm.ComponentModel;

namespace SysOpsCommander.Core.Models;

/// <summary>
/// Represents a selectable column for AD object export. Bindable to a CheckBox list.
/// </summary>
public sealed partial class ExportColumnSelection : ObservableObject
{
    /// <summary>
    /// Gets the attribute/column name.
    /// </summary>
    public required string ColumnName { get; init; }

    /// <summary>
    /// Gets or sets a value indicating whether this column is selected for export.
    /// </summary>
    [ObservableProperty]
    private bool _isSelected = true;
}
