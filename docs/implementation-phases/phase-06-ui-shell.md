# Phase 6: UI Shell & Navigation

> **Goal:** Build the complete WPF UI shell — main window layout, sidebar navigation, status bar with domain selector, dialog infrastructure, keyboard shortcuts, and the navigation framework that hosts all views.
>
> **Prereqs:** Phase 5 complete (all backend services exist). Phase 3 `ActiveDirectoryService` provides domain data.
>
> **Outputs:** Fully navigable UI shell with all view placeholders, domain selector, credential dialog, keyboard shortcuts, and converters.

---

## Sub-Steps

### 6.1 — Design `MainWindow.xaml` Layout

**File:** `src/SysOpsCommander.App/MainWindow.xaml`

**Layout structure (three zones):**
```
┌──────────────────────────────────────────────────┐
│  Title Bar                                        │
├────────┬─────────────────────────────────────────┤
│        │                                         │
│  Side  │        Content Area                     │
│  bar   │   (ContentControl bound to              │
│  Nav   │    CurrentView on ViewModel)            │
│        │                                         │
│        │                                         │
├────────┴─────────────────────────────────────────┤
│  Status Bar: [Domain] [User] [Status] [LogLevel] │
└──────────────────────────────────────────────────┘
```

**Sidebar navigation buttons:**
- Dashboard (home icon)
- AD Explorer (directory tree icon)
- Execution (play/run icon)
- Script Library (folder/code icon)
- Audit Log (clipboard/log icon)
- Settings (gear icon)

**XAML binding for content area:**
```xml
<ContentControl Content="{Binding CurrentView}" />
```

**Conventions:**
- Use `Grid` with column definitions for sidebar/content split
- Sidebar width: ~200px (collapsible in future v2)
- Use `ToggleButton` or `RadioButton` styled as nav buttons for sidebar items (mutual exclusion)
- Apply `Resources/Styles.xaml` for consistent theming

**Commit:** `feat(app): implement MainWindow layout with sidebar navigation and content area`

---

### 6.2 — Implement `MainWindowViewModel.cs` — Navigation

**File:** `src/SysOpsCommander.ViewModels/MainWindowViewModel.cs`

**Properties (using CommunityToolkit.Mvvm):**
```csharp
[ObservableProperty]
private object? _currentView;

[ObservableProperty]
private string _currentDomainName = string.Empty;

[ObservableProperty]
private string _currentUserName = string.Empty;

[ObservableProperty]
private string _connectionStatus = "Connected";

[ObservableProperty]
private bool _isUpdateAvailable;
```

**Navigation commands:**
```csharp
[RelayCommand]
private void NavigateToDashboard() => CurrentView = _serviceProvider.GetRequiredService<DashboardViewModel>();

[RelayCommand]
private void NavigateToAdExplorer() => CurrentView = _serviceProvider.GetRequiredService<AdExplorerViewModel>();

[RelayCommand]
private void NavigateToExecution() => CurrentView = _serviceProvider.GetRequiredService<ExecutionViewModel>();

[RelayCommand]
private void NavigateToScriptLibrary() => CurrentView = _serviceProvider.GetRequiredService<ScriptLibraryViewModel>();

[RelayCommand]
private void NavigateToAuditLog() => CurrentView = _serviceProvider.GetRequiredService<AuditLogViewModel>();

[RelayCommand]
private void NavigateToSettings() => CurrentView = _serviceProvider.GetRequiredService<SettingsViewModel>();
```

**Initialization (called from `App.xaml.cs`):**
```csharp
public async Task InitializeAsync()
{
    CurrentUserName = Environment.UserName;
    var domain = _adService.GetActiveDomain();
    CurrentDomainName = domain?.DomainName ?? "Unknown";
    NavigateToDashboard();
    await CheckForUpdatesAsync();
}
```

> **Improvement:** Use `IDialogService` interface for dialog management instead of direct dialog creation from ViewModels. This maintains MVVM separation and enables testing.

**Commit:** `feat(viewmodels): implement MainWindowViewModel with navigation commands`

---

### 6.3 — Create View↔ViewModel Data Templates

**File:** `src/SysOpsCommander.App/Resources/ViewModelTemplates.xaml`

