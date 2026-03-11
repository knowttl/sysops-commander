# Phase 3: Active Directory Service Layer

> **Goal:** Build the full AD integration with **multi-domain support** — domain discovery, domain switching, quick search, tree browsing, attribute viewing, pre-built security filters, and group membership resolution.
>
> **Prereqs:** Phase 2 complete (`LdapFilterSanitizer`, `HostnameValidator` available).
>
> **Outputs:** `ActiveDirectoryService` fully implemented and tested. All `IActiveDirectoryService` methods functional.

---

## Sub-Steps

### 3.1 — Implement Domain Discovery — `GetAvailableDomainsAsync()`

**File:** `src/SysOpsCommander.Services/ActiveDirectoryService.cs`

**Implementation:**
1. Use `Domain.GetCurrentDomain()` to get the user's current domain
2. Use `Forest.GetCurrentForest().Domains` to enumerate all trusted domains in the forest
3. Map each to a `DomainConnection` object with:
   - `DomainName` (e.g., `corp.contoso.com`)
   - `DomainControllerFqdn` (optional — let AD auto-select by default)
   - `RootDistinguishedName` (e.g., `DC=corp,DC=contoso,DC=com`)
   - `IsCurrentDomain` flag (true for user's home domain)
4. Handle `ActiveDirectoryOperationException` for domain enumeration failures (e.g., network issues, untrusted forests)
5. Log at `Information`: "Discovered {count} domains in forest {forestName}"

**Error handling:**
- If forest enumeration fails → return only the current domain as a single-item list
- If current domain detection fails → throw with descriptive message (this is a fatal startup condition)

**Commit:** `feat(services): implement domain discovery via forest trust enumeration`

---

### 3.2 — Implement Domain Switching — `SetActiveDomainAsync()`

**Implementation:**
1. Validate that the domain is reachable by attempting an LDAP bind to its root DN
2. Update internal `_activeDomain` field (type `DomainConnection`)
3. Update internal `_rootDirectoryEntry` to point to the new domain's root
4. Dispose the previous `DirectoryEntry` if it exists
5. Log at `Information`: "Switched active domain to {domainName}"
6. Raise an event or notification that downstream consumers can observe (e.g., `ActiveDomainChanged` event)

**Threading concerns:**
- Domain switching must be serialized — use a `SemaphoreSlim(1, 1)` to prevent concurrent switching
- All search/browse operations should check `_activeDomain` at invocation time, not cache it

**Commit:** `feat(services): implement domain switching with LDAP bind validation`

---

### 3.3 — Implement `GetActiveDomain()` — Synchronous Accessor

**Implementation:**
- Simple property or method returning the current `_activeDomain` field
- If no domain has been explicitly set, return the auto-detected current domain
- This is a synchronous method (no I/O needed — just returns the cached value)

**Commit:** (include in 3.2 commit)

---

### 3.4 — Initialize Active Domain on Service Construction

**Constructor or `InitializeAsync()` pattern:**
1. Detect current user's domain via `Domain.GetCurrentDomain()`
2. Set it as the active domain
3. Create the root `DirectoryEntry` for the current domain
4. Log at `Information`: "Initialized AD service for domain {currentDomain}"

> **Improvement:** Consider an `InitializeAsync()` method called from `App.xaml.cs` during startup rather than doing I/O in the constructor. Constructor injection should be fast and side-effect-free. The initialization can be deferred until the first AD operation or triggered explicitly.

**Commit:** `feat(services): initialize ActiveDirectoryService with current user's domain`

---

### 3.5 — Implement Quick Search — `SearchAsync()`

**Implementation:**
1. Take the user's search term (string)
2. Sanitize via `LdapFilterSanitizer.SanitizeInput(searchTerm)`
3. Build a compound LDAP filter searching across key attributes:
   ```
   (|(sAMAccountName=*{sanitized}*)(cn=*{sanitized}*)(displayName=*{sanitized}*)(mail=*{sanitized}*)(dNSHostName=*{sanitized}*))
   ```
4. Execute via `DirectorySearcher` with:
   - `SearchScope.Subtree`
   - `SizeLimit = AppConstants.MaxResultsPerPage`
   - `ServerTimeLimit = TimeSpan.FromSeconds(AppConstants.DefaultAdQueryTimeoutSeconds)`
   - `PageSize = 500` (for paged results)
5. Map `SearchResult` objects to `AdObject` models
6. Wrap in `AdSearchResult` with query metadata (search term, execution time, result count)

**Properties to load (optimization):**
```csharp
searcher.PropertiesToLoad.AddRange(new[]
{
    "sAMAccountName", "cn", "displayName", "objectClass",
    "distinguishedName", "mail", "dNSHostName", "lastLogonTimestamp",
    "whenCreated", "userAccountControl", "description"
});
```

**Cancellation:** Wire `CancellationToken` — call `ct.ThrowIfCancellationRequested()` before and after the search. `DirectorySearcher` doesn't natively support cancellation, so use `Task.Run` with token.

**Commit:** `feat(services): implement AD quick search across key attributes`

---

### 3.6 — Implement Filter Search — `SearchWithFilterAsync()`

**Implementation:**
1. Accept a raw LDAP filter string (already formatted by caller, e.g., pre-built filters)
2. Validate that it's not null/empty
3. Execute via `DirectorySearcher` with the provided filter
4. Same property loading and result mapping as `SearchAsync()`
5. Same cancellation and timeout handling

> **Security note:** Pre-built filters (constructed internally) are trusted. If this method is ever exposed to direct user input, add filter validation.

**Commit:** `feat(services): implement AD filter-based search`

---

### 3.7 — Implement Tree Browse — `BrowseChildrenAsync()`

**Implementation:**
1. Bind a `DirectoryEntry` to the specified `parentDn`
2. Use `DirectorySearcher` with `SearchScope.OneLevel` to retrieve immediate children
3. Return as `IReadOnlyList<AdObject>` with minimal properties:
   - `distinguishedName`, `cn`, `objectClass`, `description`
4. This supports lazy loading in the UI tree — only load children when a node is expanded

**Performance:** Only load essential properties for tree display. Full attribute loading is deferred to `GetObjectDetailAsync()`.

**Commit:** `feat(services): implement AD tree browsing with lazy child loading`

---

### 3.8 — Implement Attribute Detail — `GetObjectDetailAsync()`

**Implementation:**
1. Bind a `DirectoryEntry` to the specified `distinguishedName`
2. Call `DirectoryEntry.RefreshCache()` to load all attributes
3. Iterate `DirectoryEntry.Properties` and map to `AdObject.Attributes` dictionary
4. Handle multi-valued attributes (store as `string[]` in the dictionary)
5. Handle byte array attributes (e.g., `objectSid`, `objectGUID`) — convert to readable format
6. Log at `Debug`: "Loaded {attributeCount} attributes for {dn}"

**Special attribute handling:**
| Attribute | Transform |
|-----------|-----------|
| `objectSid` | `new SecurityIdentifier(bytes, 0).Value` |
| `objectGUID` | `new Guid(bytes).ToString()` |
| `lastLogonTimestamp` | `DateTime.FromFileTimeUtc(value)` |
| `whenCreated` | Already `DateTime` |
| `userAccountControl` | Decode UAC flags to readable list |
| `pwdLastSet` | `DateTime.FromFileTimeUtc(value)` |
| `accountExpires` | `DateTime.FromFileTimeUtc(value)` (handle `0` and `Int64.MaxValue` as "Never") |

**Commit:** `feat(services): implement AD object attribute detail with special type handling`

---

### 3.9 — Implement Group Membership — `GetGroupMembershipAsync()`

**Implementation:**

**Recursive membership (via `tokenGroups`):**
1. Bind `DirectoryEntry` to the object DN
2. Call `RefreshCache(new[] { "tokenGroups" })`
3. Iterate `tokenGroups` byte arrays → convert each to SID
4. Resolve each SID via `new SecurityIdentifier(bytes, 0).Translate(typeof(NTAccount))`
5. Handle `IdentityNotMappedException` for orphan SIDs (include SID string instead)
6. Return sorted list of group names

**Direct membership (via `memberOf`):**
1. Read `memberOf` attribute (already loaded via `RefreshCache`)
2. Return as-is (these are DNs of direct groups)

**Parameter:** `bool recursive` selects which method to use.

**Commit:** `feat(services): implement recursive and direct group membership resolution`

---

### 3.10 — Implement Pre-Built Security Filters

**Four filter methods:**

**`GetLockedAccountsAsync()`:**
```csharp
var filter = "(&(objectClass=user)(lockoutTime>=1))";
return SearchWithFilterAsync(filter, ct);
```

> **Improvement:** The filter `lockoutTime>=1` catches all currently-locked accounts, but some may have been auto-unlocked by the lockout policy timer. A more precise filter adds `(!(lockoutDuration=0))`, but this requires domain policy awareness. For v1, the simpler filter is correct — the UI should show the `lockoutTime` attribute value so the user can assess.

**`GetDisabledComputersAsync()`:**
```csharp
var filter = "(&(objectClass=computer)(userAccountControl:1.2.840.113556.1.4.803:=2))";
return SearchWithFilterAsync(filter, ct);
```

**`GetStaleComputersAsync(int daysInactive)`:**
```csharp
var threshold = DateTime.UtcNow.AddDays(-daysInactive).ToFileTimeUtc();
var filter = $"(&(objectClass=computer)(lastLogonTimestamp<={threshold}))";
return SearchWithFilterAsync(filter, ct);
```
- `daysInactive` loaded from `ISettingsService.GetEffectiveAsync("StaleComputerThresholdDays")` at the call site (ViewModel or service)
- Default to `AppConstants.DefaultStaleComputerDays` (90)

**`GetDomainControllersAsync()`:**
```csharp
var filter = "(&(objectClass=computer)(userAccountControl:1.2.840.113556.1.4.803:=8192))";
```
Returns list of DC FQDNs extracted from `dNSHostName`.

**Commit:** `feat(services): implement pre-built AD security filters`

---

### 3.11 — Implement `IDisposable` on `ActiveDirectoryService`

**Implementation:**
1. Implement `IDisposable` pattern
2. Dispose all held `DirectoryEntry` objects in `Dispose()`
3. Set `_disposed` flag to prevent use-after-dispose
4. Check `_disposed` in all public methods → throw `ObjectDisposedException` if disposed

**Commit:** `feat(services): implement IDisposable on ActiveDirectoryService`

---

### 3.12 — Register `ActiveDirectoryService` in DI

**Action:** In `ServiceCollectionExtensions.cs`:
```csharp
services.AddSingleton<IActiveDirectoryService, ActiveDirectoryService>();
```

> **Note:** Singleton is appropriate because the service holds the active domain state. Alternatively, the domain state could be externalized and the service made transient — but singleton is simpler for v1.

**Commit:** `build(app): register ActiveDirectoryService in DI container`

---

### 3.13 — Write Unit Tests — Search and Filter Logic

**File:** `tests/SysOpsCommander.Tests/Services/ActiveDirectoryServiceTests.cs`

> **Testability challenge:** `DirectorySearcher` and `DirectoryEntry` are sealed and hard to mock. Options:
> 1. **Wrapper interface** (recommended): Create `IDirectorySearcherWrapper` in Core that `ActiveDirectoryService` depends on. Mock the wrapper in tests.
> 2. **Integration tests only:** Test against a real AD in a lab environment.
> 3. **Internal method testing:** Test the filter construction and result mapping logic in isolation.

**Recommended approach:** Option 1 + Option 3. Create thin wrappers and test the service logic.

**Test cases (8+):**

| # | Test Name | Scenario | Verification |
|---|-----------|----------|-------------|
| 1 | `SearchAsync_BuildsCorrectFilter` | Search for "jsmith" | Filter includes all 5 attributes |
| 2 | `SearchAsync_SanitizesInput` | Search for "jsmith*" | Asterisk is escaped in filter |
| 3 | `GetStaleComputers_UsesConfiguredThreshold` | Threshold = 30 days | Filter timestamp matches 30-day lookback |
| 4 | `GetStaleComputers_DefaultsTo90Days` | No setting override | Threshold = 90 days |
| 5 | `GetLockedAccounts_UsesCorrectFilter` | — | `lockoutTime>=1` in filter |
| 6 | `GetDisabledComputers_UsesUacBitwiseFilter` | — | UAC bitwise AND filter present |
| 7 | `GetDomainControllers_UsesUac8192Filter` | — | `8192` in UAC filter |
| 8 | `SetActiveDomain_DisposePreviousEntry` | Switch domains | Previous `DirectoryEntry` disposed |
| 9 | `SearchAsync_CancellationRequested_Throws` | Cancel before search | `OperationCanceledException` |

**Commit:** `test(services): add ActiveDirectoryService unit tests`

---

### 3.14 — Write Unit Tests — Attribute Mapping

**File:** `tests/SysOpsCommander.Tests/Services/AdAttributeMappingTests.cs`

**Test cases (5+):**

| # | Test Name | Scenario |
|---|-----------|----------|
| 1 | `MapObjectSid_ConvertsToSidString` | Byte array → `S-1-5-21-...` |
| 2 | `MapObjectGuid_ConvertsToGuidString` | Byte array → GUID format |
| 3 | `MapLastLogonTimestamp_ConvertsToDateTime` | FileTime → UTC DateTime |
| 4 | `MapAccountExpires_MaxValue_ReturnsNever` | `Int64.MaxValue` → "Never" |
| 5 | `MapUserAccountControl_DecodesFlags` | UAC int → flag names |

**Commit:** `test(services): add AD attribute mapping tests`

---

### 3.15 — Phase 3 Verification

**Full acceptance criteria check:**
- [ ] App detects and connects to the current user's domain on startup
- [ ] Available domains are enumerable from forest trusts
- [ ] Domain switching updates all subsequent queries to target the new domain
- [ ] Quick search works for users, computers, and groups with partial matching
- [ ] LDAP injection in search terms is sanitized
- [ ] Pre-built security filters work, with configurable stale threshold
- [ ] Tree browsing loads lazily (one level at a time)
- [ ] Full attribute detail loads for selected objects with special type handling
- [ ] Group membership resolves recursively (tokenGroups) and directly (memberOf)
- [ ] `IDisposable` properly cleans up `DirectoryEntry` objects
- [ ] Cancellation and timeout enforced on all async methods
- [ ] All unit tests pass (13+ new cases)
- [ ] `dotnet build` — zero warnings
- [ ] `dotnet test` — all tests pass (Phase 0 + 1 + 2 + 3)
- [ ] Final commit: `chore: complete Phase 3 — Active Directory service layer`

---

## Improvements & Notes

1. **`IDirectorySearcherWrapper` for testability (step 3.13):** `DirectorySearcher` and `DirectoryEntry` are sealed classes from `System.DirectoryServices`. They cannot be mocked with NSubstitute. Create thin wrapper interfaces (`IDirectorySearcherWrapper`, `IDirectoryEntryFactory`) that the service depends on. This allows full unit testing of filter construction, result mapping, and domain switching logic without requiring a real AD environment.

2. **Locked accounts filter refinement (step 3.10):** The `lockoutTime>=1` filter catches all accounts with a non-zero lockout time, but some may have been auto-unlocked by the domain's lockout policy. The filter is correct for showing "accounts that have been locked" but may include false positives for "currently locked." For v1, this is acceptable — the UI should display the `lockoutTime` value so the user can assess manually.

3. **Cross-forest error handling (step 3.1):** If a cross-forest trust is one-way or inactive, `Forest.GetCurrentForest().Domains` may throw or return partial results. Each domain enumeration should be wrapped in a try/catch with graceful degradation — enumerate what you can, log warnings for failures, don't block on unreachable domains.

4. **Search debounce (UI concern, not service):** When `SearchAsync()` is wired to a text box in Phase 7, add a 300ms debounce delay in the ViewModel to avoid hammering AD with every keystroke. The service itself should not debounce — that's a UI concern.

5. **`InitializeAsync()` vs constructor I/O (step 3.4):** The constructor should not perform network operations. Use an initialization pattern — either a deferred `InitializeAsync()` called on first use, or explicit initialization during app startup in `App.xaml.cs`. This keeps the DI container fast.

6. **Pagination for large results:** The current implementation sets `SizeLimit = MaxResultsPerPage (500)`. For environments with thousands of matching objects, consider implementing a continuation token pattern or showing a "more results available" indicator. For v1, the 500 limit is acceptable with a clear "results truncated" message.

7. **Connection pooling:** `DirectoryEntry` and `DirectorySearcher` are relatively lightweight, but creating many in rapid succession (e.g., during tree browsing) can exhaust LDAP connections. Consider a pooling strategy or reusing the root `DirectoryEntry` for multiple searches.
