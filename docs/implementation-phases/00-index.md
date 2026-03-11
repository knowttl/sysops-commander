# SysOps Commander — Implementation Phase Index

> **Master index** for the granular, step-by-step implementation guides.
> Each phase file expands the high-level tasks from [SyOpsCommander-Design-Implementation.md](../../SyOpsCommander-Design-Implementation.md) into individually-numbered sub-steps with exact file paths, code conventions, verification criteria, and improvement notes.

---

## Phase Dependency Graph

Phases **must** be completed in order. Each phase builds on the outputs of the previous.

```
Phase 0  ──►  Phase 1  ──►  Phase 2  ──►  Phase 3  ──►  Phase 4
(Scaffolding)  (Models/DB)   (Validation)  (AD Service)  (Execution)
                                                              │
Phase 5  ◄────────────────────────────────────────────────────┘
(Scripts)
    │
    ▼
Phase 6  ──►  Phase 7  ──►  Phase 8  ──►  Phase 9  ──►  Phase 10
(UI Shell)    (AD Views)    (Exec View)   (Audit/Settings) (Polish)
```

---

## Phase Files

| # | File | Title | Sub-steps |
|---|------|-------|-----------|
| 0 | [phase-00-scaffolding.md](phase-00-scaffolding.md) | Project Scaffolding & Foundation | 25 |
| 1 | [phase-01-models-database.md](phase-01-models-database.md) | Core Models, Settings & Database | 41 |
| 2 | [phase-02-validation.md](phase-02-validation.md) | Validation Framework | 14 |
| 3 | [phase-03-active-directory.md](phase-03-active-directory.md) | Active Directory Service Layer | 15 |
| 4 | [phase-04-execution-engine.md](phase-04-execution-engine.md) | Remote Execution Engine | 18 |
| 5 | [phase-05-script-plugins.md](phase-05-script-plugins.md) | Script Plugin System | 15 |
| 6 | [phase-06-ui-shell.md](phase-06-ui-shell.md) | UI Shell & Navigation | 15 |
| 7 | [phase-07-ad-explorer-views.md](phase-07-ad-explorer-views.md) | AD Explorer Views | 12 |
| 8 | [phase-08-execution-view.md](phase-08-execution-view.md) | Execution View & Script Library | 15 |
| 9 | [phase-09-audit-settings-update.md](phase-09-audit-settings-update.md) | Audit Log, Settings & Auto-Update | 19 |
| 10 | [phase-10-polish-hardening.md](phase-10-polish-hardening.md) | Polish, Hardening & Testing | 17 |

---

## Authoritative Convention Overrides

The project ships with generic instruction files (`.github/instructions/`) and skill files (`.github/skills/`) that sometimes conflict with the project-specific `copilot-instructions.md`. **The project-specific file always wins.** This table resolves all 9 known conflicts:

| # | Topic | Generic File Says | **Authoritative Answer** | Source |
|---|-------|-------------------|--------------------------|--------|
| 1 | C# Version | `csharp.instructions.md` → "C# 14" | **C# 12** (.NET 8 max) | `copilot-instructions.md` |
| 2 | Test Framework | `dotnet-best-practices/SKILL.md` → "MSTest" | **xUnit** (`[Fact]`, `[Theory]`) | `copilot-instructions.md` |
| 3 | Mocking Framework | `dotnet-best-practices/SKILL.md` → "Moq" | **NSubstitute** (`.Returns()`, `.Received()`) | `copilot-instructions.md` |
| 4 | Assertions | `dotnet-architecture.instructions.md` → `Assert.Equal()` | **FluentAssertions** (`.Should().Be()`) | `copilot-instructions.md` |
| 5 | Logging | `dotnet-best-practices/SKILL.md` → "Microsoft.Extensions.Logging" | **Serilog** (File + Console, Compact JSON) | `copilot-instructions.md` |
| 6 | Data Access | `csharp.instructions.md` → "Entity Framework Core" | **SQLite + Dapper** (async, parameterized) | `copilot-instructions.md` |
| 7 | DI Container | `dotnet-wpf.instructions.md` → "Autofac, SimpleInjector" | **Microsoft.Extensions.DependencyInjection** | `copilot-instructions.md` |
| 8 | AAA Comments | `dotnet-architecture.instructions.md` uses section dividers | **Omit** — no AAA/section comments in tests | `csharp.instructions.md` |
| 9 | DDD Rigor | `dotnet-architecture.instructions.md` → full DDD (aggregates, domain events) | **MVVM + Services** — apply SOLID pragmatically, skip aggregates/domain events | `copilot-instructions.md` |