Define `DataTemplate` mappings so WPF auto-selects the correct view for each ViewModel:
```xml
<DataTemplate DataType="{x:Type vm:DashboardViewModel}">
    <views:DashboardView />
</DataTemplate>
<DataTemplate DataType="{x:Type vm:AdExplorerViewModel}">
    <views:AdExplorerView />
</DataTemplate>
<DataTemplate DataType="{x:Type vm:ExecutionViewModel}">
    <views:ExecutionView />
</DataTemplate>
<DataTemplate DataType="{x:Type vm:ScriptLibraryViewModel}">
    <views:ScriptLibraryView />
</DataTemplate>
<DataTemplate DataType="{x:Type vm:AuditLogViewModel}">
    <views:AuditLogView />
</DataTemplate>
<DataTemplate DataType="{x:Type vm:SettingsViewModel}">
    <views:SettingsView />
</DataTemplate>
```

Merge this resource dictionary in `App.xaml`.

**Commit:** `feat(app): create ViewModel-to-View DataTemplate mappings`

---

### 6.4 — Create Placeholder Views

**Files:** Create stub `.xaml` views for all 6 view pages:
- `src/SysOpsCommander.App/Views/DashboardView.xaml`
- `src/SysOpsCommander.App/Views/AdExplorerView.xaml`
- `src/SysOpsCommander.App/Views/ExecutionView.xaml`
- `src/SysOpsCommander.App/Views/ScriptLibraryView.xaml`
- `src/SysOpsCommander.App/Views/AuditLogView.xaml`
- `src/SysOpsCommander.App/Views/SettingsView.xaml`

Each placeholder:
```xml
<UserControl ...>
    <Grid>
        <TextBlock Text="[ViewName] — Coming in Phase X"
                   HorizontalAlignment="Center"
                   VerticalAlignment="Center"
                   FontSize="24" />
    </Grid>
</UserControl>
```

**Commit:** `feat(app): create placeholder views for all navigation targets`

---

### 6.5 — Implement Status Bar

**Part of `MainWindow.xaml` — bottom section:**

```xml
<StatusBar Grid.Row="2" Grid.ColumnSpan="2">
    <StatusBarItem>
        <StackPanel Orientation="Horizontal">
            <TextBlock Text="Domain: " />
            <ComboBox ItemsSource="{Binding AvailableDomains}"
                      SelectedItem="{Binding SelectedDomain}"
                      DisplayMemberPath="DomainName"
                      Width="200" />
            <Button Content="..." Command="{Binding OpenDomainSelectorCommand}"
                    ToolTip="Enter domain manually" Margin="2,0,0,0" />
        </StackPanel>
    </StatusBarItem>
    <Separator />
    <StatusBarItem>
        <TextBlock Text="{Binding CurrentUserName, StringFormat='User: {0}'}" />
    </StatusBarItem>
    <Separator />
    <StatusBarItem>
        <StackPanel Orientation="Horizontal">
            <Ellipse Width="8" Height="8" Fill="{Binding ConnectionStatus, Converter={StaticResource StatusToColorConverter}}" />
            <TextBlock Text="{Binding ConnectionStatus}" Margin="4,0,0,0" />
        </StackPanel>
    </StatusBarItem>
    <Separator />
    <StatusBarItem HorizontalAlignment="Right" Visibility="{Binding IsUpdateAvailable, Converter={StaticResource BoolToVisibilityConverter}}">
        <TextBlock Text="⬆ Update Available" Foreground="DodgerBlue" Cursor="Hand" />
    </StatusBarItem>
</StatusBar>
```

**ViewModel additions for status bar:**
```csharp
[ObservableProperty]
private ObservableCollection<DomainConnection> _availableDomains = new();

[ObservableProperty]
private DomainConnection? _selectedDomain;

partial void OnSelectedDomainChanged(DomainConnection? value)
{
    if (value != null)
        _ = SwitchDomainAsync(value);
}
```

**Commit:** `feat(app): implement status bar with domain selector and connection indicator`

---

### 6.6 — Implement Domain Selector Dialog

