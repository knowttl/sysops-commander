# SysOps Commander — UI Redesign Specification

**Version:** 1.0
**Date:** March 13, 2026
**Author:** Brytton (Owner/Developer)
**Target:** WPF / C# Desktop Application
**Scope:** Full application — all views (Dashboard, AD Explorer, Execution, Script Library, Audit Log, Settings)

---

## 1. Executive Summary

This document specifies a layout restructure, visual refresh, and feature additions for SysOps Commander. The redesign addresses three core pain points: clunky tree navigation, hard-to-read search results, and a cramped object details panel. The approach is a **layout restructure + visual refresh** — redesigning the panel architecture first, applying a new global theme, then building features into the new structure.

All theme and style changes described in this document **must be implemented as a global `ResourceDictionary`** that applies across every view in the application. No view should carry its own one-off color, font, or spacing overrides. Every control template, brush, and style referenced below lives in the shared dictionary and is inherited by all pages.

---

## 2. Layout Architecture

### 2.1 Panel Structure

Replace the current fixed three-panel layout with a four-zone `Grid` using `GridSplitter` dividers. The XAML skeleton:

```xml
<Grid>
    <Grid.RowDefinitions>
        <RowDefinition Height="*" />          <!-- Main content -->
        <RowDefinition Height="28" />         <!-- Status bar -->
    </Grid.RowDefinitions>
    <Grid.ColumnDefinitions>
        <ColumnDefinition Width="280" MinWidth="40" MaxWidth="450" />   <!-- Zone 1 -->
        <ColumnDefinition Width="Auto" />                                <!-- GridSplitter -->
        <ColumnDefinition Width="*" />                                   <!-- Zone 2 -->
        <ColumnDefinition Width="Auto" />                                <!-- GridSplitter -->
        <ColumnDefinition Width="320" MinWidth="40" MaxWidth="600" />   <!-- Zone 3 -->
    </Grid.ColumnDefinitions>

    <!-- Zone 1: OU Navigator -->
    <!-- GridSplitter -->
    <!-- Zone 2: Tabbed Workspace -->
    <!-- GridSplitter -->
    <!-- Zone 3: Detail Inspector -->
    <!-- Zone 4: Status Bar (spans all columns) -->
</Grid>
```

### 2.2 Zone 1 — OU Navigator (Left Panel)

- **Default width:** 280px | **Min:** 200px expanded, 40px collapsed | **Max:** 450px
- **Collapsible:** Chevron button (`«` / `»`) in the panel header toggles between expanded and a 40px icon rail
- **Collapsed rail:** Shows only the Powerex.ca domain icon and the expand chevron. No text.
- **Contains (top to bottom):**
  1. **Panel header** — "OU Navigator" label + collapse chevron (right-aligned)
  2. **Tree search box** — `TextBox` with placeholder "Filter OUs..." that filters the tree in real-time as the user types. Non-matching branches are hidden; matching nodes and their parent chain remain visible.
  3. **Breadcrumb bar** — Displays the full DN path of the currently selected OU as clickable segments (e.g., `DC=Powerex` > `DC=ca` > `OU=POWEREX`). Clicking a segment navigates the tree to that level.
  4. **OU TreeView** — The AD tree with improved styling (see Section 3.4)
- **`GridSplitter`** on the right edge for drag-to-resize

### 2.3 Zone 2 — Tabbed Workspace (Center Panel)

- **Width:** Fills all remaining horizontal space (`*`)
- **Contains:**
  1. **Tab bar** — `TabControl` where each tab represents an independent search session. A `+` button adds a new tab. Tabs are closeable (×) but at least one tab must remain open. Middle-click on a tab also closes it.
  2. **Advanced search bar** (per tab) — See Section 4 for full spec
  3. **Results `DataGrid`** (per tab) — Sortable columns: Name, Class, Display Name, Distinguished Name. Columns are resizable and reorderable. Row selection populates Zone 3.
  4. **Footer row** — Export button (CSV / clipboard), live result count (e.g., "42 results"), and pagination if results exceed threshold (500+)

### 2.4 Zone 3 — Detail Inspector (Right Panel)

