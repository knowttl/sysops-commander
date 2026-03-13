using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Serilog;
using SysOpsCommander.Core.Constants;
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
    private bool _isSearching;

    [ObservableProperty]
    private int _staleThresholdDays = AppConstants.DefaultStaleComputerDays;

    [ObservableProperty]
    private string _resultStatus = string.Empty;

    [ObservableProperty]
    private string _treeFilterText = string.Empty;

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
    private bool _filterContacts;

    [ObservableProperty]
    private bool _filterAll = true;

    [ObservableProperty]
    private bool _searchEntireDomain;

    [ObservableProperty]
    private string _scopeDisplay = string.Empty;

    [ObservableProperty]
    private ObservableCollection<BreadcrumbSegment> _breadcrumbSegments = [];

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
    /// <param name="logger">The Serilog logger.</param>
    public AdExplorerViewModel(
        IActiveDirectoryService adService,
        IHostTargetingService hostTargetingService,
        ISettingsService settingsService,
        IDialogService dialogService,
        ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(adService);
        ArgumentNullException.ThrowIfNull(hostTargetingService);
        ArgumentNullException.ThrowIfNull(settingsService);
        ArgumentNullException.ThrowIfNull(dialogService);
        ArgumentNullException.ThrowIfNull(logger);

        _adService = adService;
        _hostTargetingService = hostTargetingService;
        _settingsService = settingsService;
        _dialogService = dialogService;
        _logger = logger;
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

            TreeNodes = new ObservableCollection<AdTreeNode>(rootChildren.Select(MapToTreeNode));
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

        node.HasDummyChild = false;
        node.Children.Clear();

        try
        {
            IReadOnlyList<AdObject> children = await _adService.BrowseChildrenAsync(
                node.DistinguishedName, _cts.Token);

            foreach (AdObject child in children)
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
        if (string.IsNullOrWhiteSpace(SearchText))
        {
            return;
        }

        IsSearching = true;
        ResultStatus = "Searching...";

        try
        {
            string? attribute = SelectedAttribute == "All attributes" ? null : SelectedAttribute;
            IReadOnlyList<string>? objectClasses = GetActiveObjectClassFilters();
            string? baseDn = SearchEntireDomain ? null : (string.IsNullOrEmpty(ScopeDisplay) ? null : ScopeDisplay);

            AdSearchResult result = await _adService.SearchScopedAsync(
                SearchText, baseDn, objectClasses, attribute, _cts.Token);

            SearchResults = new ObservableCollection<AdObject>(result.Results);
            ResultStatus = $"{result.TotalResultCount} results found in {result.ExecutionTime.TotalMilliseconds:F0}ms";
        }
        catch (OperationCanceledException)
        {
            ResultStatus = "Search cancelled.";
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "AD search failed for term: {SearchTerm}", SearchText);
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
        if (FilterContacts) { filters.Add("contact"); }
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
            FilterContacts = false;
            return;
        }

        FilterAll = false;

        switch (objectClass)
        {
            case "user": FilterUsers = !FilterUsers; break;
            case "computer": FilterComputers = !FilterComputers; break;
            case "group": FilterGroups = !FilterGroups; break;
            case "organizationalUnit": FilterOus = !FilterOus; break;
            case "contact": FilterContacts = !FilterContacts; break;
            default: break;
        }

        // If no individual filters are active, reactivate All
        if (!FilterUsers && !FilterComputers && !FilterGroups && !FilterOus && !FilterContacts)
        {
            FilterAll = true;
        }
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
            _ = LoadObjectDetailAsync(value.DistinguishedName);
        }
        else
        {
            SelectedObjectAttributes.Clear();
            SelectedObjectGroups.Clear();
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
        _ = DebounceSearchAsync(_searchDebounceTimer.Token);
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
                dn, recursive: true, _cts.Token);
            await Task.WhenAll(detailTask, groupsTask);

            SelectedObjectAttributes = new ObservableCollection<KeyValuePair<string, string>>(
                detailTask.Result.Attributes
                    .OrderBy(a => a.Key)
                    .Select(a => new KeyValuePair<string, string>(a.Key, a.Value?.ToString() ?? string.Empty)));

            SelectedObjectGroups = new ObservableCollection<string>(groupsTask.Result);
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

    /// <inheritdoc />
    public void Dispose()
    {
        _searchDebounceTimer?.Dispose();
        _cts.Dispose();
        GC.SuppressFinalize(this);
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