**Files:**
- `src/SysOpsCommander.App/Dialogs/DomainSelectorDialog.xaml`
- `src/SysOpsCommander.App/Dialogs/DomainSelectorDialog.xaml.cs`
- `src/SysOpsCommander.ViewModels/Dialogs/DomainSelectorViewModel.cs`

**Dialog layout:**
```
┌─────────────────────────────────────┐
│  Connect to Domain                   │
├─────────────────────────────────────┤
│  Domain Name: [________________]     │
│  Domain Controller (optional):       │
│                [________________]     │
│                                      │
│  [Test Connection]   Status: ✓ OK    │
│                                      │
│           [OK]  [Cancel]             │
└─────────────────────────────────────┘
```

**ViewModel:**
```csharp
[ObservableProperty]
private string _domainName = string.Empty;

[ObservableProperty]
private string _domainControllerFqdn = string.Empty;

[ObservableProperty]
private string _testStatus = string.Empty;

[ObservableProperty]
private bool _isTestSuccessful;

[RelayCommand]
private async Task TestConnectionAsync()
{
    TestStatus = "Testing...";
    try
    {
        var domain = new DomainConnection { DomainName = DomainName, DomainControllerFqdn = DomainControllerFqdn };
        await _adService.SetActiveDomainAsync(domain, CancellationToken.None);
        TestStatus = "✓ Connected successfully";
        IsTestSuccessful = true;
    }
    catch (Exception ex)
    {
        TestStatus = $"✗ Failed: {ex.Message}";
        IsTestSuccessful = false;
    }
}
```

**Commit:** `feat(app): implement DomainSelectorDialog with connection testing`

---

### 6.7 — Implement Credential Dialog

**Files:**
- `src/SysOpsCommander.App/Dialogs/CredentialDialog.xaml`
- `src/SysOpsCommander.App/Dialogs/CredentialDialog.xaml.cs`
- `src/SysOpsCommander.ViewModels/Dialogs/CredentialDialogViewModel.cs`

**Dialog layout:**
```
┌─────────────────────────────────────┐
│  Enter Credentials                   │
├─────────────────────────────────────┤
│  Domain:    [pre-populated______]    │
│  Username:  [________________]       │
│  Password:  [****************]       │
│                                      │
│  Auth Method: [Kerberos ▼]           │
│               (Kerberos/NTLM/CredSSP)│
│                                      │
│           [OK]  [Cancel]             │
└─────────────────────────────────────┘
```

**Key behaviors:**
- Domain field pre-populated with active domain name
- Auth method pre-populated from default settings
- Password field uses `PasswordBox` (WPF secure input — does not bind directly)
- OK button creates `PSCredential` from inputs, returns to caller
- Never store credentials after dialog closes

> **MVVM note on `PasswordBox`:** WPF's `PasswordBox` does not support data binding by design (security). Use code-behind to extract the `SecureString` when OK is clicked, or use an `IPasswordSupplier` interface pattern.

**Commit:** `feat(app): implement CredentialDialog with auth method selection`

---

### 6.8 — Implement `IDialogService` Interface

**File:** `src/SysOpsCommander.Core/Interfaces/IDialogService.cs`

```csharp
public interface IDialogService
{
    Task<DomainConnection?> ShowDomainSelectorAsync();
    Task<PSCredential?> ShowCredentialDialogAsync(string? defaultDomain = null, WinRmAuthMethod? defaultAuth = null);
    Task<string?> ShowSaveFileDialogAsync(string defaultExtension, string filter);
    Task<string?> ShowOpenFileDialogAsync(string filter);
    Task<bool> ShowConfirmationAsync(string title, string message);
    void ShowError(string title, string message);
    void ShowInfo(string title, string message);
}
```

> **Improvement:** This interface keeps ViewModels decoupled from WPF dialog types. The implementation lives in the App project and uses WPF `Window.ShowDialog()`, `MessageBox`, `SaveFileDialog`, etc.

**Commit:** `feat(core): define IDialogService interface for MVVM dialog abstraction`

---

### 6.9 — Implement `DialogService.cs`

**File:** `src/SysOpsCommander.App/Services/DialogService.cs`

Implements `IDialogService` using WPF dialog classes. Register in DI:
```csharp
services.AddSingleton<IDialogService, DialogService>();
```

