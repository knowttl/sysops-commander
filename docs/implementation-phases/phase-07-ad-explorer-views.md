# Phase 7: AD Explorer Views

> **Goal:** Wire the AD service layer into the UI — tree browsing, search, attribute detail, pre-built security filters, group membership, and the Dashboard "Quick Connect" feature. All views are domain-aware and use configurable thresholds.
>
> **Prereqs:** Phase 6 complete (UI shell, navigation, dialog infrastructure). Phase 3 `ActiveDirectoryService` fully implemented.
>
> **Outputs:** Fully functional `AdExplorerView`, `AdSearchViewModel`, and `DashboardView` with Quick Connect.

---

## Sub-Steps

### 7.1 — Implement `AdExplorerViewModel.cs` — Properties and State

**File:** `src/SysOpsCommander.ViewModels/AdExplorerViewModel.cs`

**Dependencies (injected):**
- `IActiveDirectoryService`
- `IHostTargetingService`
- `ISettingsService`
- `IDialogService`
- `Serilog.ILogger`

**Observable properties:**
```csharp
[ObservableProperty]
private string _searchText = string.Empty;

[ObservableProperty]
private string _activeDomainDisplay = string.Empty;

[ObservableProperty]
private ObservableCollection<AdTreeNode> _treeNodes = new();

[ObservableProperty]
private ObservableCollection<AdObject> _searchResults = new();

[ObservableProperty]
private AdObject? _selectedObject;

[ObservableProperty]
private ObservableCollection<KeyValuePair<string, string>> _selectedObjectAttributes = new();

[ObservableProperty]
private ObservableCollection<string> _selectedObjectGroups = new();

[ObservableProperty]
private bool _isSearching;

[ObservableProperty]
private int _staleThresholdDays;

[ObservableProperty]
private string _resultStatus = string.Empty;
```

**`AdTreeNode` helper class:**
```csharp
public class AdTreeNode : ObservableObject
{
    public string Name { get; init; } = string.Empty;
    public string DistinguishedName { get; init; } = string.Empty;
    public string ObjectClass { get; init; } = string.Empty;
    public ObservableCollection<AdTreeNode> Children { get; } = new();
    public bool IsExpanded { get; set; }
    public bool HasDummyChild { get; set; } = true; // For lazy loading
}
```

**Commit:** `feat(viewmodels): implement AdExplorerViewModel properties and state`

---

### 7.2 — Implement AD Tree Browse — Lazy Loading

**Commands:**
```csharp
[RelayCommand]
private async Task LoadTreeRootAsync()
{
    var domain = _adService.GetActiveDomain();
    ActiveDomainDisplay = domain?.DomainName ?? "Unknown";

    var rootChildren = await _adService.BrowseChildrenAsync(domain.RootDistinguishedName, _cts.Token);
    TreeNodes.Clear();
    foreach (var child in rootChildren)
    {
        var node = MapToTreeNode(child);
        node.HasDummyChild = true;
        TreeNodes.Add(node);
    }
}

[RelayCommand]
private async Task ExpandNodeAsync(AdTreeNode node)
{
    if (!node.HasDummyChild) return;
    node.HasDummyChild = false;
    node.Children.Clear();

    var children = await _adService.BrowseChildrenAsync(node.DistinguishedName, _cts.Token);
    foreach (var child in children)
    {
        var childNode = MapToTreeNode(child);
        node.Children.Add(childNode);
    }
}
```

**WPF TreeView binding:**
```xml
<TreeView ItemsSource="{Binding TreeNodes}">
    <TreeView.ItemTemplate>
        <HierarchicalDataTemplate ItemsSource="{Binding Children}">
            <TextBlock Text="{Binding Name}" />
        </HierarchicalDataTemplate>
    </TreeView.ItemTemplate>
</TreeView>
```

**Lazy loading:** The `TreeView.Expanded` event triggers `ExpandNodeAsync()`. A dummy child node is added to each unexpanded node so the expand arrow appears.

**Commit:** `feat(viewmodels): implement AD tree browse with lazy loading`

---

### 7.3 — Implement Quick Search

**Command:**
```csharp
[RelayCommand]
private async Task SearchAsync()
{
    if (string.IsNullOrWhiteSpace(SearchText)) return;

    IsSearching = true;
    ResultStatus = "Searching...";

    try
    {
        var result = await _adService.SearchAsync(SearchText, _cts.Token);
        SearchResults.Clear();
        foreach (var obj in result.Results)
            SearchResults.Add(obj);

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
```