- **Default width:** 320px | **Min:** 250px expanded, 40px collapsed | **Max:** 600px
- **Collapsible:** Chevron button (`»` / `«`) in the panel header toggles between expanded and a 40px icon rail
- **Collapsed rail:** Shows only the selected object's type icon (User/Computer/Group badge) and the expand chevron. If no object is selected, shows a generic info icon.
- **Expandable:** A `⤢` button in the panel header expands the inspector to a full-width overlay (covers Zone 2), for deep attribute inspection. Press `Esc` or click the collapse button to return to normal panel mode.
- **Contains (top to bottom):**
  1. **Panel header** — "Detail Inspector" label + collapse chevron (left-aligned) + expand button (right-aligned)
  2. **Object header card** — Icon (based on objectClass) + object name (CN) + object type label + DN path (truncated, tooltip for full path)
  3. **Sub-tab control** with three tabs:
     - **Attributes** — Key-value list of all AD attributes. Attribute names in tertiary text, values in primary text. Timestamps rendered as human-readable relative time (e.g., "2 hours ago") with the raw value in a tooltip. SIDs rendered with resolved name where possible. Binary values shown as hex with byte count. Long values are truncated with "Show more" expander.
     - **Group Membership** — List of groups the object belongs to (direct + nested, with a toggle). Each group is clickable to navigate to it. For Group objects, this tab also shows a "Show Members" button that opens a new workspace tab with the group's members.
     - **Permissions** — Effective permissions / ACL entries for the selected object. Read-only display.
- **Empty state:** When no object is selected, show a muted message: "Select an object to view details" with a subtle icon.
- **`GridSplitter`** on the left edge for drag-to-resize

### 2.5 Zone 4 — Status Bar (Bottom)

- **Height:** 28px, always visible, spans all columns
- **Contains (left to right):**
  1. **Connection indicator** — Green/red dot + domain name (e.g., "Connected: Powerex.ca"). Clicking opens connection settings.
  2. **Current OU path** — Shows the DN of the selected OU from Zone 1 (truncated with tooltip)
  3. **Object count** — Number of objects in the current result set (e.g., "42 objects")
  4. **Last refresh timestamp** — Time of last query execution (e.g., "14:32:01")

### 2.6 Collapse Behavior

Both side panels collapse independently. When a panel collapses:
- Its `ColumnDefinition.Width` animates from current width to 40px (use `DoubleAnimation` on a binding proxy or set width directly)
- The center workspace (`*` column) automatically absorbs the freed space
- The `GridSplitter` adjacent to the collapsed panel becomes non-interactive (set `IsEnabled="False"`)
- Keyboard shortcuts: `Ctrl+1` toggles Zone 1, `Ctrl+3` toggles Zone 3

**Possible combined states:**
| Zone 1 | Zone 3 | Workspace gets |
|--------|--------|----------------|
| Expanded | Expanded | Remaining space |
| Collapsed | Expanded | More space (Zone 1 frees ~240px) |
| Expanded | Collapsed | More space (Zone 3 frees ~280px) |
| Collapsed | Collapsed | Maximum space (both rails = 80px total) |

---

## 3. Visual Theme (Global)

### 3.1 Implementation Strategy

All colors, brushes, fonts, and control templates defined below **must live in a single shared `ResourceDictionary`** (e.g., `Themes/DarkTheme.xaml`) that is merged into `App.xaml`:

```xml
<Application.Resources>
    <ResourceDictionary>
        <ResourceDictionary.MergedDictionaries>
            <ResourceDictionary Source="Themes/DarkTheme.xaml" />
        </ResourceDictionary.MergedDictionaries>
    </ResourceDictionary>
</Application.Resources>
```

Every view in the application (Dashboard, AD Explorer, Execution, Script Library, Audit Log, Settings) inherits from this dictionary. **No view may define its own brushes or override theme colors locally.** If a view needs a semantic alias (e.g., `TreeNodeHoverBrush`), define it in the shared dictionary pointing to the appropriate surface brush.

### 3.2 Color Palette

#### Backgrounds (Four-tier depth system)

