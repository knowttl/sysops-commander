# AD Explorer — Computer IP Address Resolution

> **Date:** 2026-03-17
> **Status:** Approved
> **Scope:** AD Explorer view — display IP addresses for computer objects via async DNS resolution

## Overview

Add IP address visibility for computer objects in the AD Explorer. Uses DNS resolution (`Dns.GetHostAddressesAsync`) with lazy loading to resolve `dNSHostName` after search results are displayed. Shows primary IPv4 in the grid column, full IP list (v4+v6) in the inspector panel.

## Requirements

1. **Grid column** — New "IP Address" column in the search results DataGrid showing primary IPv4 for computer objects
2. **Inspector section** — Dedicated IP Addresses card in the detail inspector showing all resolved addresses with family labels
3. **Lazy loading** — Results display immediately; IP resolution happens asynchronously in the background with progressive per-row updates
4. **Computer-only** — Only computer objects trigger DNS resolution. Non-computer rows show empty cells.
5. **Three-state display** — "Resolving..." (italic/gray) → "10.0.1.50" (normal) → "N/A" (gray, with error tooltip)
6. **Throttled parallelism** — Max 20 concurrent DNS lookups to avoid flooding DNS
7. **Per-host timeout** — 3-second timeout per hostname to prevent one unreachable host from blocking the batch
8. **Cancellation** — New search cancels all pending DNS resolutions from the previous search
9. **Export** — IP Address column included in CSV/Excel export column picker

## Architecture

### Approach: `AdObjectRow` Wrapper ViewModel

Each `AdObject` search result is wrapped in an `AdObjectRow : ObservableObject`. The wrapper passes through `AdObject` properties and adds observable IP resolution fields. `[ObservableProperty]` source generators fire `PropertyChanged` per row, giving the DataGrid automatic per-cell updates as DNS results stream in.

This keeps `AdObject` immutable (no model pollution) and follows standard MVVM patterns.

## Component Design

### Core Layer

#### `IpResolutionStatus` Enum (`Core/Enums/IpResolutionStatus.cs`)

```csharp
public enum IpResolutionStatus
{
    NotStarted,
    Resolving,
    Resolved,
    Failed,
    NotApplicable
}
```

#### `IpResolutionResult` Model (`Core/Models/IpResolutionResult.cs`)

```csharp
public sealed class IpResolutionResult
{
    public IpResolutionStatus Status { get; init; }
    public string? PrimaryIPv4 { get; init; }
    public IReadOnlyList<string> AllAddresses { get; init; }
    public string? ErrorMessage { get; init; }
    public string? Hostname { get; init; }
}
```

#### `IDnsResolverService` Interface (`Core/Interfaces/IDnsResolverService.cs`)

```csharp
public interface IDnsResolverService
{
    Task<IpResolutionResult> ResolveAsync(string hostname, CancellationToken cancellationToken = default);

    Task ResolveAllAsync(
        IReadOnlyList<(string Hostname, Action<IpResolutionResult> Callback)> requests,
        CancellationToken cancellationToken = default);
}
```

- `ResolveAsync` — single hostname lookup (inspector selection)
- `ResolveAllAsync` — batch with per-item callback, throttled parallel

### Service Layer

#### `DnsResolverService` (`Services/DnsResolverService.cs`)

- Uses `Dns.GetHostAddressesAsync(hostname, cancellationToken)`
- Filters `AddressFamily.InterNetwork` for IPv4; first one = `PrimaryIPv4`
- All addresses (v4+v6) collected as strings in `AllAddresses`
- Per-host timeout: 3 seconds via linked `CancellationTokenSource.CancelAfter(3000)`
- Batch: `Parallel.ForEachAsync` with `MaxDegreeOfParallelism = 20`
- Callback marshalled to UI thread via `SynchronizationContext` / `IDispatcherService`
- Error handling:
  - `SocketException` → `Failed` with exception message
  - `OperationCanceledException` (per-host timeout) → `Failed` with "DNS resolution timed out"
  - `OperationCanceledException` (parent token) → swallowed, stops resolution
  - Null/empty hostname → `NotApplicable`, no DNS call
- Testability: thin internal `IDnsLookup` interface wrapping `Dns.GetHostAddressesAsync`
- DI registration: **transient**

### ViewModel Layer

#### `AdObjectRow` (`ViewModels/AdObjectRow.cs`)

```csharp
public partial class AdObjectRow : ObservableObject
```

**Pass-through properties:**
- `AdObject AdObject { get; }` — the wrapped model
- `string Name` → `AdObject.Name`
- `string ObjectClass` → `AdObject.ObjectClass`
- `string? Description` → `AdObject.Description`
- `string DistinguishedName` → `AdObject.DistinguishedName`
- `IReadOnlyDictionary<string, object?> Attributes` → `AdObject.Attributes`

**Observable IP properties:**
- `[ObservableProperty] string? _ipAddress` — grid display text
- `[ObservableProperty] string? _ipTooltip` — tooltip text
- `[ObservableProperty] IpResolutionStatus _ipResolutionStatus = NotStarted`
- `[ObservableProperty] IReadOnlyList<string>? _allIpAddresses` — full list for inspector

