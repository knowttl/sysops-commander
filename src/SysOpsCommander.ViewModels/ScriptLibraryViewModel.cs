using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Serilog;
using SysOpsCommander.Core.Extensions;
using SysOpsCommander.Core.Interfaces;
using SysOpsCommander.Core.Models;

namespace SysOpsCommander.ViewModels;

/// <summary>
/// ViewModel for the Script Library view. Manages the script plugin catalog and metadata.
/// </summary>
public partial class ScriptLibraryViewModel : ObservableObject, IRefreshable, IDisposable
{
    private readonly IScriptLoaderService _scriptLoaderService;
    private readonly ILogger _logger;
    private readonly CancellationTokenSource _cts = new();

    private IReadOnlyList<ScriptPlugin> _allScripts = [];

    [ObservableProperty]
    private ObservableCollection<ScriptPlugin> _filteredScripts = [];

    [ObservableProperty]
    private ScriptPlugin? _selectedScript;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private string _selectedCategory = "All";

    [ObservableProperty]
    private ObservableCollection<string> _categories = ["All"];

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    // --- Detail panel properties ---

    [ObservableProperty]
    private string _scriptDetailName = string.Empty;

    [ObservableProperty]
    private string _scriptDetailDescription = string.Empty;

    [ObservableProperty]
    private string _scriptDetailVersion = string.Empty;

    [ObservableProperty]
    private string _scriptDetailAuthor = string.Empty;

    [ObservableProperty]
    private string _scriptDetailCategory = string.Empty;

    [ObservableProperty]
    private string _scriptDetailDangerLevel = string.Empty;

    [ObservableProperty]
    private string _scriptDetailParameters = string.Empty;

    [ObservableProperty]
    private string _scriptDetailValidationErrors = string.Empty;

    [ObservableProperty]
    private string _scriptDetailWarnings = string.Empty;

    [ObservableProperty]
    private ObservableCollection<DangerousPatternWarning> _dangerousPatterns = [];

    /// <summary>
    /// Initializes a new instance of the <see cref="ScriptLibraryViewModel"/> class.
    /// </summary>
    public ScriptLibraryViewModel(
        IScriptLoaderService scriptLoaderService,
        ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(scriptLoaderService);
        ArgumentNullException.ThrowIfNull(logger);

        _scriptLoaderService = scriptLoaderService;
        _logger = logger;

        _scriptLoaderService.LibraryChanged += OnLibraryChanged;
    }

    /// <summary>
    /// Loads all scripts and populates categories.
    /// </summary>
    [RelayCommand]
    private async Task InitializeAsync()
    {
        try
        {
            _allScripts = await _scriptLoaderService.LoadAllScriptsAsync(_cts.Token);
            RebuildCategories();
            ApplyFilter();
            StatusMessage = $"Loaded {_allScripts.Count} scripts.";
        }
        catch (OperationCanceledException)
        {
            // Cancelled — no action needed
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to load script library");
            StatusMessage = "Failed to load scripts.";
        }
    }

    /// <inheritdoc />
    public async Task RefreshAsync()
    {
        try
        {
            await _scriptLoaderService.RefreshAsync(_cts.Token);
            _allScripts = await _scriptLoaderService.LoadAllScriptsAsync(_cts.Token);
            RebuildCategories();
            ApplyFilter();
            StatusMessage = $"Refreshed — {_allScripts.Count} scripts loaded.";
        }
        catch (OperationCanceledException)
        {
            // Cancelled — no action needed
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Script library refresh failed");
            StatusMessage = "Refresh failed.";
        }
    }

    partial void OnSearchTextChanged(string value) => ApplyFilter();

    partial void OnSelectedCategoryChanged(string value) => ApplyFilter();

    partial void OnSelectedScriptChanged(ScriptPlugin? value)
    {
        if (value is null)
        {
            ClearDetailPanel();
            return;
        }

        ScriptManifest? manifest = value.Manifest;
        ScriptDetailName = manifest?.Name ?? value.FileName;
        ScriptDetailDescription = manifest?.Description ?? "No description available.";
        ScriptDetailVersion = manifest?.Version ?? "—";
        ScriptDetailAuthor = manifest?.Author ?? "—";
        ScriptDetailCategory = value.Category;
        ScriptDetailDangerLevel = value.EffectiveDangerLevel.ToString();

        ScriptDetailParameters = manifest?.Parameters is { Count: > 0 } parameters
            ? string.Join("\n", parameters.Select(p =>
                $"  {p.Name} ({p.Type}){(p.Required ? " *required" : "")} — {p.Description}"))
            : "None";

        ScriptDetailValidationErrors = value.ValidationErrors.Count > 0
            ? string.Join("\n", value.ValidationErrors)
            : string.Empty;

        ScriptDetailWarnings = value.ValidationWarnings.Count > 0
            ? string.Join("\n", value.ValidationWarnings)
            : string.Empty;

        DangerousPatterns = new ObservableCollection<DangerousPatternWarning>(value.DangerousPatterns);
    }

    private void ApplyFilter()
    {
        IEnumerable<ScriptPlugin> filtered = _allScripts;

        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            string search = SearchText.Trim();
            filtered = filtered.Where(s =>
                s.FileName.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                (s.Manifest?.Name?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (s.Manifest?.Description?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false));
        }

        if (SelectedCategory != "All")
        {
            filtered = filtered.Where(s =>
                s.Category.Equals(SelectedCategory, StringComparison.OrdinalIgnoreCase));
        }

        FilteredScripts = new ObservableCollection<ScriptPlugin>(filtered);
        StatusMessage = $"Showing {FilteredScripts.Count} of {_allScripts.Count} scripts.";
    }

    private void RebuildCategories()
    {
        var cats = _allScripts
            .Select(s => s.Category)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(c => c, StringComparer.OrdinalIgnoreCase)
            .ToList();

        Categories = new ObservableCollection<string>(["All", .. cats]);

        if (!Categories.Contains(SelectedCategory))
        {
            SelectedCategory = "All";
        }
    }

    private void ClearDetailPanel()
    {
        ScriptDetailName = string.Empty;
        ScriptDetailDescription = string.Empty;
        ScriptDetailVersion = string.Empty;
        ScriptDetailAuthor = string.Empty;
        ScriptDetailCategory = string.Empty;
        ScriptDetailDangerLevel = string.Empty;
        ScriptDetailParameters = string.Empty;
        ScriptDetailValidationErrors = string.Empty;
        ScriptDetailWarnings = string.Empty;
        DangerousPatterns = [];
    }

    private void OnLibraryChanged(object? sender, ScriptLibraryChangedEventArgs e)
    {
        _logger.Information("Script library changed — reloading cached scripts");
        ReloadFromCacheAsync().SafeFireAndForget(_logger);
    }

    private async Task ReloadFromCacheAsync()
    {
        try
        {
            _allScripts = await _scriptLoaderService.LoadAllScriptsAsync(_cts.Token);
            RebuildCategories();
            ApplyFilter();
            StatusMessage = $"Refreshed — {_allScripts.Count} scripts loaded.";
        }
        catch (OperationCanceledException)
        {
            // Cancelled — no action needed
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Script library reload failed");
            StatusMessage = "Refresh failed.";
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _scriptLoaderService.LibraryChanged -= OnLibraryChanged;
        _cts.Dispose();
        GC.SuppressFinalize(this);
    }
}