| Token | Hex | Usage |
|-------|-----|-------|
| `BackgroundBase` | `#1B1B1F` | App shell, status bar, title bar |
| `Surface1` | `#252529` | Panel backgrounds (all three zones) |
| `Surface2` | `#2E2E34` | Cards, search bar, active tab, selected row, input fields |
| `Surface3` | `#38383F` | Hover states, dropdowns, context menus, tooltips |

#### Text (Four-tier hierarchy, all WCAG AA compliant against Surface1)

| Token | Hex | Contrast vs Surface1 | Usage |
|-------|-----|----------------------|-------|
| `TextPrimary` | `#EDEDEF` | 13.5:1 | Object names, attribute values, selected items |
| `TextSecondary` | `#A9A9B2` | 7.2:1 | Column headers, tab labels, breadcrumbs, panel titles |
| `TextTertiary` | `#6F6F7A` | 4.1:1 | Attribute names, placeholders, timestamps, captions |
| `TextAccent` | `#6BA3E8` | 5.8:1 | Links, active filters, selected tab indicator, clickable items |

#### Functional Accents

| Token | Hex | Usage |
|-------|-----|-------|
| `AccentBlue` | `#6BA3E8` | Selected tree node, active tab, focused input border, links |
| `AccentGreen` | `#4DAA7A` | Connection indicator (healthy), success states |
| `AccentAmber` | `#D4A73A` | Warnings, stale objects, expiring items |
| `AccentRed` | `#D4585A` | Errors, disconnected state, locked accounts |

#### Borders

| Token | Hex | Usage |
|-------|-----|-------|
| `BorderSubtle` | `#2E2E34` | Panel dividers, grid lines, subtle separators |
| `BorderDefault` | `#38383F` | Input borders (idle), card borders, splitters |
| `BorderFocus` | `#6BA3E8` | Focused input border, active control outline |

### 3.3 Typography

**Font family:** Segoe UI (WPF system default — excellent legibility at small sizes on Windows)

| Role | Size | Weight | Color Token | WPF Style Key |
|------|------|--------|-------------|---------------|
| Panel title | 13px | SemiBold (600) | `TextSecondary` | `PanelTitleStyle` |
| Body text | 12.5px | Regular (400) | `TextPrimary` | `BodyTextStyle` |
| Column header | 11.5px | SemiBold (600) | `TextSecondary` | `ColumnHeaderStyle` |
| Label / attribute name | 11.5px | Regular (400) | `TextTertiary` | `LabelStyle` |
| Caption / status bar | 11px | Regular (400) | `TextTertiary` | `CaptionStyle` |
| Monospace (DN, SID, LDAP) | 11.5px Consolas | Regular (400) | `TextPrimary` | `MonoStyle` |

### 3.4 Control-Specific Styling

#### TreeView (Zone 1)

- **Icon set (14×14px each):** Consistent iconography per objectClass:
  - OU / Container: Folder icon — `TextSecondary` color, `TextAccent` when selected
  - User: Person silhouette icon
  - Computer: Monitor icon
  - Group: Two-person icon
  - Domain root: Globe icon
  - Generic: Document icon
- **Indentation:** 20px per nesting level (increase from current ~12px)
- **Row height:** 26px with 4px vertical padding
- **Idle state:** Transparent background, `TextPrimary` label
- **Hover state:** `Surface3` background, full row width highlight
- **Selected state:** `Surface2` background + 2px left accent border (`AccentBlue`), `TextPrimary` label
- **Expand/collapse chevrons:** Small triangle glyphs (▶ / ▼), `TextTertiary` color, rotate animation on toggle

#### DataGrid (Zone 2)

- **Header row:** `Surface2` background, `TextSecondary` labels at 11.5px SemiBold. Sort indicator arrows next to sortable column names.
- **Body rows:** Alternating `Surface1` / transparent — subtle striping for scanability
- **Row height:** 28px minimum, auto-grow for wrapped content
- **Hover state:** `Surface3` background, full row
- **Selected state:** `Surface2` background + 2px left accent border (`AccentBlue`)
- **Multi-select:** `Ctrl+Click` and `Shift+Click` supported. Selected rows share the same visual treatment.
- **Column resizing:** Drag column header edges. Double-click to auto-fit.
- **Column reordering:** Drag column headers to rearrange.
- **Right-click context menu:** Copy row, Copy DN, Search within this group (if group), Open in new tab, Export selected