> **Improvement:** Add 300ms debounce when wiring the search text box. Use a `DispatcherTimer` or `CancellationTokenSource` replacement pattern to cancel the previous search if the user is still typing.

**Commit:** `feat(viewmodels): implement AD quick search with error handling`

---

### 7.4 — Implement Object Selection → Attribute Detail

**Partial method triggered by property change:**
```csharp
partial void OnSelectedObjectChanged(AdObject? value)
{
    if (value != null)
        _ = LoadObjectDetailAsync(value.DistinguishedName);
}

private async Task LoadObjectDetailAsync(string dn)
{
    try
    {
        var detail = await _adService.GetObjectDetailAsync(dn, _cts.Token);
        SelectedObjectAttributes.Clear();
        foreach (var attr in detail.Attributes.OrderBy(a => a.Key))
            SelectedObjectAttributes.Add(attr);

        // Also load group membership
        var groups = await _adService.GetGroupMembershipAsync(dn, recursive: true, _cts.Token);
        SelectedObjectGroups.Clear();
        foreach (var g in groups)
            SelectedObjectGroups.Add(g);
    }
    catch (Exception ex)
    {
        _logger.Error(ex, "Failed to load detail for {DN}", dn);
    }
}
```

> **Improvement:** Add a "Copy DN" button next to the selected object that copies the Distinguished Name to clipboard. Useful for scripting and troubleshooting.

**Commit:** `feat(viewmodels): implement AD object selection with attribute detail and group membership`

---

### 7.5 — Implement Pre-Built Security Filter Buttons

**Commands:**
```csharp
[RelayCommand]
private async Task GetLockedAccountsAsync()
{
    var result = await _adService.GetLockedAccountsAsync(_cts.Token);
    PopulateSearchResults(result, "Locked Accounts");
}

[RelayCommand]
private async Task GetDisabledComputersAsync()
{
    var result = await _adService.GetDisabledComputersAsync(_cts.Token);
    PopulateSearchResults(result, "Disabled Computers");
}

[RelayCommand]
private async Task GetStaleComputersAsync()
{
    StaleThresholdDays = await _settingsService
        .GetTypedAsync("StaleComputerThresholdDays", AppConstants.DefaultStaleComputerDays);
    var result = await _adService.GetStaleComputersAsync(StaleThresholdDays, _cts.Token);
    PopulateSearchResults(result, $"Stale Computers ({StaleThresholdDays} days)");
}

[RelayCommand]
private async Task GetDomainControllersAsync()
{
    var dcs = await _adService.GetDomainControllersAsync(_cts.Token);
    // Map DCs to AdObject format and display
}
```

**Button labels (dynamic):**
- "Locked Accounts"
- "Disabled Computers"
- `$"Stale Computers ({StaleThresholdDays} days)"` — updates when settings change
- "Domain Controllers"

**Commit:** `feat(viewmodels): implement pre-built AD security filter commands`

---

### 7.6 — Implement "Send to Execution Targets" Action

**Command (on selected search results):**
```csharp
[RelayCommand]
private void SendToExecutionTargets()
{
    var computers = SearchResults
        .Where(o => o.ObjectClass.Equals("computer", StringComparison.OrdinalIgnoreCase))
        .ToList();

    if (computers.Count == 0)
    {
        _dialogService.ShowInfo("No Computers", "No computer objects are selected to send.");
        return;
    }

    _hostTargetingService.AddFromAdSearchResults(computers);
    ResultStatus = $"Sent {computers.Count} computers to Execution Targets.";
    _logger.Information("Sent {Count} AD computers to execution targets", computers.Count);
}
```

This is the key cross-view communication mechanism: AD Explorer populates the shared `IHostTargetingService` singleton, which the Execution view consumes.

**Commit:** `feat(viewmodels): implement 'Send to Execution Targets' from AD search results`

---

### 7.7 — Build `AdExplorerView.xaml`

**File:** `src/SysOpsCommander.App/Views/AdExplorerView.xaml`

