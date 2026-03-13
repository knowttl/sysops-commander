using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Serilog;
using SysOpsCommander.Core.Constants;
using SysOpsCommander.Core.Extensions;
using SysOpsCommander.Core.Interfaces;
using SysOpsCommander.Core.Models;

namespace SysOpsCommander.ViewModels;

/// <summary>
/// ViewModel for the Active Directory Explorer view. Provides tree browse, search, object detail,
/// pre-built security filters, and integration with the execution target list.
/// </summary>
public partial class AdExplorerViewModel : ObservableObject, IRefreshable, IDisposable
{
    private readonly IActiveDirectoryService _adService;
    private readonly IHostTargetingService _hostTargetingService;
    private readonly ISettingsService _settingsService;
    private readonly IDialogService _dialogService;
    private readonly IExportService _exportService;
    private readonly ILogger _logger;
    private CancellationTokenSource? _searchDebounceTimer;
    private readonly CancellationTokenSource _cts = new();

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private string _activeDomainDisplay = string.Empty;

    [ObservableProperty]
    private ObservableCollection<AdTreeNode> _treeNodes = [];

    [ObservableProperty]
    private ObservableCollection<AdObject> _searchResults = [];

    [ObservableProperty]
    private AdObject? _selectedObject;

    [ObservableProperty]
    private ObservableCollection<KeyValuePair<string, string>> _selectedObjectAttributes = [];

    [ObservableProperty]
    private ObservableCollection<string> _selectedObjectGroups = [];

    [ObservableProperty]
    private ObservableCollection<AdAccessControlEntry> _selectedObjectPermissions = [];

    [ObservableProperty]
    private bool _isSearching;

    [ObservableProperty]
    private int _staleThresholdDays = AppConstants.DefaultStaleComputerDays;

    [ObservableProperty]
    private string _resultStatus = string.Empty;

    [ObservableProperty]
    private string _treeFilterText = string.Empty;

    [ObservableProperty]
    private ObservableCollection<AdTreeNode> _filteredTreeNodes = [];

    [ObservableProperty]
    private bool _isZone1Collapsed;

    [ObservableProperty]
    private bool _isZone3Collapsed;

    [ObservableProperty]
    private bool _isInspectorExpanded;

    [ObservableProperty]
    private string _selectedAttribute = "All attributes";

    [ObservableProperty]
    private bool _filterUsers;

    [ObservableProperty]
    private bool _filterComputers;

    [ObservableProperty]
    private bool _filterGroups;

    [ObservableProperty]
    private bool _filterOus;

    [ObservableProperty]
    private bool _filterAll = true;

    [ObservableProperty]
    private bool _searchEntireDomain = true;

    [ObservableProperty]
    private string _scopeDisplay = string.Empty;

    [ObservableProperty]
    private ObservableCollection<BreadcrumbSegment> _breadcrumbSegments = [];

    [ObservableProperty]
    private ObservableCollection<SearchHistoryEntry> _searchHistory = [];

    [ObservableProperty]
    private bool _isSearchHistoryOpen;

    [ObservableProperty]
    private ObservableCollection<SavedSearch> _savedSearches = [];

    [ObservableProperty]
    private bool _isSavedSearchesOpen;

    [ObservableProperty]
    private bool _isLdapFilterMode;

    [ObservableProperty]
    private string _rawLdapFilter = string.Empty;

    [ObservableProperty]
    private ObservableCollection<ExportColumnSelection> _availableExportColumns = [];

    [ObservableProperty]
    private bool _isExportColumnPickerOpen;

    [ObservableProperty]
    private bool _isExportMenuOpen;

    /// <summary>
    /// Gets or sets the filter text for the groups tab search bar.
    /// </summary>
    [ObservableProperty]
    private string _groupFilterText = string.Empty;

    /// <summary>
    /// Gets or sets the filtered group membership list.
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<string> _filteredGroups = [];

    /// <summary>
    /// Gets or sets whether the column picker popup is open.
    /// </summary>
    [ObservableProperty]
    private bool _isColumnPickerOpen;

    /// <summary>
    /// Gets or sets the configurable DataGrid columns.
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<DataGridColumnConfig> _dataGridColumns = [];

    /// <summary>
    /// Gets a value indicating whether the search undo stack has entries.
    /// </summary>
    [ObservableProperty]
    private bool _canGoBack;

    /// <summary>
    /// Tracks the simple-mode search state preserved when switching to LDAP filter mode.
    /// </summary>
    private string _preservedSearchText = string.Empty;
    private string _preservedSelectedAttribute = "All attributes";
    private bool _preservedFilterAll = true;
    private bool _preservedFilterUsers;
    private bool _preservedFilterComputers;
    private bool _preservedFilterGroups;
    private bool _preservedFilterOus;

    /// <summary>
    /// Tracks the pending export format ("csv" or "excel") so the column picker knows which to invoke.
    /// </summary>
    private string _pendingExportFormat = string.Empty;

    /// <summary>
    /// Stack of previous search states for undo navigation (max <see cref="AppConstants.MaxSearchUndoDepth"/>).
    /// </summary>
    private readonly List<SearchStateSnapshot> _undoStack = [];

    /// <summary>
    /// Gets the list of searchable attribute options.
    /// </summary>
    public IReadOnlyList<string> AttributeOptions { get; } =
    [
        "All attributes",
        "sAMAccountName",
        "cn",
        "displayName",
        "mail",
        "SID",
        "description",
        "distinguishedName"
    ];