#### TabControl (Zone 2 tab bar)

- **Active tab:** `Surface2` background, `TextPrimary` label, 2px bottom border in `AccentBlue`
- **Inactive tab:** Transparent background, `TextSecondary` label
- **Hover (inactive):** `Surface3` background
- **Close button:** Small `×` glyph on each tab, appears on hover. `TextTertiary`, turns `AccentRed` on hover.
- **New tab button:** `+` glyph in a pill-shaped button after the last tab

#### Inputs (TextBox, ComboBox)

- **Idle:** `Surface2` background, `BorderDefault` border, `TextPrimary` text, `TextTertiary` placeholder
- **Hover:** `BorderDefault` lightens slightly
- **Focused:** `BorderFocus` border (2px), subtle glow (1px box-shadow equivalent via `Effect`)
- **Corner radius:** 4px

#### Buttons

- **Primary:** `AccentBlue` background, white text, 4px corner radius. Hover: slightly lighter. Active: slightly darker.
- **Secondary / Outline:** Transparent background, `BorderDefault` border, `TextSecondary` label. Hover: `Surface3` fill.
- **Icon buttons (chevrons, expand, export):** Transparent background, `TextSecondary` icon. Hover: `Surface3` circle/pill background.

#### Scrollbars

- **Track:** Transparent
- **Thumb:** `Surface3` with 4px width, appears on hover/scroll, fades after 1.5s idle
- **Corner radius:** 2px (pill-shaped thumb)

#### Tooltips

- **Background:** `Surface3`
- **Border:** `BorderDefault`, 1px
- **Text:** `TextPrimary`, 11.5px
- **Corner radius:** 4px
- **Shadow:** Subtle drop shadow (2px blur, 10% opacity black)

#### Context Menus

- **Background:** `Surface2`
- **Border:** `BorderDefault`, 1px
- **Item hover:** `Surface3`
- **Text:** `TextPrimary` for labels, `TextTertiary` for keyboard shortcuts
- **Separator:** `BorderSubtle`, 1px horizontal line
- **Corner radius:** 6px
- **Shadow:** 4px blur, 15% opacity black

---

## 4. Advanced Search & Filtering

### 4.1 Search Bar Layout (per workspace tab)

The search bar sits at the top of each workspace tab and contains three stacked rows:

**Row 1 — Input row:**
```
[Attribute dropdown ▼] [Search text input                    ] [Search button]
```
- **Attribute dropdown (`ComboBox`):** Options: "All attributes", samAccountName, CN, displayName, mail, SID, description, distinguishedName. Default: "All attributes". This determines which AD attribute the text query matches against.
- **Search text input (`TextBox`):** Placeholder text dynamically reflects the active object type filters (e.g., "Search users by name, email, or SID..." when Users pill is active). Pressing `Enter` executes the search.
- **Search button:** Primary style button with magnifying glass icon. Click or `Enter` to execute.

**Row 2 — Object type filter pills:**
```
[Users] [Computers] [Groups] [OUs] [Contacts] [All]
```
- Implemented as `ToggleButton` elements styled as pills (pill-shaped, `Surface2` background idle, `AccentBlue` background + white text when active)
- **Multi-select:** Multiple pills can be active simultaneously (e.g., Users + Computers). Under the hood, this builds an OR clause on `objectClass`.
- **"All" pill:** When clicked, deactivates all other pills. When any other pill is clicked, "All" deactivates. If all specific pills are deactivated, "All" re-activates automatically.
- **Mapping:** Users → `objectClass=user`, Computers → `objectClass=computer`, Groups → `objectClass=group`, OUs → `objectClass=organizationalUnit`, Contacts → `objectClass=contact`

**Row 3 — Scope context line:**
```
Searching in: OU=POWEREX,DC=Powerex,DC=ca  [× Reset to root]  [☐ Search entire domain]
```
- Displays the current search scope — defaults to whichever OU is selected in Zone 1
- **"Search entire domain" checkbox:** Overrides the scope to the domain root. When checked, scope line shows the root DN.
- **"× Reset to root" button:** Clears the OU scope and resets to domain root. Only visible when a specific OU or group scope is active.
- When in group-scoped mode (see Section 4.2), shows: `Members of: CN=Domain Admins,OU=Groups,...`