**Layout:**
```
┌─────────────────────────────────────────────────┐
│ [Domain Badge: corp.contoso.com]                 │
├────────────┬────────────────────────────────────┤
│            │  Search: [___________] [Search]     │
│   Tree     │                                     │
│   View     │  [Locked] [Disabled] [Stale(90d)]  │
│            │  [DCs]                              │
│   (left    │                                     │
│    panel)  │  Results DataGrid                   │
│            │  ┌──────────────────────────┐       │
│            │  │ Name │ Type │ DN │ ...   │       │
│            │  │ ...  │ ...  │ .. │ ...   │       │
│            │  └──────────────────────────┘       │
│            │                                     │
│            │  [Send to Execution Targets]         │
├────────────┴────────────────────────────────────┤
│ Attribute Detail Panel (collapsible)             │
│ ┌──────────────┬────────────────────────┐        │
│ │ Attribute    │ Value                   │        │
│ │ cn           │ SERVER01                │        │
│ │ ...          │ ...                     │        │
│ └──────────────┴────────────────────────┘        │
│ Group Membership: [group1, group2, ...]          │
└─────────────────────────────────────────────────┘
```

> **Improvement:** Add column sorting on the results DataGrid. Click column header to sort ascending/descending.

**Commit:** `feat(app): build AdExplorerView with tree, search, filters, and detail panel`

---

### 7.8 — Build `DashboardView.xaml` with Quick Connect

**File:** `src/SysOpsCommander.App/Views/DashboardView.xaml`

**File:** `src/SysOpsCommander.ViewModels/DashboardViewModel.cs`

**Dashboard layout:**
```
┌─────────────────────────────────────────────────┐
│  Welcome to SysOps Commander                     │
│  Domain: corp.contoso.com    User: jsmith         │
├─────────────────────────────────────────────────┤
│                                                  │
│  Quick Connect                                   │
│  ┌──────────────────────────────────────────┐    │
│  │ Hostname: [____________] [Connect]        │    │
│  └──────────────────────────────────────────┘    │
│                                                  │
│  [Quick Connect Result Panel]                    │
│  ┌──────────────────────────────────────────┐    │
│  │ Name: SERVER01.corp.contoso.com           │    │
│  │ OS: Windows Server 2022                   │    │
│  │ Last Logon: 2026-03-10                    │    │
│  │ Status: Enabled                           │    │
│  │ [Execute Script...] [View in AD Explorer]  │    │
│  └──────────────────────────────────────────┘    │
│                                                  │
│  Recent Executions (last 5)                      │
│  ┌──────────────────────────────────┐            │
│  │ Script │ Hosts │ Result │ Time  │            │
│  │ ...    │ ...   │ ...    │ ...   │            │
│  └──────────────────────────────────┘            │
│                                                  │
└─────────────────────────────────────────────────┘
```

**Quick Connect flow:**
1. User enters a hostname and clicks "Connect"
2. Validate hostname via `HostnameValidator.Validate()`
3. Search AD for the computer object
4. Display key attributes: name, OS, last logon, UAC status
5. Offer actions:
   - "Execute Script..." → add to `IHostTargetingService` and navigate to Execution view
   - "View in AD Explorer" → navigate to AD Explorer and select the object

**Commit:** `feat(app): implement DashboardView with Quick Connect for incident response`

---

### 7.9 — Implement `DashboardViewModel.cs`

**File:** `src/SysOpsCommander.ViewModels/DashboardViewModel.cs`

**Dependencies:**
- `IActiveDirectoryService`
- `IHostTargetingService`
- `IAuditLogService`
- `Serilog.ILogger`

**Properties:**
```csharp
[ObservableProperty]
private string _quickConnectHostname = string.Empty;

[ObservableProperty]
private AdObject? _quickConnectResult;

[ObservableProperty]
private ObservableCollection<AuditLogEntry> _recentExecutions = new();

[ObservableProperty]
private string _activeDomainName = string.Empty;
```

**Commands:**
```csharp
[RelayCommand]
private async Task QuickConnectAsync()
{
    var validation = HostnameValidator.Validate(QuickConnectHostname);
    if (!validation.IsValid)
    {
        // Show validation error
        return;
    }

    var result = await _adService.SearchAsync(QuickConnectHostname, _cts.Token);
    QuickConnectResult = result.Results.FirstOrDefault(o =>
        o.ObjectClass.Equals("computer", StringComparison.OrdinalIgnoreCase));
}

[RelayCommand]
private void SendQuickConnectToExecution()
{
    if (QuickConnectResult == null) return;
    _hostTargetingService.AddFromAdSearchResults(new[] { QuickConnectResult });
    // Navigate to Execution view (via MainWindowViewModel navigation event)
}
```

**On initialization:** Load last 5 audit log entries for "Recent Executions" display.

**Commit:** `feat(viewmodels): implement DashboardViewModel with Quick Connect`

---

### 7.10 — Write Unit Tests — `AdExplorerViewModel`

**File:** `tests/SysOpsCommander.Tests/ViewModels/AdExplorerViewModelTests.cs`

**Test cases (8+):**