**Commit:** `feat(app): implement DialogService for WPF dialog management`

---

### 6.10 — Create Value Converters

**Files:**
- `src/SysOpsCommander.App/Converters/StatusToColorConverter.cs`
- `src/SysOpsCommander.App/Converters/BoolToVisibilityConverter.cs`

**`StatusToColorConverter`:**
```csharp
public object Convert(object value, ...) => value?.ToString() switch
{
    "Connected" => Brushes.Green,
    "Disconnected" => Brushes.Red,
    "Connecting" => Brushes.Orange,
    _ => Brushes.Gray
};
```

**`BoolToVisibilityConverter`:**
```csharp
public object Convert(object value, ...) =>
    value is true ? Visibility.Visible : Visibility.Collapsed;
```

Register in `App.xaml` or `Styles.xaml` as static resources.

**Commit:** `feat(app): implement StatusToColor and BoolToVisibility converters`

---

### 6.11 — Create `Styles.xaml` Resource Dictionary

**File:** `src/SysOpsCommander.App/Resources/Styles.xaml`

**Define base styles for:**
- Navigation buttons (consistent sizing, hover effects, active state)
- `DataGrid` defaults (alternating rows, selection style)
- `TextBox` defaults (padding, border)
- `Button` defaults (padding, min width)
- Status bar items
- Section headers
- Error/warning text styles (red/orange)

Merge in `App.xaml`:
```xml
<Application.Resources>
    <ResourceDictionary>
        <ResourceDictionary.MergedDictionaries>
            <ResourceDictionary Source="Resources/Styles.xaml" />
            <ResourceDictionary Source="Resources/ViewModelTemplates.xaml" />
        </ResourceDictionary.MergedDictionaries>
    </ResourceDictionary>
</Application.Resources>
```

**Commit:** `feat(app): create Styles.xaml with base application styles`

---

### 6.12 — Implement Keyboard Shortcuts

**In `MainWindow.xaml` — InputBindings:**
```xml
<Window.InputBindings>
    <KeyBinding Key="F" Modifiers="Control" Command="{Binding FocusSearchCommand}" />
    <KeyBinding Key="E" Modifiers="Control" Command="{Binding NavigateToExecutionCommand}" />
    <KeyBinding Key="D" Modifiers="Control" Command="{Binding OpenDomainSelectorCommand}" />
    <KeyBinding Key="F5" Command="{Binding RefreshCurrentViewCommand}" />
    <KeyBinding Key="Escape" Command="{Binding CancelCurrentOperationCommand}" />
</Window.InputBindings>
```

**ViewModel commands:**
```csharp
[RelayCommand]
private void FocusSearch()
{
    // Raise a message/event that the View subscribes to for focus management
}

[RelayCommand]
private async Task OpenDomainSelectorAsync()
{
    var domain = await _dialogService.ShowDomainSelectorAsync();
    if (domain != null)
        await SwitchDomainAsync(domain);
}

[RelayCommand]
private async Task RefreshCurrentViewAsync()
{
    if (CurrentView is IRefreshable refreshable)
        await refreshable.RefreshAsync();
}

[RelayCommand]
private void CancelCurrentOperation()
{
    _globalCancellationSource?.Cancel();
}
```

> **`IRefreshable` interface:** Define in Core for ViewModels that support F5 refresh:
> ```csharp
> public interface IRefreshable
> {
>     Task RefreshAsync();
> }
> ```

**Commit:** `feat(app): implement keyboard shortcuts for navigation and actions`

---

### 6.13 — Implement Loading Indicator

> **Improvement (not in original plan):** Add a global loading indicator overlay for long-running operations.

**In `MainWindow.xaml`:**
```xml
<Grid Visibility="{Binding IsBusy, Converter={StaticResource BoolToVisibilityConverter}}"
      Background="#80000000" Panel.ZIndex="100">
    <StackPanel HorizontalAlignment="Center" VerticalAlignment="Center">
        <ProgressBar IsIndeterminate="True" Width="200" Height="8" />
        <TextBlock Text="{Binding BusyMessage}" Foreground="White"
                   HorizontalAlignment="Center" Margin="0,8,0,0" />
    </StackPanel>
</Grid>
```