### 4.2 Relationship Queries (Group Membership Exploration)

**Entry point 1 — Context menu:**
1. User right-clicks a Group object in the results DataGrid
2. Context menu shows "Search within this group" option
3. Clicking opens a **new workspace tab** titled `Members of: [GroupName]`
4. The new tab's scope context line shows: `Members of: CN=[GroupName],[DN]`
5. Object type filter pills still work — user can click "Computers" to see only computer members, "Users" for only user members, etc.
6. The search text input still works — user can further filter members by name/attribute

**Entry point 2 — Detail Inspector:**
1. User selects a Group object in results
2. Zone 3 (Detail Inspector) populates, showing the Group Membership tab
3. A "Show all members" button in the Group Membership tab opens the same new workspace tab as above

**Implementation — LDAP query construction:**
- Direct members: `(memberOf=CN=[GroupName],[GroupDN])`
- Recursive/nested members: `(memberOf:1.2.840.113556.1.4.1941:=CN=[GroupName],[GroupDN])`
- A toggle in the scope context line: `[☐ Include nested members]` switches between direct and recursive
- Combined with object type pills: e.g., `(&(objectClass=computer)(memberOf:1.2.840.113556.1.4.1941:=CN=VPN Users,OU=Groups,DC=Powerex,DC=ca))`

**Workflow example — "find all computers in the VPN Users group":**
1. User types "VPN" in search bar with "Groups" pill active
2. Results show VPN Users, VPN Admins, etc.
3. Right-click VPN Users → "Search within this group"
4. New tab opens: `Members of: VPN Users`
5. Click "Computers" pill
6. Results show only computer objects that are members of VPN Users
7. Toggle "Include nested members" to see recursively nested members too

### 4.3 Additional Search Features

**Search history:**
- Dropdown arrow on the right edge of the search text input
- Shows the last 10 searches for the current tab (persisted in app settings)
- Each entry shows: query text + filter pills that were active + result count
- Click an entry to re-execute that search

**Saved searches:**
- Right-click a tab → "Save this search" stores: query text + attribute dropdown selection + active filter pills + scope
- Saved searches appear in a dropdown in the tab bar area or as entries in the left navigation
- Each saved search can be renamed and deleted

**LDAP filter mode (power user):**
- A `⚙ Advanced` toggle button in the search bar header
- When toggled, replaces Rows 1-2 (text input + pills) with a raw LDAP filter `TextBox` (monospace font, multi-line capable)
- User can type arbitrary LDAP filters: `(&(objectClass=user)(department=IT*)(!(userAccountControl:1.2.840.113556.1.4.803:=2)))`
- Scope context line (Row 3) still applies — the raw filter is AND-combined with the scope
- Toggle back to "Simple" mode restores the pills + text input (does NOT try to parse the raw filter back into pills)

---

## 5. Export Functionality

### 5.1 Export Button (Zone 2 footer)

Located in the bottom-left of the workspace tab, next to the result count.

**Export options (dropdown on the Export button):**
- **Copy to Clipboard** — Copies selected rows (or all if none selected) as tab-delimited text with headers. Keyboard shortcut: `Ctrl+C` (selected rows only).
- **Export to CSV** — Opens a Save File dialog. Exports all results (not just visible page) with all columns. UTF-8 encoding with BOM for Excel compatibility.
- **Export to Excel (.xlsx)** — If feasible, direct Excel export using a library like ClosedXML. Otherwise, CSV with .xlsx guidance in status bar.

### 5.2 Column Selection for Export

Before exporting, a small popup lets the user check/uncheck which columns to include. "All columns" is the default. This prevents exporting the full DN when the user only needs Name + Class.

---

## 6. Keyboard Shortcuts