| # | Test Name | Scenario | Verification |
|---|-----------|----------|-------------|
| 1 | `Search_ValidTerm_PopulatesResults` | Search "server" | `SearchResults.Count > 0` |
| 2 | `Search_EmptyTerm_DoesNotSearch` | Empty string | Service not called |
| 3 | `Search_ServiceThrows_ShowsError` | Service exception | `ResultStatus` contains error |
| 4 | `GetStaleComputers_UsesSettingsThreshold` | Setting = 30 | Service called with 30 |
| 5 | `SendToExecution_ComputersOnly_Sends` | Mix of users/computers | Only computers sent |
| 6 | `SendToExecution_NoComputers_ShowsInfo` | All users | Info dialog shown |
| 7 | `SelectObject_LoadsAttributes` | Select object | Attributes populated |
| 8 | `ExpandNode_LazyLoads_Children` | Expand tree node | Children loaded from service |

**Commit:** `test(viewmodels): add AdExplorerViewModel tests`

---

### 7.11 — Write Unit Tests — `DashboardViewModel`

**File:** `tests/SysOpsCommander.Tests/ViewModels/DashboardViewModelTests.cs`

**Test cases (4+):**

| # | Test Name | Scenario | Verification |
|---|-----------|----------|-------------|
| 1 | `QuickConnect_ValidHostname_SearchesAd` | "SERVER01" | AD service `SearchAsync` called |
| 2 | `QuickConnect_InvalidHostname_ShowsError` | "server;cmd" | Validation failure shown |
| 3 | `QuickConnect_NoResult_ShowsNotFound` | Unknown host | `QuickConnectResult` is null |
| 4 | `SendToExecution_AddsHostToTargets` | Valid result | `HostTargetingService.AddFromAdSearchResults` called |

**Commit:** `test(viewmodels): add DashboardViewModel tests`

---

### 7.12 — Phase 7 Verification

**Full acceptance criteria check:**
- [ ] AD Explorer tree loads lazily from the active domain's root
- [ ] Quick search returns results across users, computers, and groups
- [ ] Search input is sanitized (LDAP special characters escaped)
- [ ] Pre-built security filters work:
  - Locked accounts
  - Disabled computers
  - Stale computers (threshold from settings, not hardcoded)
  - Domain controllers
- [ ] Stale computers button label shows current threshold dynamically
- [ ] Object selection loads full attribute detail with type-specific formatting
- [ ] Group membership (recursive) loads for selected objects
- [ ] "Send to Execution Targets" sends only computer objects to `IHostTargetingService`
- [ ] AD Explorer shows which domain is active (badge at top)
- [ ] Dashboard Quick Connect resolves hostname → AD object and offers execution
- [ ] Recent executions displayed on Dashboard
- [ ] All unit tests pass (12+ new cases)
- [ ] `dotnet build` — zero warnings
- [ ] `dotnet test` — all tests pass (Phase 0–7)
- [ ] Final commit: `chore: complete Phase 7 — AD Explorer and Dashboard views`

---

## Improvements & Notes

1. **Navigation vs. embedded execution in Quick Connect (step 7.8):** Quick Connect currently navigates to the Execution view. An alternative is to embed a mini execution panel directly in the Dashboard for single-host scenarios. For v1, navigation is simpler. For v2, consider an inline execution panel.

2. **Copy DN button (step 7.4):** Add a clipboard copy button next to the selected object's DN. In incident response, users frequently need to copy the full DN for LDAP queries, GPO targeting, or documentation.

3. **Column sorting on DataGrid (step 7.7):** WPF `DataGrid` supports column sorting out of the box with `CanUserSortColumns="True"`. Enable it on the results grid and attribute detail list.

4. **Search debounce (step 7.3):** Implement a 300ms debounce on the search text box to prevent excessive AD queries during typing. Use a `CancellationTokenSource` pattern:
   ```csharp
   partial void OnSearchTextChanged(string value)
   {
       _searchDebounceTimer?.Cancel();
       _searchDebounceTimer = new CancellationTokenSource();
       _ = Task.Delay(300, _searchDebounceTimer.Token)
           .ContinueWith(_ => SearchAsync(), TaskScheduler.FromCurrentSynchronizationContext());
   }
   ```

5. **"View in AD Explorer" from Dashboard (step 7.8):** Requires cross-ViewModel navigation with a target object. Use the `IHostTargetingService`-like pattern: a shared `INavigationService` that can request "navigate to AD Explorer and select DN=X". For v1, simple navigation without auto-selection is acceptable.
