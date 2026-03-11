# Phase 2: Validation Framework

> **Goal:** Build all validation logic before the features that depend on it. Hostname validation, LDAP input sanitization, manifest schema validation, and script syntax/safety validation.
>
> **Prereqs:** Phase 1 complete (all models, enums, `ValidationResult`, `ManifestValidationResult` defined).
>
> **Outputs:** Four validators fully implemented and tested with 25+ test cases total.

---

## Sub-Steps

### 2.1 — Implement `HostnameValidator.cs` — Core Validation Methods

**File:** `src/SysOpsCommander.Core/Validation/HostnameValidator.cs`

**Design:** Static class with no dependencies (pure validation logic).

**Public methods:**
```csharp
public static ValidationResult Validate(string hostname)
public static ValidationResult ValidateNetBios(string hostname)
public static ValidationResult ValidateFqdn(string hostname)
public static ValidationResult ValidateIpv4(string hostname)
```

**`Validate()` dispatcher logic:**
1. If null, empty, or whitespace → `ValidationResult.Failure("Hostname cannot be empty.")`
2. Check for injection characters: `;`, `|`, `&`, `$`, `` ` ``, `(`, `)` → `ValidationResult.Failure("Hostname contains disallowed characters: {chars}")`
3. Try IPv4 → if matches, validate
4. Try FQDN (contains `.`) → validate
5. Otherwise → try NetBIOS → validate
6. If nothing matches → `ValidationResult.Failure("Hostname format is not recognized.")`

**NetBIOS rules:**
- Max 15 characters
- Alphanumeric + hyphens only (`[a-zA-Z0-9-]`)
- Cannot start or end with hyphen
- Cannot be all digits

**FQDN rules:**
- Dot-separated labels
- Each label 1–63 characters
- Each label: `[a-zA-Z0-9-]`, no leading/trailing hyphens
- Total max 253 characters

**IPv4 rules:**
- Four dot-separated octets
- Each octet 0–255
- No leading zeros (e.g., "01.02.03.04" is invalid)

> **Improvement:** Added `ValidateMany()` for batch validation:
> ```csharp
> public static IReadOnlyList<(string Hostname, ValidationResult Result)> ValidateMany(IEnumerable<string> hostnames)
> ```
> Used by `HostTargetingService.AddFromHostnames()` in Phase 4.

**Verification:**
- [ ] All valid formats accepted
- [ ] Injection characters rejected
- [ ] Commit: `feat(core): implement HostnameValidator with NetBIOS, FQDN, IPv4 validation`

---

### 2.2 — Implement `LdapFilterSanitizer.cs` — Input Escaping

**File:** `src/SysOpsCommander.Core/Validation/LdapFilterSanitizer.cs`

**Design:** Static class.

**Public methods:**
```csharp
public static string SanitizeInput(string raw)
public static string BuildSafeFilter(string attribute, string value)
```

**`SanitizeInput()` — RFC 4515 character escaping:**

| Character | Escape Sequence |
|-----------|----------------|
| `\` | `\5c` |
| `*` | `\2a` |
| `(` | `\28` |
| `)` | `\29` |
| NUL (0x00) | `\00` |

> **Critical:** Backslash must be escaped **first** to avoid double-escaping.

**`BuildSafeFilter()` template:**
```csharp
return $"({attribute}={SanitizeInput(value)})";
```

**Conventions:**
- `ArgumentNullException.ThrowIfNull()` for null inputs
- Return empty string for empty input (don't throw)
- XML docs with `<example>` showing input → output

**Verification:**
- [ ] All 5 special characters correctly escaped
- [ ] Nested injection attempts blocked (e.g., `admin)(objectClass=*` → properly escaped)
- [ ] Commit: `feat(core): implement LdapFilterSanitizer with RFC 4515 escaping`

---

### 2.3 — Implement `ManifestSchemaValidator.cs` — Manifest Validation

**File:** `src/SysOpsCommander.Core/Validation/ManifestSchemaValidator.cs`

**Design:** Static class or injectable service (no external dependencies).

**Public methods:**
```csharp
public static ManifestValidationResult Validate(ScriptManifest manifest)
```

**Validation rules (each violation adds to the errors list):**

1. **Required fields present and non-empty:**
   - `Name`, `Description`, `Version`, `Author`, `Category`
   - Error: `"Required field '{field}' is missing or empty."`

2. **Version format:**
   - Must match `^\d+\.\d+\.\d+$` (strict semver major.minor.patch)
   - Error: `"Version '{value}' is not in semver format (major.minor.patch)."`

3. **Category is valid:**
   - Must be one of a predefined set (define as `AllowedCategories` static array)
   - Suggested categories: `"Security"`, `"Inventory"`, `"Diagnostics"`, `"Remediation"`, `"Compliance"`, `"Network"`, `"Uncategorized"`
   - Error: `"Category '{value}' is not recognized. Allowed values: {list}"`

4. **`DangerLevel` is valid:**
   - Must map to `ScriptDangerLevel` enum
   - Warning (not error) if missing — defaults to `Safe`

5. **`OutputFormat` is valid:**
   - Must be one of `text`, `table`, `json`
   - Warning if missing — defaults to `text`

6. **Parameters (if present):**
   - Each parameter `Name` is non-empty: Error if empty
   - Each parameter `Type` is valid (`string`, `int`, `bool`, `choice`): Error if invalid
   - `choice` type requires non-empty `Choices` array: Error if missing/empty
   - No duplicate parameter names (case-insensitive): Error if duplicates found

**Verification:**
- [ ] Valid manifests pass with no errors
- [ ] Each violation type produces the correct error message
- [ ] Commit: `feat(core): implement ManifestSchemaValidator for script manifest validation`

---

### 2.4 — Implement `ScriptValidationService.cs` — Syntax Validation

**File:** `src/SysOpsCommander.Services/ScriptValidationService.cs`

**Dependencies:**
- `Serilog.ILogger` (injected)

**Method 1: `ValidateSyntaxAsync(string scriptPath, CancellationToken ct)`**

**Implementation:**
1. Read script content from file
2. Use `System.Management.Automation.Language.Parser.ParseFile()` to parse
3. Collect all `ParseError` objects
4. Map each to a `ScriptValidationError` record: `{ Line, Column, Message }`
5. Return `ScriptSyntaxResult` with `IsValid`, `Errors`

**Error handling:**
- File not found → specific error message
- File read permission denied → specific error message
- Log at `Debug` level: "Validating script syntax: {scriptPath}"

**Commit:** `feat(services): implement ScriptValidationService syntax validation via PowerShell AST`

---

### 2.5 — Implement `ScriptValidationService.cs` — Dangerous Pattern Detection

**Method 2: `DetectDangerousPatternsAsync(string scriptPath, CancellationToken ct)`**

**Implementation:**
1. Parse the script with `Parser.ParseFile()` to get the AST
2. Walk the AST looking for `CommandAst` nodes matching dangerous cmdlets:

| Cmdlet | Condition | Danger Level |
|--------|-----------|-------------|
| `Remove-Item` | Has both `-Recurse` and `-Force` switches | Destructive |
| `Format-Volume` | Any usage | Destructive |
| `Stop-Computer` | Any usage | Destructive |
| `Restart-Computer` | Any usage | Caution |
| `Clear-EventLog` | Any usage | Destructive |
| `Set-ExecutionPolicy` | Any usage | Caution |
| `Disable-NetAdapter` | Any usage | Destructive |
| `Stop-Service` | On critical services* | Caution |

*Critical services: `WinRM`, `WinDefend`, `EventLog`, `Netlogon`, `NTDS`, `DNS`

3. Return list of `DangerousPatternWarning`: `{ Line, CmdletName, Reason, DangerLevel }`
4. The highest `DangerLevel` found across all warnings becomes the script's `EffectiveDangerLevel`

**Conventions:**
- Use `AstVisitor` or `FindAll()` on the AST to walk nodes
- Log at `Warning` level when destructive patterns found

**Commit:** `feat(services): implement dangerous pattern detection via AST walking`

---

### 2.6 — Implement `ScriptValidationService.cs` — Manifest-Script Pair Validation

**Method 3: `ValidateManifestPairAsync(string ps1Path, CancellationToken ct)`**

**Implementation:**
1. Derive JSON manifest path: replace `.ps1` extension with `.json`
2. If manifest doesn't exist → return warning (not error): "No manifest found for {filename}. Script will load as a simple drop-in."
3. If manifest exists:
   a. Deserialize JSON into `ScriptManifest`
   b. Run `ManifestSchemaValidator.Validate()` — include any errors/warnings
   c. Parse the `.ps1` file's `param()` block via AST
   d. Compare parameter names in manifest vs. `param()` block:
      - Params in manifest but not in script → Warning: "Manifest parameter '{name}' not found in script param() block."
      - Params in script but not in manifest → Info (not warning): parameters in the script are authoritative

**Commit:** `feat(services): implement manifest-script pair validation`

---

### 2.7 — Implement `ScriptValidationService.cs` — CredSSP Availability Check

**Method 4: `ValidateCredSspAvailabilityAsync(string hostname, CancellationToken ct)`**

**Implementation:**
1. Attempt a WinRM connection test to the host using CredSSP auth
2. If connection fails with auth error → return `ValidationResult.Failure()` with message:
   ```
   "CredSSP authentication is not configured on {hostname}. This requires GPO configuration on both client and server. See: https://learn.microsoft.com/en-us/powershell/module/microsoft.wsman.management/enable-wsmancredssp"
   ```
3. If connection succeeds → return `ValidationResult.Success()`
4. If host unreachable → return `ValidationResult.Failure("Host {hostname} is not reachable.")`

**Conventions:**
- Wrap WinRM connection in try/catch
- Timeout after `AppConstants.DefaultWinRmTimeoutSeconds`
- Log at `Information` level: "Testing CredSSP availability on {hostname}"

**Commit:** `feat(services): implement CredSSP availability validation`

---

### 2.8 — Create Supporting Types for Validation Results

**Files:**
- `src/SysOpsCommander.Core/Validation/ScriptSyntaxResult.cs`
- `src/SysOpsCommander.Core/Validation/ScriptValidationError.cs`
- `src/SysOpsCommander.Core/Validation/DangerousPatternWarning.cs`

**`ScriptSyntaxResult`:**
- `bool IsValid`
- `IReadOnlyList<ScriptValidationError> Errors`

**`ScriptValidationError`:**
- `int Line`
- `int Column`
- `string Message`

**`DangerousPatternWarning`:**
- `int Line`
- `string CmdletName`
- `string Reason`
- `ScriptDangerLevel DangerLevel`

**Commit:** `feat(core): add script validation result types`

---

### 2.9 — Write `HostnameValidatorTests.cs`

**File:** `tests/SysOpsCommander.Tests/Validation/HostnameValidatorTests.cs`

**Test cases (10+):**

| # | Test Name | Input | Expected |
|---|-----------|-------|----------|
| 1 | `Validate_ValidNetBios_ReturnsSuccess` | `"SERVER01"` | Valid |
| 2 | `Validate_ValidFqdn_ReturnsSuccess` | `"server01.corp.contoso.com"` | Valid |
| 3 | `Validate_ValidIpv4_ReturnsSuccess` | `"192.168.1.100"` | Valid |
| 4 | `Validate_EmptyString_ReturnsFailure` | `""` | Invalid |
| 5 | `Validate_Whitespace_ReturnsFailure` | `"  "` | Invalid |
| 6 | `Validate_InjectionSemicolon_ReturnsFailure` | `"server;ls"` | Invalid |
| 7 | `Validate_InjectionPipe_ReturnsFailure` | `"server|cmd"` | Invalid |
| 8 | `Validate_InjectionDollar_ReturnsFailure` | `"$env:PATH"` | Invalid |
| 9 | `Validate_NetBiosTooLong_ReturnsFailure` | `"ABCDEFGHIJKLMNOP"` (16 chars) | Invalid |
| 10 | `Validate_NetBiosLeadingHyphen_ReturnsFailure` | `"-SERVER"` | Invalid |
| 11 | `Validate_FqdnLabelTooLong_ReturnsFailure` | 64-char label | Invalid |
| 12 | `Validate_Ipv4OctetOutOfRange_ReturnsFailure` | `"256.1.1.1"` | Invalid |
| 13 | `ValidateMany_MixedInput_ReturnsCorrectResults` | Mix of valid/invalid | Mixed |

**Use `[Theory]` with `[InlineData]`** for the parameterized cases.

**Commit:** `test(validation): add HostnameValidator tests (13 cases)`

---

### 2.10 — Write `LdapFilterSanitizerTests.cs`

**File:** `tests/SysOpsCommander.Tests/Validation/LdapFilterSanitizerTests.cs`

**Test cases (8+):**

| # | Test Name | Input | Expected |
|---|-----------|-------|----------|
| 1 | `SanitizeInput_Asterisk_EscapedCorrectly` | `"admin*"` | `"admin\2a"` |
| 2 | `SanitizeInput_OpenParen_EscapedCorrectly` | `"admin("` | `"admin\28"` |
| 3 | `SanitizeInput_CloseParen_EscapedCorrectly` | `"admin)"` | `"admin\29"` |
| 4 | `SanitizeInput_Backslash_EscapedCorrectly` | `"admin\"` | `"admin\5c"` |
| 5 | `SanitizeInput_NulChar_EscapedCorrectly` | `"admin\0"` | `"admin\00"` |
| 6 | `SanitizeInput_InjectionAttempt_FullyEscaped` | `"admin)(objectClass=*"` | All specials escaped |
| 7 | `SanitizeInput_EmptyString_ReturnsEmpty` | `""` | `""` |
| 8 | `SanitizeInput_CleanInput_ReturnsUnchanged` | `"jsmith"` | `"jsmith"` |
| 9 | `BuildSafeFilter_ValidInput_ReturnsFormattedFilter` | `"cn", "jsmith"` | `"(cn=jsmith)"` |
| 10 | `BuildSafeFilter_InputWithSpecial_EscapesValue` | `"cn", "j*smith"` | `"(cn=j\2asmith)"` |

**Commit:** `test(validation): add LdapFilterSanitizer tests (10 cases)`

---

### 2.11 — Write `ManifestSchemaValidatorTests.cs`

**File:** `tests/SysOpsCommander.Tests/Validation/ManifestSchemaValidatorTests.cs`

**Test cases (8+):**

| # | Test Name | Scenario | Expected |
|---|-----------|----------|----------|
| 1 | `Validate_CompleteValidManifest_ReturnsNoErrors` | All fields valid | IsValid = true |
| 2 | `Validate_MissingName_ReturnsError` | Name is null/empty | Error about Name |
| 3 | `Validate_InvalidVersion_ReturnsError` | `"1.0"` (no patch) | Error about semver |
| 4 | `Validate_InvalidCategory_ReturnsError` | `"Unknown"` | Error about category |
| 5 | `Validate_DuplicateParameterNames_ReturnsError` | Two params named `"Filter"` | Error about duplicates |
| 6 | `Validate_ChoiceParamNoChoices_ReturnsError` | Type=choice, Choices=null | Error about choices |
| 7 | `Validate_InvalidParameterType_ReturnsError` | Type=`"datetime"` | Error about type |
| 8 | `Validate_MissingOptionalFields_ReturnsWarnings` | No DangerLevel, no OutputFormat | Warnings only, IsValid = true |

**Commit:** `test(validation): add ManifestSchemaValidator tests (8 cases)`

---

### 2.12 — Write `ScriptValidationServiceTests.cs`

**File:** `tests/SysOpsCommander.Tests/Services/ScriptValidationServiceTests.cs`

**Test cases (6+):**

| # | Test Name | Scenario | Expected |
|---|-----------|----------|----------|
| 1 | `ValidateSyntax_ValidScript_ReturnsNoErrors` | Syntactically correct .ps1 | IsValid = true |
| 2 | `ValidateSyntax_SyntaxError_ReturnsErrorWithLineNumber` | Missing closing brace | Error with line/column |
| 3 | `DetectDangerousPatterns_RemoveItemRecurseForce_DetectsDestructive` | `Remove-Item -Recurse -Force` | DangerLevel = Destructive |
| 4 | `DetectDangerousPatterns_StopComputer_DetectsDestructive` | `Stop-Computer` | Detected |
| 5 | `DetectDangerousPatterns_SafeScript_NoWarnings` | `Get-Process` only | Empty warnings |
| 6 | `ValidateManifestPair_ParameterMismatch_ReturnsWarning` | Manifest has param not in script | Warning returned |
| 7 | `ValidateManifestPair_NoManifest_ReturnsWarning` | No .json file exists | Warning about missing manifest |

**Setup:** Create temp `.ps1` files for testing, or use embedded string content parsed via `Parser.ParseInput()`.

**Commit:** `test(services): add ScriptValidationService tests (7 cases)`

---

### 2.13 — Register `ScriptValidationService` in DI

**Action:** Add DI registration in `ServiceCollectionExtensions.cs`:
```csharp
services.AddSingleton<ScriptValidationService>();
```

> **Note:** If `ScriptValidationService` needs an interface for testability, define `IScriptValidationService` in Core. The plan doesn't define one, but it's recommended for consistency with the interface-first pattern.

**Commit:** `build(app): register ScriptValidationService in DI container`

---

### 2.14 — Phase 2 Verification

**Full acceptance criteria check:**
- [ ] Hostname validation correctly accepts/rejects all expected patterns including injection characters
- [ ] LDAP sanitizer escapes all RFC 4515 special characters
- [ ] Manifest validator catches all schema violations
- [ ] Script syntax validation returns parse errors with line numbers
- [ ] Dangerous pattern detection identifies all specified cmdlets
- [ ] CredSSP availability check returns clear error when not configured
- [ ] All validation unit tests pass (25+ cases total)
- [ ] `dotnet build` — zero warnings
- [ ] `dotnet test` — all tests pass (Phase 0 + Phase 1 + Phase 2)
- [ ] Final commit: `chore: complete Phase 2 — validation framework`

---

## Improvements & Notes

1. **IPv6 support consideration:** The plan validates only IPv4 addresses, but enterprise networks increasingly use IPv6. At minimum, `HostnameValidator.Validate()` should accept IPv6 addresses (e.g., `::1`, `fe80::1%eth0`) without crashing, even if full validation is deferred to v2. Add a `ValidateIpv6()` stub that accepts well-formed IPv6 and returns `Success`.

2. **`HostnameValidator` as static vs. injectable:** Made static since it has no dependencies. This is simpler but means it can't be mocked in tests that call it indirectly. For services that use it internally (like `HostTargetingService`), this is fine — validate the behavior through the service's own tests.

3. **`ValidateMany()` batch method added (step 2.1):** `HostTargetingService.AddFromHostnames()` validates multiple hostnames. A batch method avoids repeated overhead and returns all results in one call with hostname-to-result mapping.

4. **Configurable dangerous patterns (future v2):** The dangerous pattern list is hardcoded. For v2, consider loading patterns from a configuration file or manifest that admins can customize. For v1, the hardcoded list is sufficient.

5. **`IScriptValidationService` interface recommended (step 2.13):** The plan places `ScriptValidationService` in the Services project but doesn't define a corresponding interface in Core. For consistency with the interface-first pattern and to enable mocking in ViewModel tests, consider adding `IScriptValidationService`. This is a minor deviation from the plan but aligns with the project's architectural rules.

6. **AST walking approach:** The plan recommends `Parser.ParseFile()`, but for unit tests it's easier to use `Parser.ParseInput()` with string content. Both return the same AST types. The service should support both paths — file-based for production, string-based for testing (or test via temp files).

7. **CredSSP validation is an integration test concern:** Step 2.7 (`ValidateCredSspAvailabilityAsync`) requires a real WinRM connection to test. Unit tests should mock this. The real validation should be tested manually in a lab environment during Phase 10.