    /// <summary>
    /// Initializes a new instance of the <see cref="AdExplorerViewModel"/> class.
    /// </summary>
    /// <param name="adService">The Active Directory service.</param>
    /// <param name="hostTargetingService">The shared host targeting service.</param>
    /// <param name="settingsService">The settings service for configurable thresholds.</param>
    /// <param name="dialogService">The dialog service for user notifications.</param>
    /// <param name="exportService">The export service for CSV/Excel output.</param>
    /// <param name="logger">The Serilog logger.</param>
    public AdExplorerViewModel(
        IActiveDirectoryService adService,
        IHostTargetingService hostTargetingService,
        ISettingsService settingsService,
        IDialogService dialogService,
        IExportService exportService,
        ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(adService);
        ArgumentNullException.ThrowIfNull(hostTargetingService);
        ArgumentNullException.ThrowIfNull(settingsService);
        ArgumentNullException.ThrowIfNull(dialogService);
        ArgumentNullException.ThrowIfNull(exportService);
        ArgumentNullException.ThrowIfNull(logger);

        _adService = adService;
        _hostTargetingService = hostTargetingService;
        _settingsService = settingsService;
        _dialogService = dialogService;
        _exportService = exportService;
        _logger = logger;

        InitializeDataGridColumns();
        LoadPersistedStateAsync().SafeFireAndForget(_logger);
    }

    /// <summary>
    /// Refreshes the tree root and active domain display.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    public Task RefreshAsync() => LoadTreeRootAsync();

    /// <summary>
    /// Loads the tree root from the active domain.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [RelayCommand]
    private async Task LoadTreeRootAsync()
    {
        try
        {
            DomainConnection domain = _adService.GetActiveDomain();
            ActiveDomainDisplay = domain.DomainName;

            IReadOnlyList<AdObject> rootChildren = await _adService.BrowseChildrenAsync(
                domain.RootDistinguishedName, _cts.Token);

            TreeNodes = new ObservableCollection<AdTreeNode>(
                rootChildren
                    .Where(IsNavigableTreeObject)
                    .OrderBy(o => o.Name, StringComparer.OrdinalIgnoreCase)
                    .Select(MapToTreeNode));

            ApplyTreeFilter();
        }
        catch (OperationCanceledException)
        {
            // Cancelled — no action needed
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to load AD tree root");
            ResultStatus = $"Failed to load tree: {ex.Message}";
        }
    }

    /// <summary>
    /// Expands a tree node by lazy-loading its children from AD.
    /// </summary>
    /// <param name="node">The tree node to expand.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    [RelayCommand]
    private async Task ExpandNodeAsync(AdTreeNode? node)
    {
        if (node is null || !node.HasDummyChild)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(node.DistinguishedName))
        {
            _logger.Warning("Skipping tree expansion — node has empty DistinguishedName");
            return;
        }

        node.HasDummyChild = false;
        node.Children.Clear();