**Helpers:**
- `string? DnsHostName` — extracts `dNSHostName` from attributes
- `bool IsComputer` — checks `ObjectClass == "computer"`
- `UpdateFromResolutionResult(IpResolutionResult)` — updates all four observable properties

#### `AdExplorerViewModel` Changes

**Collection type change:**
- `SearchResults` → `ObservableCollection<AdObjectRow>` (was `ObservableCollection<AdObject>`)
- `SelectedObject` → type `AdObjectRow` (was `AdObject`), use `.AdObject` for service calls

**After search completes:**
1. Map each `AdObject` → `AdObjectRow`
2. Populate `SearchResults`
3. Filter computer rows with non-null `DnsHostName`
4. Set each to `Resolving` status
5. Fire `_dnsResolverService.ResolveAllAsync(...)` with per-row callbacks
6. CancellationToken linked to search cancellation

**Inspector selection:**
- If selected row is computer and already resolved → show `AllIpAddresses` in inspector
- If not yet resolved → trigger single `ResolveAsync`, update row

### UI Layer

#### DataGrid — IP Address Column

- **Position:** 5th column (after Description, before Distinguished Name)
- **Width:** 120
- **Type:** Template column for three-state display + tooltip
- **States:**
  - `Resolving` → "Resolving..." italic, muted gray
  - `Resolved` → IPv4 address, normal text, tooltip = hostname
  - `Failed` → "N/A" muted gray, tooltip = error message
  - `NotApplicable` → empty cell
- Uses `DataTrigger` on `IpResolutionStatus`
- Integrated into column picker — default visible

#### Inspector — IP Addresses Section

- Appears only for computer objects, above the Attributes tab content
- Below the object header card
- Label: **"IP Addresses"**
- Each address on its own line: `10.0.1.50 (IPv4)`, `fe80::1 (IPv6)`
- Failed: "Unable to resolve — [error message]" in muted text
- Resolving: small progress indicator

#### Export

- IP Address included in column picker popup
- Exports `PrimaryIPv4` value (or "N/A" / empty)
- `AllIpAddresses` not exported

## Testing Strategy

### `DnsResolverServiceTests`
- `ResolveAsync_ValidHostname_ReturnsResolvedResult`
- `ResolveAsync_UnresolvableHostname_ReturnsFailedWithError`
- `ResolveAsync_Timeout_ReturnsFailedWithTimeoutMessage`
- `ResolveAsync_EmptyHostname_ReturnsNotApplicable`
- `ResolveAsync_MultipleAddresses_PrimaryIsFirstIPv4`
- `ResolveAsync_IPv6Only_PrimaryIsNull`
- `ResolveAllAsync_ThrottlesParallelism`
- `ResolveAllAsync_CancellationStopsPending`
- `ResolveAllAsync_CallbackFiredPerHostname`

### `AdObjectRowTests`
- `UpdateFromResolutionResult_Resolved_SetsIpAndTooltip`
- `UpdateFromResolutionResult_Failed_SetsNAAndErrorTooltip`
- `UpdateFromResolutionResult_Resolving_SetsResolvingText`
- `IsComputer_ComputerClass_ReturnsTrue`
- `IsComputer_UserClass_ReturnsFalse`
- `DnsHostName_ExtractsFromAttributes`
- `PropertyChanged_FiredOnIpUpdate`

### Testability
- `IDnsLookup` internal interface wraps `Dns.GetHostAddressesAsync` for unit test injection
- `InternalsVisibleTo("SysOpsCommander.Tests")` on Services project (already configured)

### Existing Test Updates
- ViewModel tests referencing `SearchResults` items updated for `AdObjectRow` wrapper

## Files Changed/Created

### New Files
| File | Project | Description |
|------|---------|-------------|
| `Core/Enums/IpResolutionStatus.cs` | Core | Resolution status enum |
| `Core/Models/IpResolutionResult.cs` | Core | Resolution result model |
| `Core/Interfaces/IDnsResolverService.cs` | Core | DNS resolver interface |
| `Services/DnsResolverService.cs` | Services | DNS resolver implementation |
| `ViewModels/AdObjectRow.cs` | ViewModels | Wrapper ViewModel for search results |
| `Tests/DnsResolverServiceTests.cs` | Tests | DNS resolver unit tests |
| `Tests/AdObjectRowTests.cs` | Tests | Row wrapper unit tests |

### Modified Files
| File | Project | Change |
|------|---------|--------|
| `ViewModels/AdExplorerViewModel.cs` | ViewModels | SearchResults → `AdObjectRow`, DNS orchestration |
| `App/Views/AdExplorerView.xaml` | App | IP Address column, inspector IP section |
| `App/DependencyInjection/ServiceCollectionExtensions.cs` | App | Register `IDnsResolverService` |
| `Tests/AdExplorerViewModelTests.cs` | Tests | Update for `AdObjectRow` type |