| Shortcut | Action |
|----------|--------|
| `Ctrl+1` | Toggle Zone 1 (OU Navigator) collapse |
| `Ctrl+3` | Toggle Zone 3 (Detail Inspector) collapse |
| `Ctrl+F` | Focus the search text input in the active workspace tab |
| `Ctrl+T` | Open a new workspace tab |
| `Ctrl+W` | Close the active workspace tab (if more than one open) |
| `Ctrl+Tab` | Cycle to the next workspace tab |
| `Ctrl+Shift+Tab` | Cycle to the previous workspace tab |
| `Enter` | Execute search (when search input is focused) |
| `Escape` | Exit full-width inspector overlay / clear search input / deselect |
| `Ctrl+C` | Copy selected DataGrid rows to clipboard |
| `Ctrl+E` | Export results (opens export dropdown) |
| `F5` | Refresh current search results |
| `Ctrl+L` | Focus the tree search/filter box in Zone 1 |

---

## 7. Empty States & Loading

### 7.1 Empty States

Every panel should have a clear empty state rather than just blank space:

- **Zone 2 (no search executed):** Centered muted text: "Search for Active Directory objects using the bar above" with a subtle search icon. Optionally show recent/saved searches as quick-launch cards.
- **Zone 2 (search returned no results):** "No objects found matching your query" with suggestions: "Try broadening your search or changing the object type filters."
- **Zone 3 (no object selected):** "Select an object to view details" with a subtle info icon.
- **Zone 1 (tree filter has no matches):** "No OUs match your filter" with a "Clear filter" link.

### 7.2 Loading States

- **Tree loading:** Skeleton placeholder bars (3-4 animated shimmer rectangles) in Zone 1 while the tree loads
- **Search in progress:** A thin indeterminate progress bar at the top of Zone 2, below the tab bar. Search button changes to a "Cancel" button.
- **Detail loading:** Skeleton placeholders in Zone 3 while attributes are fetched
- **Connection attempt:** Status bar shows amber dot with "Connecting to Powerex.ca..." and a spinner

---

## 8. Implementation Priority

Recommended order for the AI agent to implement:

### Phase 1 — Foundation (Do first)
1. **Global `ResourceDictionary`** — Define all brushes, colors, fonts, and base control templates from Section 3. Merge into `App.xaml`.
2. **Apply theme to all existing views** — Ensure Dashboard, AD Explorer, Execution, Script Library, Audit Log, and Settings all inherit and display correctly with the new theme. Fix any hardcoded colors or styles in individual views.
3. **Layout restructure (AD Explorer)** — Implement the four-zone Grid layout from Section 2 with GridSplitters.

### Phase 2 — Core Interactions
4. **Collapsible panels** — Implement collapse/expand for Zone 1 and Zone 3 with rail states and keyboard shortcuts.
5. **Tabbed workspace** — Replace the single search results area with a TabControl supporting multiple independent search tabs.
6. **Status bar** — Implement Zone 4 with connection indicator, OU path, object count, and timestamp.

### Phase 3 — Search & Filtering
7. **Advanced search bar** — Build the three-row search bar with attribute dropdown, text input, and object type filter pills.
8. **Relationship queries** — Implement "Search within this group" context menu and the group-scoped tab workflow.
9. **Search history & saved searches** — Add the history dropdown and save/load functionality.
10. **LDAP filter mode** — Add the Advanced toggle for raw LDAP filter input.

### Phase 4 — Detail Inspector Enhancements
11. **Sub-tabs (Attributes / Groups / Permissions)** — Implement the three-tab layout in Zone 3.
12. **Attribute formatting** — Human-readable timestamps, resolved SIDs, truncated long values with "Show more".
13. **Full-width overlay mode** — Implement the expand button and Escape-to-close behavior.

### Phase 5 — Polish
14. **Breadcrumb bar** — Implement clickable breadcrumb navigation in Zone 1.
15. **Tree search/filter** — Real-time filtering of the OU tree as the user types.
16. **Export functionality** — CSV, clipboard, and optional Excel export with column selection.
17. **Empty states & loading indicators** — Implement all empty state messages and loading skeletons.
18. **Keyboard shortcuts** — Wire up all shortcuts from Section 6.
19. **Tree icon consistency** — Replace current mixed iconography with the unified 14×14px icon set.

---

## 9. Open Questions & Future Considerations