        try
        {
            IReadOnlyList<AdObject> children = await _adService.BrowseChildrenAsync(
                node.DistinguishedName, _cts.Token);

            foreach (AdObject child in children
                         .Where(IsNavigableTreeObject)
                         .OrderBy(o => o.Name, StringComparer.OrdinalIgnoreCase))
            {
                AdTreeNode childNode = MapToTreeNode(child);
                node.Children.Add(childNode);
            }
        }
        catch (OperationCanceledException)
        {
            // Cancelled — no action needed
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to expand tree node {DN}", node.DistinguishedName);
        }
    }

    /// <summary>
    /// Searches Active Directory for objects matching the current search text.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [RelayCommand]
    private async Task SearchAsync()
    {
        string queryText = IsLdapFilterMode ? RawLdapFilter : SearchText;

        if (string.IsNullOrWhiteSpace(queryText))
        {
            return;
        }

        PushSearchState();
        IsSearching = true;
        ResultStatus = "Searching...";

        try
        {
            AdSearchResult result;

            if (IsLdapFilterMode)
            {
                string? baseDn = SearchEntireDomain ? null : (string.IsNullOrEmpty(ScopeDisplay) ? null : ScopeDisplay);
                result = await _adService.SearchWithFilterAsync(RawLdapFilter, baseDn, _cts.Token);
            }
            else
            {
                string? attribute = SelectedAttribute == "All attributes" ? null : SelectedAttribute;
                IReadOnlyList<string>? objectClasses = GetActiveObjectClassFilters();
                string? baseDn = SearchEntireDomain ? null : (string.IsNullOrEmpty(ScopeDisplay) ? null : ScopeDisplay);

                result = await _adService.SearchScopedAsync(
                    SearchText, baseDn, objectClasses, attribute, _cts.Token);
            }

            SearchResults = new ObservableCollection<AdObject>(result.Results);
            ResultStatus = $"{result.TotalResultCount} results found in {result.ExecutionTime.TotalMilliseconds:F0}ms";

            await RecordSearchHistoryAsync(queryText, result.TotalResultCount);
        }
        catch (OperationCanceledException)
        {
            ResultStatus = "Search cancelled.";
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "AD search failed for term: {SearchTerm}", queryText);
            ResultStatus = $"Search failed: {ex.Message}";
        }
        finally
        {
            IsSearching = false;
        }
    }

    private List<string>? GetActiveObjectClassFilters()
    {
        if (FilterAll)
        {
            return null;
        }

        var filters = new List<string>();
        if (FilterUsers) { filters.Add("user"); }
        if (FilterComputers) { filters.Add("computer"); }
        if (FilterGroups) { filters.Add("group"); }
        if (FilterOus) { filters.Add("organizationalUnit"); }
        return filters.Count > 0 ? filters : null;
    }

    /// <summary>
    /// Gets currently locked-out user accounts.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [RelayCommand]
    private async Task GetLockedAccountsAsync()
    {
        await ExecuteSecurityFilterAsync(
            _adService.GetLockedAccountsAsync,
            "Locked Accounts");
    }

    /// <summary>
    /// Gets disabled computer accounts.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [RelayCommand]
    private async Task GetDisabledComputersAsync()
    {
        await ExecuteSecurityFilterAsync(
            _adService.GetDisabledComputersAsync,
            "Disabled Computers");
    }

    /// <summary>
    /// Gets stale computer accounts based on the configurable threshold.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [RelayCommand]
    private async Task GetStaleComputersAsync()
    {
        try
        {
            StaleThresholdDays = await _settingsService.GetTypedAsync(
                "StaleComputerThresholdDays",
                AppConstants.DefaultStaleComputerDays,
                _cts.Token);
        }
        catch (Exception ex)
        {
            _logger.Warning(ex, "Failed to load stale threshold setting, using default");
            StaleThresholdDays = AppConstants.DefaultStaleComputerDays;
        }

        await ExecuteSecurityFilterAsync(
            ct => _adService.GetStaleComputersAsync(StaleThresholdDays, ct),
            $"Stale Computers ({StaleThresholdDays} days)");
    }

    /// <summary>
    /// Gets all domain controllers in the active domain.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [RelayCommand]
    private async Task GetDomainControllersAsync()
    {
        PushSearchState();
        IsSearching = true;
        ResultStatus = "Loading domain controllers...";

        try
        {
            IReadOnlyList<string> dcs = await _adService.GetDomainControllersAsync(_cts.Token);
            SearchResults = new ObservableCollection<AdObject>(dcs.Select(dc => new AdObject
            {
                Name = dc,
                DistinguishedName = dc,
                ObjectClass = "computer",
                DisplayName = dc
            }));

            ResultStatus = $"{dcs.Count} domain controllers found";
        }
        catch (OperationCanceledException)
        {
            ResultStatus = "Operation cancelled.";
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to load domain controllers");
            ResultStatus = $"Failed to load domain controllers: {ex.Message}";
        }
        finally
        {
            IsSearching = false;
        }
    }

    /// <summary>
    /// Sends computer objects from the search results to the execution target list.
    /// </summary>
    [RelayCommand]
    private void SendToExecutionTargets()
    {
        var computers = SearchResults
            .Where(o => o.ObjectClass.Equals("computer", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (computers.Count == 0)
        {
            _dialogService.ShowInfo("No Computers", "No computer objects are available to send.");
            return;
        }

        _hostTargetingService.AddFromAdSearchResults(computers);
        ResultStatus = $"Sent {computers.Count} computers to Execution Targets.";
        _logger.Information("Sent {Count} AD computers to execution targets", computers.Count);
    }

    /// <summary>
    /// Shows the members of the currently selected group object.
    /// </summary>
    [RelayCommand]
    private async Task ShowGroupMembersAsync()
    {
        if (SelectedObject is null ||
            !SelectedObject.ObjectClass.Equals("group", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        PushSearchState();
        IsSearching = true;
        ResultStatus = "Loading group members...";

        try
        {
            AdSearchResult result = await _adService.GetGroupMembersAsync(
                SelectedObject.DistinguishedName, recursive: true, _cts.Token);

            SearchResults = new ObservableCollection<AdObject>(result.Results);
            ResultStatus = $"{result.TotalResultCount} members found in {result.ExecutionTime.TotalMilliseconds:F0}ms";
        }
        catch (OperationCanceledException)
        {
            ResultStatus = "Operation cancelled.";
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to load group members for {DN}", SelectedObject.DistinguishedName);
            ResultStatus = $"Failed to load group members: {ex.Message}";
        }
        finally
        {
            IsSearching = false;
        }
    }

    /// <summary>
    /// Scopes the search to the selected object's OU.
    /// </summary>
    [RelayCommand]
    private void ScopeToSelectedOu()
    {
        if (SelectedObject is null)
        {
            return;
        }

        string dn = SelectedObject.DistinguishedName;
        if (SelectedObject.ObjectClass.Equals("organizationalUnit", StringComparison.OrdinalIgnoreCase) ||
            SelectedObject.ObjectClass.Equals("container", StringComparison.OrdinalIgnoreCase))
        {
            SetScope(dn);
        }
        else
        {
            int commaIndex = dn.IndexOf(',');
            if (commaIndex > 0)
            {
                SetScope(dn[(commaIndex + 1)..]);
            }
        }
    }

    /// <summary>
    /// Toggles the collapsed state of Zone 1 (OU Navigator).
    /// </summary>
    [RelayCommand]
    private void ToggleZone1() => IsZone1Collapsed = !IsZone1Collapsed;

    /// <summary>
    /// Toggles the collapsed state of Zone 3 (Detail Inspector).
    /// </summary>
    [RelayCommand]
    private void ToggleZone3() => IsZone3Collapsed = !IsZone3Collapsed;

    /// <summary>
    /// Expands the Detail Inspector to overlay the workspace.
    /// </summary>
    [RelayCommand]
    private void ExpandInspector() => IsInspectorExpanded = true;

    /// <summary>
    /// Collapses the Detail Inspector back to its normal width.
    /// </summary>
    [RelayCommand]
    private void CollapseInspector() => IsInspectorExpanded = false;

    /// <summary>
    /// Toggles a specific object class filter pill.
    /// </summary>
    /// <param name="objectClass">The object class filter to toggle (Users, Computers, Groups, OUs, Contacts, All).</param>
    [RelayCommand]
    private void ToggleFilter(string? objectClass)
    {
        if (objectClass == "All")
        {
            FilterAll = true;
            FilterUsers = false;
            FilterComputers = false;
            FilterGroups = false;
            FilterOus = false;
            SearchCommand.Execute(null);
            return;
        }

        FilterAll = false;

        switch (objectClass)
        {
            case "user": FilterUsers = !FilterUsers; break;
            case "computer": FilterComputers = !FilterComputers; break;
            case "group": FilterGroups = !FilterGroups; break;
            case "organizationalUnit": FilterOus = !FilterOus; break;
            default: break;
        }

        if (!FilterUsers && !FilterComputers && !FilterGroups && !FilterOus)
        {
            FilterAll = true;
        }

        SearchCommand.Execute(null);
    }

    /// <summary>
    /// Resets the search scope to the domain root.
    /// </summary>
    [RelayCommand]
    private void ResetScope()
    {
        ScopeDisplay = string.Empty;
        SearchEntireDomain = false;
        BreadcrumbSegments.Clear();
    }

    /// <summary>
    /// Sets the search scope to the specified OU distinguished name.
    /// </summary>
    /// <param name="distinguishedName">The DN of the OU to scope searches to.</param>
    [RelayCommand]
    private void SetScope(string? distinguishedName)
    {
        if (string.IsNullOrEmpty(distinguishedName))
        {
            return;
        }

        ScopeDisplay = distinguishedName;
        SearchEntireDomain = false;
        UpdateBreadcrumbs(distinguishedName);
    }

    /// <summary>
    /// Navigates the search scope to a breadcrumb segment.
    /// </summary>
    /// <param name="dn">The distinguished name to navigate to.</param>
    [RelayCommand]
    private void NavigateToBreadcrumb(string? dn)
    {
        if (!string.IsNullOrEmpty(dn))
        {
            SetScope(dn);
        }
    }

    private void UpdateBreadcrumbs(string dn)
    {
        var segments = new ObservableCollection<BreadcrumbSegment>();
        string remaining = dn;

        while (!string.IsNullOrEmpty(remaining))
        {
            int commaIndex = remaining.IndexOf(',');
            string label = commaIndex > 0 ? remaining[..commaIndex] : remaining;
            segments.Add(new BreadcrumbSegment(label, remaining));

            if (commaIndex < 0)
            {
                break;
            }

            remaining = remaining[(commaIndex + 1)..];
        }

        BreadcrumbSegments = segments;
    }

    partial void OnSelectedObjectChanged(AdObject? value)
    {
        if (value is not null)
        {
            LoadObjectDetailAsync(value.DistinguishedName).SafeFireAndForget(_logger);
        }
        else
        {
            SelectedObjectAttributes.Clear();
            SelectedObjectGroups.Clear();
            SelectedObjectPermissions.Clear();
        }
    }

    partial void OnSearchTextChanged(string value)
    {
        _searchDebounceTimer?.Cancel();

        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        _searchDebounceTimer = new CancellationTokenSource();
        DebounceSearchAsync(_searchDebounceTimer.Token).SafeFireAndForget(_logger);
    }

    private async Task DebounceSearchAsync(CancellationToken token)
    {
        try
        {
            await Task.Delay(300, token);
            SearchCommand.Execute(null);
        }
        catch (OperationCanceledException)
        {
            // Debounce cancelled — expected when user types quickly
        }
    }

    private async Task LoadObjectDetailAsync(string dn)
    {
        try
        {
            Task<AdObject> detailTask = _adService.GetObjectDetailAsync(dn, _cts.Token);
            Task<IReadOnlyList<string>> groupsTask = _adService.GetGroupMembershipAsync(
                dn, recursive: false, _cts.Token);
            Task<IReadOnlyList<AdAccessControlEntry>> aclTask = _adService.GetObjectAclAsync(dn, _cts.Token);
            await Task.WhenAll(detailTask, groupsTask, aclTask);

            SelectedObjectAttributes = new ObservableCollection<KeyValuePair<string, string>>(
                detailTask.Result.Attributes
                    .OrderBy(a => a.Key)
                    .Select(a => new KeyValuePair<string, string>(a.Key, a.Value?.ToString() ?? string.Empty)));

            SelectedObjectGroups = new ObservableCollection<string>(
                groupsTask.Result.Distinct(StringComparer.OrdinalIgnoreCase));

            SelectedObjectPermissions = new ObservableCollection<AdAccessControlEntry>(aclTask.Result);
        }
        catch (OperationCanceledException)
        {
            // Cancelled — no action needed
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to load detail for {DN}", dn);
        }
    }

    private async Task ExecuteSecurityFilterAsync(
        Func<CancellationToken, Task<AdSearchResult>> filterFunc,
        string filterName)
    {
        PushSearchState();
        IsSearching = true;
        ResultStatus = $"Loading {filterName}...";

        try
        {
            AdSearchResult result = await filterFunc(_cts.Token);
            SearchResults = new ObservableCollection<AdObject>(result.Results);

            ResultStatus = $"{result.TotalResultCount} {filterName} found in {result.ExecutionTime.TotalMilliseconds:F0}ms";
        }
        catch (OperationCanceledException)
        {
            ResultStatus = "Operation cancelled.";
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to load {FilterName}", filterName);
            ResultStatus = $"Failed to load {filterName}: {ex.Message}";
        }
        finally
        {
            IsSearching = false;
        }
    }

    private static AdTreeNode MapToTreeNode(AdObject adObject) =>
        new()
        {
            Name = adObject.Name,
            DistinguishedName = adObject.DistinguishedName,
            ObjectClass = adObject.ObjectClass,
            HasDummyChild = true
        };

    private static readonly HashSet<string> NavigableObjectClasses = new(StringComparer.OrdinalIgnoreCase)
    {
        "organizationalUnit",
        "container",
        "builtinDomain",
        "domainDNS",
        "domain"
    };

    private static bool IsNavigableTreeObject(AdObject obj) =>
        NavigableObjectClasses.Contains(obj.ObjectClass);

    partial void OnTreeFilterTextChanged(string value) => ApplyTreeFilter();

    private void ApplyTreeFilter()
    {
        if (string.IsNullOrWhiteSpace(TreeFilterText))
        {
            FilteredTreeNodes = new ObservableCollection<AdTreeNode>(TreeNodes);
            return;
        }

        FilteredTreeNodes = new ObservableCollection<AdTreeNode>(
            FilterNodesRecursive(TreeNodes, TreeFilterText));
    }

    private static IEnumerable<AdTreeNode> FilterNodesRecursive(
        IEnumerable<AdTreeNode> nodes, string filter)
    {
        foreach (AdTreeNode node in nodes)
        {
            bool nameMatches = node.Name.Contains(filter, StringComparison.OrdinalIgnoreCase);
            var matchingChildren = FilterNodesRecursive(node.Children, filter).ToList();

            if (nameMatches || matchingChildren.Count > 0)
            {
                var clone = new AdTreeNode
                {
                    Name = node.Name,
                    DistinguishedName = node.DistinguishedName,
                    ObjectClass = node.ObjectClass,
                    HasDummyChild = node.HasDummyChild
                };

                foreach (AdTreeNode child in matchingChildren)
                {
                    clone.Children.Add(child);
                }

                if (matchingChildren.Count > 0)
                {
                    clone.IsExpanded = true;
                }

                yield return clone;
            }
        }
    }

    /// <summary>
    /// Copies the distinguished name of a tree node to the clipboard.
    /// </summary>
    /// <param name="distinguishedName">The DN to copy.</param>
    [RelayCommand]
    private void CopyOuPath(string? distinguishedName)
    {
        if (string.IsNullOrEmpty(distinguishedName))
        {
            return;
        }

        _dialogService.SetClipboardText(distinguishedName);
        ResultStatus = "Copied OU path to clipboard";
    }

    /// <summary>
    /// Toggles search history popup visibility.
    /// </summary>
    [RelayCommand]
    private void ToggleSearchHistory()
    {
        IsSearchHistoryOpen = !IsSearchHistoryOpen;
        IsSavedSearchesOpen = false;
    }

    /// <summary>
    /// Executes a search history entry by restoring its search state.
    /// </summary>
    /// <param name="entry">The history entry to execute.</param>
    [RelayCommand]
    private async Task ExecuteHistoryEntryAsync(SearchHistoryEntry? entry)
    {
        if (entry is null)
        {
            return;
        }

        IsSearchHistoryOpen = false;
        RestoreSearchState(entry.QueryText, entry.SelectedAttribute, entry.ActiveFilters, entry.ScopeDn, entry.IsLdapFilterMode);
        await SearchCommand.ExecuteAsync(null);
    }

    /// <summary>
    /// Clears all search history entries.
    /// </summary>
    [RelayCommand]
    private async Task ClearSearchHistoryAsync()
    {
        SearchHistory.Clear();
        IsSearchHistoryOpen = false;
        await _settingsService.SetAsync(AppConstants.SearchHistoryKey, "[]", _cts.Token);
    }

    /// <summary>
    /// Toggles saved searches popup visibility.
    /// </summary>
    [RelayCommand]
    private void ToggleSavedSearches()
    {
        IsSavedSearchesOpen = !IsSavedSearchesOpen;
        IsSearchHistoryOpen = false;
    }

    /// <summary>
    /// Saves the current search configuration as a named saved search.
    /// </summary>
    [RelayCommand]
    private async Task SaveCurrentSearchAsync()
    {
        string queryText = IsLdapFilterMode ? RawLdapFilter : SearchText;
        if (string.IsNullOrWhiteSpace(queryText))
        {
            _dialogService.ShowInfo("Nothing to Save", "Enter a search query first.");
            return;
        }

        string? name = await _dialogService.ShowInputDialogAsync(
            "Save Search", "Enter a name for this search:", queryText);

        if (string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        List<string> activeFilters = GetActiveFilterNames();
        var saved = new SavedSearch(
            Guid.NewGuid().ToString(),
            name,
            queryText,
            SelectedAttribute,
            activeFilters,
            string.IsNullOrEmpty(ScopeDisplay) ? null : ScopeDisplay,
            IsLdapFilterMode,
            DateTime.UtcNow);

        SavedSearches.Add(saved);
        IsSavedSearchesOpen = false;
        await PersistSavedSearchesAsync();
    }

    /// <summary>
    /// Executes a saved search by restoring its state and running the query.
    /// </summary>
    /// <param name="search">The saved search to execute.</param>
    [RelayCommand]
    private async Task ExecuteSavedSearchAsync(SavedSearch? search)
    {
        if (search is null)
        {
            return;
        }

        IsSavedSearchesOpen = false;
        RestoreSearchState(search.QueryText, search.SelectedAttribute, search.ActiveFilters, search.ScopeDn, search.IsLdapFilterMode);
        await SearchCommand.ExecuteAsync(null);
    }

    /// <summary>
    /// Deletes a saved search.
    /// </summary>
    /// <param name="search">The saved search to delete.</param>
    [RelayCommand]
    private async Task DeleteSavedSearchAsync(SavedSearch? search)
    {
        if (search is null)
        {
            return;
        }

        _ = SavedSearches.Remove(search);
        await PersistSavedSearchesAsync();
    }

    /// <summary>
    /// Renames a saved search.
    /// </summary>
    /// <param name="search">The saved search to rename.</param>
    [RelayCommand]
    private async Task RenameSavedSearchAsync(SavedSearch? search)
    {
        if (search is null)
        {
            return;
        }

        string? newName = await _dialogService.ShowInputDialogAsync(
            "Rename Search", "Enter a new name:", search.Name);

        if (string.IsNullOrWhiteSpace(newName))
        {
            return;
        }

        int index = SavedSearches.IndexOf(search);
        if (index >= 0)
        {
            SavedSearches[index] = search with { Name = newName };
            await PersistSavedSearchesAsync();
        }
    }

    /// <summary>
    /// Toggles between simple search and raw LDAP filter mode.
    /// </summary>
    [RelayCommand]
    private void ToggleLdapFilterMode()
    {
        if (!IsLdapFilterMode)
        {
            _preservedSearchText = SearchText;
            _preservedSelectedAttribute = SelectedAttribute;
            _preservedFilterAll = FilterAll;
            _preservedFilterUsers = FilterUsers;
            _preservedFilterComputers = FilterComputers;
            _preservedFilterGroups = FilterGroups;
            _preservedFilterOus = FilterOus;
            IsLdapFilterMode = true;
        }
        else
        {
            IsLdapFilterMode = false;
            SearchText = _preservedSearchText;
            SelectedAttribute = _preservedSelectedAttribute;
            FilterAll = _preservedFilterAll;
            FilterUsers = _preservedFilterUsers;
            FilterComputers = _preservedFilterComputers;
            FilterGroups = _preservedFilterGroups;
            FilterOus = _preservedFilterOus;
        }
    }

    /// <summary>
    /// Shows the export format menu.
    /// </summary>
    [RelayCommand]
    private void ShowExportMenu() =>
        IsExportMenuOpen = !IsExportMenuOpen;

    /// <summary>
    /// Toggles the DataGrid column picker popup.
    /// </summary>
    [RelayCommand]
    private void ToggleColumnPicker() =>
        IsColumnPickerOpen = !IsColumnPickerOpen;

    /// <summary>
    /// Toggles visibility of a specific DataGrid column and persists the choice.
    /// </summary>
    /// <param name="config">The column config to toggle.</param>
    [RelayCommand]
    private async Task ToggleColumnVisibilityAsync(DataGridColumnConfig? config)
    {
        if (config is null)
        {
            return;
        }

        config.IsVisible = !config.IsVisible;
        await PersistColumnVisibilityAsync();
    }

    /// <summary>
    /// Shows the members of the specified group by its distinguished name.
    /// </summary>
    /// <param name="groupDn">The distinguished name of the group.</param>
    [RelayCommand]
    private async Task ShowGroupMembersFromListAsync(string? groupDn)
    {
        if (string.IsNullOrEmpty(groupDn))
        {
            return;
        }

        PushSearchState();
        IsSearching = true;
        ResultStatus = "Loading group members...";

        try
        {
            AdSearchResult result = await _adService.GetGroupMembersAsync(
                groupDn, recursive: true, _cts.Token);

            SearchResults = new ObservableCollection<AdObject>(result.Results);
            ResultStatus = $"{result.TotalResultCount} members found in {result.ExecutionTime.TotalMilliseconds:F0}ms";
        }
        catch (OperationCanceledException)
        {
            ResultStatus = "Operation cancelled.";
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to load group members for {DN}", groupDn);
            ResultStatus = $"Failed to load group members: {ex.Message}";
        }
        finally
        {
            IsSearching = false;
        }
    }

    /// <summary>
    /// Navigates back to the previous search state (undo).
    /// </summary>
    [RelayCommand]
    private void GoBack()
    {
        if (_undoStack.Count == 0)
        {
            return;
        }

        SearchStateSnapshot snapshot = _undoStack[^1];
        _undoStack.RemoveAt(_undoStack.Count - 1);
        CanGoBack = _undoStack.Count > 0;

        SearchResults = snapshot.Results;
        ResultStatus = snapshot.ResultStatus;
        SearchText = snapshot.SearchText;
        SelectedAttribute = snapshot.SelectedAttribute;
        IsLdapFilterMode = snapshot.IsLdapFilterMode;
        RawLdapFilter = snapshot.RawLdapFilter;
        SearchEntireDomain = snapshot.SearchEntireDomain;
        FilterAll = snapshot.FilterAll;
        FilterUsers = snapshot.FilterUsers;
        FilterComputers = snapshot.FilterComputers;
        FilterGroups = snapshot.FilterGroups;
        FilterOus = snapshot.FilterOus;

        if (!string.IsNullOrEmpty(snapshot.ScopeDisplay))
        {
            ScopeDisplay = snapshot.ScopeDisplay;
            UpdateBreadcrumbs(snapshot.ScopeDisplay);
        }
        else
        {
            ScopeDisplay = string.Empty;
            BreadcrumbSegments.Clear();
        }
    }

    /// <summary>
    /// Starts the CSV export flow with column picker.
    /// </summary>
    [RelayCommand]
    private void StartCsvExport()
    {
        IsExportMenuOpen = false;
        PopulateExportColumns();
        _pendingExportFormat = "csv";
        IsExportColumnPickerOpen = true;
    }

    /// <summary>
    /// Starts the Excel export flow with column picker.
    /// </summary>
    [RelayCommand]
    private void StartExcelExport()
    {
        IsExportMenuOpen = false;
        PopulateExportColumns();
        _pendingExportFormat = "excel";
        IsExportColumnPickerOpen = true;
    }

    /// <summary>
    /// Confirms the selected columns and performs the export.
    /// </summary>
    [RelayCommand]
    private async Task ConfirmExportAsync()
    {
        IsExportColumnPickerOpen = false;

        List<string> selectedColumns = [.. AvailableExportColumns
            .Where(c => c.IsSelected)
            .Select(c => c.ColumnName)];

        if (selectedColumns.Count == 0)
        {
            _dialogService.ShowInfo("No Columns", "Select at least one column to export.");
            return;
        }

        List<AdObject> objects = [.. SearchResults];

        if (_pendingExportFormat == "csv")
        {
            string? filePath = await _dialogService.ShowSaveFileDialogAsync(".csv", "CSV files|*.csv");
            if (filePath is null)
            {
                return;
            }

            await _exportService.ExportAdObjectsToCsvAsync(objects, filePath, selectedColumns, _cts.Token);
            ResultStatus = $"Exported {objects.Count} objects to CSV.";
        }
        else if (_pendingExportFormat == "excel")
        {
            string? filePath = await _dialogService.ShowSaveFileDialogAsync(".xlsx", "Excel files|*.xlsx");
            if (filePath is null)
            {
                return;
            }

            await _exportService.ExportAdObjectsToExcelAsync(objects, filePath, selectedColumns, _cts.Token);
            ResultStatus = $"Exported {objects.Count} objects to Excel.";
        }
    }

    /// <summary>
    /// Cancels the export column picker.
    /// </summary>
    [RelayCommand]
    private void CancelExport() =>
        IsExportColumnPickerOpen = false;

    /// <summary>
    /// Selects all export columns.
    /// </summary>
    [RelayCommand]
    private void SelectAllExportColumns()
    {
        foreach (ExportColumnSelection col in AvailableExportColumns)
        {
            col.IsSelected = true;
        }
    }

    /// <summary>
    /// Deselects all export columns.
    /// </summary>
    [RelayCommand]
    private void DeselectAllExportColumns()
    {
        foreach (ExportColumnSelection col in AvailableExportColumns)
        {
            col.IsSelected = false;
        }
    }

    /// <summary>
    /// Copies search results to the clipboard as tab-delimited text.
    /// </summary>
    [RelayCommand]
    private void CopyToClipboard()
    {
        if (SearchResults.Count == 0)
        {
            return;
        }

        var sb = new StringBuilder();
        _ = sb.AppendLine("Name\tObjectClass\tDescription\tDistinguishedName");

        foreach (AdObject obj in SearchResults)
        {
            _ = sb.Append(CultureInfo.InvariantCulture, $"{obj.Name}\t{obj.ObjectClass}\t{obj.Description ?? string.Empty}\t{obj.DistinguishedName}")
                  .AppendLine();
        }

        try
        {
            _dialogService.SetClipboardText(sb.ToString());
            ResultStatus = $"Copied {SearchResults.Count} objects to clipboard.";
        }
        catch (Exception ex)
        {
            _logger.Warning(ex, "Failed to copy to clipboard");
            ResultStatus = "Failed to copy to clipboard.";
        }
    }

    private void PopulateExportColumns()
    {
        var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Name",
            "ObjectClass",
            "Description",
            "DistinguishedName"
        };

        foreach (AdObject obj in SearchResults)
        {
            foreach (string key in obj.Attributes.Keys)
            {
                _ = columns.Add(key);
            }
        }

        AvailableExportColumns = new ObservableCollection<ExportColumnSelection>(
            columns.OrderBy(c => c, StringComparer.OrdinalIgnoreCase)
                   .Select(c => new ExportColumnSelection { ColumnName = c, IsSelected = true }));
    }

    private async Task RecordSearchHistoryAsync(string queryText, int resultCount)
    {
        try
        {
            List<string> activeFilters = GetActiveFilterNames();

            var entry = new SearchHistoryEntry(
                queryText,
                SelectedAttribute,
                activeFilters,
                string.IsNullOrEmpty(ScopeDisplay) ? null : ScopeDisplay,
                IsLdapFilterMode,
                resultCount,
                DateTime.UtcNow);

            SearchHistory.Insert(0, entry);

            while (SearchHistory.Count > AppConstants.MaxSearchHistoryCount)
            {
                SearchHistory.RemoveAt(SearchHistory.Count - 1);
            }

            string json = JsonSerializer.Serialize<List<SearchHistoryEntry>>([.. SearchHistory]);
            await _settingsService.SetAsync(AppConstants.SearchHistoryKey, json, _cts.Token);
        }
        catch (Exception ex)
        {
            _logger.Warning(ex, "Failed to persist search history");
        }
    }

    private List<string> GetActiveFilterNames()
    {
        if (FilterAll)
        {
            return ["All"];
        }

        var names = new List<string>();
        if (FilterUsers) { names.Add("user"); }
        if (FilterComputers) { names.Add("computer"); }
        if (FilterGroups) { names.Add("group"); }
        if (FilterOus) { names.Add("organizationalUnit"); }
        return names;
    }

    private void RestoreSearchState(
        string queryText,
        string selectedAttribute,
        IReadOnlyList<string> activeFilters,
        string? scopeDn,
        bool isLdapFilterMode)
    {
        if (isLdapFilterMode)
        {
            IsLdapFilterMode = true;
            RawLdapFilter = queryText;
        }
        else
        {
            IsLdapFilterMode = false;
            SearchText = queryText;
        }

        SelectedAttribute = selectedAttribute;

        FilterAll = activeFilters.Contains("All") || activeFilters.Count == 0;
        FilterUsers = activeFilters.Contains("user");
        FilterComputers = activeFilters.Contains("computer");
        FilterGroups = activeFilters.Contains("group");
        FilterOus = activeFilters.Contains("organizationalUnit");

        if (!string.IsNullOrEmpty(scopeDn))
        {
            SetScope(scopeDn);
        }
        else
        {
            ResetScope();
        }
    }

    private async Task PersistSavedSearchesAsync()
    {
        try
        {
            string json = JsonSerializer.Serialize<List<SavedSearch>>([.. SavedSearches]);
            await _settingsService.SetAsync(AppConstants.SavedSearchesKey, json, _cts.Token);
        }
        catch (Exception ex)
        {
            _logger.Warning(ex, "Failed to persist saved searches");
        }
    }

    private async Task LoadPersistedStateAsync()
    {
        try
        {
            string historyJson = await _settingsService.GetAsync(AppConstants.SearchHistoryKey, "[]", _cts.Token);
            List<SearchHistoryEntry>? history = JsonSerializer.Deserialize<List<SearchHistoryEntry>>(historyJson);
            if (history is not null)
            {
                SearchHistory = new ObservableCollection<SearchHistoryEntry>(history);
            }

            string savedJson = await _settingsService.GetAsync(AppConstants.SavedSearchesKey, "[]", _cts.Token);
            List<SavedSearch>? saved = JsonSerializer.Deserialize<List<SavedSearch>>(savedJson);
            if (saved is not null)
            {
                SavedSearches = new ObservableCollection<SavedSearch>(saved);
            }

            await LoadColumnVisibilityAsync();
        }
        catch (Exception ex)
        {
            _logger.Warning(ex, "Failed to load persisted search state");
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _searchDebounceTimer?.Dispose();
        _cts.Dispose();
        GC.SuppressFinalize(this);
    }

    partial void OnGroupFilterTextChanged(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            FilteredGroups = new ObservableCollection<string>(SelectedObjectGroups);
        }
        else
        {
            FilteredGroups = new ObservableCollection<string>(
                SelectedObjectGroups.Where(g => g.Contains(value, StringComparison.OrdinalIgnoreCase)));
        }
    }

    partial void OnSelectedObjectGroupsChanged(ObservableCollection<string> value)
    {
        GroupFilterText = string.Empty;
        FilteredGroups = new ObservableCollection<string>(value);
    }

    private void PushSearchState()
    {
        _undoStack.Add(new SearchStateSnapshot(
            new ObservableCollection<AdObject>(SearchResults),
            ResultStatus,
            SearchText,
            SelectedAttribute,
            IsLdapFilterMode,
            RawLdapFilter,
            string.IsNullOrEmpty(ScopeDisplay) ? null : ScopeDisplay,
            SearchEntireDomain,
            FilterAll,
            FilterUsers,
            FilterComputers,
            FilterGroups,
            FilterOus));

        while (_undoStack.Count > AppConstants.MaxSearchUndoDepth)
        {
            _undoStack.RemoveAt(0);
        }

        CanGoBack = true;
    }

    private void InitializeDataGridColumns()
    {
        DataGridColumns =
        [
            new DataGridColumnConfig { Header = "Name", PropertyName = "Name" },
            new DataGridColumnConfig { Header = "Class", PropertyName = "ObjectClass" },
            new DataGridColumnConfig { Header = "Description", PropertyName = "Description" },
            new DataGridColumnConfig { Header = "Distinguished Name", PropertyName = "DistinguishedName" }
        ];
    }

    private async Task PersistColumnVisibilityAsync()
    {
        try
        {
            var visibility = DataGridColumns.ToDictionary(c => c.PropertyName, c => c.IsVisible);
            string json = JsonSerializer.Serialize(visibility);
            await _settingsService.SetAsync(AppConstants.ColumnVisibilityKey, json, _cts.Token);
        }
        catch (Exception ex)
        {
            _logger.Warning(ex, "Failed to persist column visibility");
        }
    }

    private async Task LoadColumnVisibilityAsync()
    {
        try
        {
            string json = await _settingsService.GetAsync(AppConstants.ColumnVisibilityKey, "{}", _cts.Token);
            Dictionary<string, bool>? visibility = JsonSerializer.Deserialize<Dictionary<string, bool>>(json);
            if (visibility is not null)
            {
                foreach (DataGridColumnConfig col in DataGridColumns)
                {
                    if (visibility.TryGetValue(col.PropertyName, out bool isVisible))
                    {
                        col.IsVisible = isVisible;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.Warning(ex, "Failed to load column visibility");
        }
    }
}

/// <summary>
/// Represents a node in the Active Directory tree view with lazy-loading support.
/// </summary>
public partial class AdTreeNode : ObservableObject
{
    /// <summary>
    /// Gets the display name of the tree node.
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Gets the full distinguished name of the AD object.
    /// </summary>
    public string DistinguishedName { get; init; } = string.Empty;

    /// <summary>
    /// Gets the AD object class (e.g., "organizationalUnit", "container").
    /// </summary>
    public string ObjectClass { get; init; } = string.Empty;

    /// <summary>
    /// Gets the child nodes of this tree node.
    /// </summary>
    public ObservableCollection<AdTreeNode> Children { get; } = [];

    [ObservableProperty]
    private bool _isExpanded;

    /// <summary>
    /// Gets or sets a value indicating whether this node has a dummy child for lazy-load expand arrow display.
    /// </summary>
    public bool HasDummyChild { get; set; } = true;
}