**ViewModel:**
```csharp
[ObservableProperty]
private bool _isBusy;

[ObservableProperty]
private string _busyMessage = string.Empty;
```

**Commit:** `feat(app): add global loading indicator overlay`

---

### 6.14 — Write Unit Tests — `MainWindowViewModel`

**File:** `tests/SysOpsCommander.Tests/ViewModels/MainWindowViewModelTests.cs`

**Test cases (6+):**

| # | Test Name | Scenario | Verification |
|---|-----------|----------|-------------|
| 1 | `NavigateToDashboard_SetsCurrentView_ToDashboardVm` | Navigate command | `CurrentView` is `DashboardViewModel` |
| 2 | `NavigateToExecution_SetsCurrentView_ToExecutionVm` | Navigate command | `CurrentView` is `ExecutionViewModel` |
| 3 | `Initialize_SetsCurrentUser` | Startup | `CurrentUserName` not empty |
| 4 | `Initialize_DetectsCurrentDomain` | Startup | `CurrentDomainName` populated |
| 5 | `DomainSwitch_UpdatesCurrentDomainName` | Select new domain | `CurrentDomainName` changed |
| 6 | `PropertyChanged_FiredForNavigation` | Navigate | `PropertyChanged` raised for `CurrentView` |

**Commit:** `test(viewmodels): add MainWindowViewModel navigation and initialization tests`

---

### 6.15 — Phase 6 Verification

**Full acceptance criteria check:**
- [ ] Application launches with sidebar navigation and content area
- [ ] All 6 navigation targets work (Dashboard, AD Explorer, Execution, Script Library, Audit Log, Settings)
- [ ] View↔ViewModel DataTemplates auto-resolve correctly
- [ ] Status bar shows: domain selector, current user, connection indicator, update badge
- [ ] Domain selector dropdown works; manual domain entry dialog works with "Test Connection"
- [ ] Credential dialog pre-populates domain and auth method from settings
- [ ] Keyboard shortcuts work: `Ctrl+F`, `Ctrl+E`, `Ctrl+D`, `F5`, `Escape`
- [ ] `IDialogService` abstracts all dialog interactions from ViewModels
- [ ] Loading indicator overlay appears during long operations
- [ ] Value converters work (status→color, bool→visibility)
- [ ] All unit tests pass (6+ new ViewModel tests)
- [ ] `dotnet build` — zero warnings
- [ ] `dotnet test` — all tests pass (Phase 0–6)
- [ ] Final commit: `chore: complete Phase 6 — UI shell and navigation`

---

## Improvements & Notes

1. **`IDialogService` interface (step 6.8):** Not in the original plan but essential for MVVM. ViewModels should never directly instantiate WPF `Window` types. The `IDialogService` interface in Core provides the abstraction; the `DialogService` implementation in the App project uses WPF dialogs. This enables testing ViewModels with a mock `IDialogService`.

2. **Accessibility attributes:** Add `AutomationProperties.Name` and `AutomationProperties.HelpText` to all interactive controls. This supports screen readers and automated UI testing. Example:
   ```xml
   <Button AutomationProperties.Name="Navigate to Dashboard"
           AutomationProperties.HelpText="Opens the dashboard view" />
   ```

3. **Loading spinner (step 6.13):** The original plan doesn't specify a loading indicator. Long operations (AD search, script execution, export) need visual feedback. The global overlay with `IsBusy` binding is a simple, effective approach.

4. **`PasswordBox` MVVM challenge (step 6.7):** WPF `PasswordBox.Password` cannot be data-bound by design (it's a `string` property, not a dependency property). Common approaches:
   - Use code-behind to extract the password in the dialog's OK handler (simplest)
   - Use an `IPasswordSupplier` interface injected into the ViewModel
   - Use an attached property (breaks security model — not recommended)
   The code-behind approach is pragmatic for a dialog and doesn't violate MVVM in a meaningful way.

5. **Sidebar collapse (v2):** Consider a hamburger-style collapsible sidebar for smaller screens. For v1, a fixed-width sidebar is sufficient. Add a collapse toggle as a v2 enhancement.