---

## Cross-Cutting Concerns Checklist

These rules apply to **every phase, every file, every commit**. Verify compliance before marking any step complete.

### Code Quality
- [ ] **Zero warnings.** `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>` in `Directory.Build.props`. Never suppress without documented justification.
- [ ] **XML documentation** on all public members. Follow `csharp-docs/SKILL.md` conventions: `<summary>` starts with present-tense 3rd-person verb.
- [ ] **File-scoped namespaces.** One type per file. PascalCase file names matching type names.
- [ ] **Nullable reference types enabled.** Use `is null` / `is not null`, not `== null`.
- [ ] **Self-explanatory code.** Comments explain WHY, not WHAT. No dead code, no changelog comments, no divider comments.

### Async & Threading
- [ ] **`CancellationToken`** on every async method. Thread through the entire call chain.
- [ ] **`ConfigureAwait(false)`** in Services and Infrastructure layers. Do NOT use in ViewModels.
- [ ] **`Async` suffix** on all async methods.
- [ ] **Never use `.Wait()`, `.Result`, or `.GetAwaiter().GetResult()`.** Use `await`.
- [ ] **`ValueTask<T>`** for hot-path methods where allocation matters.

### Security
- [ ] **Never store credentials.** Passwords never appear in logs, DB, settings, config, or temp files.
- [ ] **`CredentialDestructuringPolicy`** active — `SecureString`, `PSCredential`, `NetworkCredential` → `"[REDACTED]"` in all Serilog output.
- [ ] **Parameterized queries only** in Dapper. No string concatenation in SQL.
- [ ] **`AddParameter()` only** for PowerShell script parameters. Never string interpolation.
- [ ] **LDAP input sanitized** via `LdapFilterSanitizer` before every AD query that includes user input.

### Architecture
- [ ] **Interface-first.** Define interface in Core → Implement in Services/Infrastructure → Register in DI → Inject via constructor.
- [ ] **`IHostTargetingService` is a SINGLETON.** Shared between AD Explorer and Execution views. Verified by test.
- [ ] **MVVM strict separation.** ViewModels never reference WPF types (`System.Windows.*`). Views bind via `DataContext`.
- [ ] **CommunityToolkit.Mvvm** source generators: `[ObservableProperty]`, `[RelayCommand]`, `ObservableObject`.

### Source Control
- [ ] **Conventional commits** per `.github/skills/conventional-commit/SKILL.md`: `type(scope): description` in imperative mood.
- [ ] **Commit after each meaningful sub-step.** Not after every line, but after each logically complete unit.
- [ ] **Run all existing tests** after every commit. Never break previously passing tests.

### Testing
- [ ] **xUnit** with `[Fact]` and `[Theory]`.
- [ ] **NSubstitute** for mocking (`.Returns()`, `.Received()`).
- [ ] **FluentAssertions** for assertions (`.Should().Be()`, `.Should().BeEquivalentTo()`).
- [ ] **Naming:** `MethodName_Scenario_ExpectedBehavior`.
- [ ] **No AAA comments.** Test code should be self-explanatory.

---

## Missing Design Document Note

The implementation plan references **`SysOpsCommander_DesignDocument.docx (v1.0, March 2026)`** as the source-of-truth design document. **This file does not exist in the workspace.** The [SyOpsCommander-Design-Implementation.md](../../SyOpsCommander-Design-Implementation.md) serves as the de facto design reference for all phases.

---

## Commit Message Format Reference

Per `.github/skills/conventional-commit/SKILL.md`:

```
<type>(<scope>): <description>

[optional body]

[optional footer]
```

**Types:** `feat`, `fix`, `docs`, `style`, `refactor`, `perf`, `test`, `build`, `ci`, `chore`, `revert`

**Scopes for this project:** `core`, `services`, `infrastructure`, `viewmodels`, `app`, `tests`, `scripts`, `docs`

**Examples:**
- `feat(core): add WinRmConnectionOptions model with auth/transport enums`
- `build(app): configure Serilog with rolling file sink and credential destructuring`
- `test(services): add HostnameValidator unit tests for injection character rejection`
- `docs: create Phase 0 implementation guide`