- **Light theme:** This spec defines a dark theme only. A light theme toggle could be added later using the same `ResourceDictionary` pattern with a secondary dictionary that overrides the brush values.
- **Drag-and-drop:** Could enable dragging objects between groups or into script targets in the Execution view. Not in scope for this iteration.
- **Column pinning in DataGrid:** For wide result sets, pinning the Name column while scrolling horizontally could help. Consider for a future pass.
- **Multi-domain support:** Currently targets Powerex.ca. The architecture (status bar, connection indicator) is designed to support multiple domain connections in the future.

---

## Appendix A: Annotated Layout Wireframe

```
┌──────────────────────────────────────────────────────────────────────┐
│                        Title Bar                                     │
├────────────┬──┬──────────────────────────────┬──┬───────────────────┤
│ Zone 1     │⟷│ Zone 2                        │⟷│ Zone 3            │
│ OU Nav     │  │ Tabbed Workspace              │  │ Detail Inspector  │
│            │  │                                │  │                   │
│ [Search..] │  │ [Tab1] [Tab2] [+]             │  │ [Icon] jsmith     │
│ DC>OU>...  │  │                                │  │ User object       │
│            │  │ [▼ Attr] [Search...   ] [Go]  │  │                   │
│ ▶ Builtin  │  │ [Users][Computers][Groups]    │  │ [Attrs|Grps|Perms]│
│ ▶ Computers│  │ Scope: OU=POWEREX,...         │  │                   │
│ ▼ Domain.. │  │                                │  │ displayName       │
│   ▶ OU=... │  │ Name    Class   DisplayName   │  │ John Smith        │
│   ▶ OU=... │  │ ─────── ─────── ────────────  │  │                   │
│ ▶ Groups   │  │ jsmith  user    John Smith    │  │ whenCreated       │
│ ▶ Users    │  │ jdoe    user    Jane Doe      │  │ 2024-01-15 09:32  │
│            │  │ pc-001  comp    PC-001        │  │                   │
│            │  │                                │  │ lastLogon         │
│            │  │                                │  │ 2 hours ago       │
│            │  │                                │  │                   │
│ «          │  │ [Export ▼]  42 results         │  │           » | ⤢   │
├────────────┴──┴──────────────────────────────┴──┴───────────────────┤
│ ● Connected: Powerex.ca │ OU=POWEREX,DC=... │ 42 objects │ 14:32:01│
└──────────────────────────────────────────────────────────────────────┘
```

## Appendix B: WPF ResourceDictionary Brush Naming Convention

All brushes follow the pattern `{Category}{Tier}Brush`:

```xml
<!-- Backgrounds -->
<SolidColorBrush x:Key="BackgroundBaseBrush" Color="#1B1B1F" />
<SolidColorBrush x:Key="Surface1Brush" Color="#252529" />
<SolidColorBrush x:Key="Surface2Brush" Color="#2E2E34" />
<SolidColorBrush x:Key="Surface3Brush" Color="#38383F" />

<!-- Text -->
<SolidColorBrush x:Key="TextPrimaryBrush" Color="#EDEDEF" />
<SolidColorBrush x:Key="TextSecondaryBrush" Color="#A9A9B2" />
<SolidColorBrush x:Key="TextTertiaryBrush" Color="#6F6F7A" />
<SolidColorBrush x:Key="TextAccentBrush" Color="#6BA3E8" />

<!-- Accents -->
<SolidColorBrush x:Key="AccentBlueBrush" Color="#6BA3E8" />
<SolidColorBrush x:Key="AccentGreenBrush" Color="#4DAA7A" />
<SolidColorBrush x:Key="AccentAmberBrush" Color="#D4A73A" />
<SolidColorBrush x:Key="AccentRedBrush" Color="#D4585A" />

<!-- Borders -->
<SolidColorBrush x:Key="BorderSubtleBrush" Color="#2E2E34" />
<SolidColorBrush x:Key="BorderDefaultBrush" Color="#38383F" />
<SolidColorBrush x:Key="BorderFocusBrush" Color="#6BA3E8" />
```

The AI agent should define these brushes first, then reference them by key throughout all control templates and styles. **Never use hardcoded hex values in XAML outside of this dictionary.**