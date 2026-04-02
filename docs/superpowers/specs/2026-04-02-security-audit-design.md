# Security Audit — Design Spec

## Overview

A comprehensive security analysis tool that gives editors and admins visibility into content access rights across the entire CMS. Three complementary views — Content Tree Permissions, Role/User Explorer, and Issues Dashboard — help identify who can access what, spot problems, and maintain a secure content structure.

Feature toggle: `SecurityAudit` (default: `true`).  
Permission: `EditorPowertoolsPermissions.SecurityAudit`.  
Menu group: Security.

---

## Architecture

### Pre-computation via IContentAnalyzer

Enumerating ACLs across all content at request time is too expensive for large sites. The Security Audit follows the same pattern as `ContentTypeStatisticsAnalyzer` and `PersonalizationAnalyzer`: a `SecurityAuditAnalyzer : IContentAnalyzer` that runs during the unified scheduled job, reads every content item's ACL from `IContentSecurityRepository`, and persists the results to DDS.

**Why an analyzer, not on-demand?**
- `IContentSecurityRepository.Get(contentRef)` is a per-item call — doing it for 75K+ items on every page load is not viable.
- The analyzer runs once per scheduled job execution (typically nightly), stores flattened ACL records, and the UI reads from DDS with efficient queries.
- The Issues Dashboard depends on cross-tree analysis (comparing parent/child permissions) that requires a full traversal.

### Data Flow

```
Unified Scheduled Job
  └─ SecurityAuditAnalyzer.Analyze(content, contentRef)
       ├─ Read ACL from IContentSecurityRepository
       ├─ Read parent ACL (cached from previous iteration)
       ├─ Detect issues (inheritance anomalies, "Everyone" access, etc.)
       └─ Save SecurityAuditRecord to DDS

UI (vanilla JS)
  └─ Fetches from SecurityAudit API endpoints
       └─ SecurityAuditService reads from DDS + in-memory cache
```

---

## Data Model

### DDS Record: `SecurityAuditRecord`

One record per content item (not per ACE). Stores the full ACL snapshot plus computed issue flags.

```csharp
[EPiServerDataStore(AutomaticallyRemapStore = true, StoreName = "EditorPowertools_SecurityAudit")]
public class SecurityAuditRecord : IDynamicData
{
    public Identity Id { get; set; }

    // Content identification
    public int ContentId { get; set; }
    public string ContentName { get; set; }
    public string? ContentTypeName { get; set; }
    public string? Breadcrumb { get; set; }
    public int ParentContentId { get; set; }
    public int TreeDepth { get; set; }           // depth from root, for tree rendering
    public bool IsPage { get; set; }             // vs block/media/folder

    // Serialized ACL — compact JSON array of entries
    // Format: [{"name":"Everyone","type":"Role","access":"Read"},...]
    public string AclEntriesJson { get; set; }

    // Inheritance
    public bool IsInheriting { get; set; }       // content inherits from parent
    public bool HasExplicitAcl { get; set; }     // content has its own ACL set

    // Pre-computed issue flags (set by analyzer during traversal)
    public bool HasNoRestrictions { get; set; }  // effectively open to everyone
    public bool EveryoneCanPublish { get; set; } // "Everyone" role has Publish or above
    public bool EveryoneCanEdit { get; set; }    // "Everyone" role has Edit or above
    public bool ChildMorePermissive { get; set; }// this node grants more than its parent
    public int IssueCount { get; set; }          // total issues on this node

    public DateTime LastUpdated { get; set; }
}
```

### DTO: `AclEntryDto`

Deserialized from `AclEntriesJson` for API responses.

```csharp
public class AclEntryDto
{
    public string Name { get; set; }             // role or username
    public string EntityType { get; set; }       // "Role", "User", "VisitorGroup"
    public string Access { get; set; }           // "Read", "Edit", "Publish", "FullAccess", etc.
}
```

### DTO: `ContentPermissionNodeDto`

Returned by the tree endpoints. Matches the `ept-tree` UI component pattern.

```csharp
public class ContentPermissionNodeDto
{
    public int ContentId { get; set; }
    public string Name { get; set; }
    public string? ContentTypeName { get; set; }
    public string? Breadcrumb { get; set; }
    public bool IsPage { get; set; }
    public bool HasChildren { get; set; }        // for lazy-load expand

    // Permissions summary
    public List<AclEntryDto> Entries { get; set; }
    public bool IsInheriting { get; set; }
    public bool HasExplicitAcl { get; set; }

    // Issues
    public bool HasNoRestrictions { get; set; }
    public bool EveryoneCanPublish { get; set; }
    public bool EveryoneCanEdit { get; set; }
    public bool ChildMorePermissive { get; set; }
    public int IssueCount { get; set; }
    public int SubtreeIssueCount { get; set; }   // aggregate for badge on collapsed nodes

    // Children (populated on expand)
    public List<ContentPermissionNodeDto>? Children { get; set; }
}
```

### DTO: `RoleAccessSummaryDto`

For the Role/User Explorer view.

```csharp
public class RoleAccessSummaryDto
{
    public string RoleOrUser { get; set; }
    public string EntityType { get; set; }       // "Role" or "User"

    // Content grouped by access level
    public int FullAccessCount { get; set; }
    public int PublishCount { get; set; }
    public int EditCount { get; set; }
    public int ReadOnlyCount { get; set; }
    public int TotalContentCount { get; set; }
}
```

### DTO: `SecurityIssueDto`

For the Issues Dashboard.

```csharp
public class SecurityIssueDto
{
    public string IssueType { get; set; }        // "EveryonePublish", "NoRestrictions", etc.
    public string Severity { get; set; }         // "Critical", "Warning", "Info"
    public string Description { get; set; }
    public int ContentId { get; set; }
    public string ContentName { get; set; }
    public string? Breadcrumb { get; set; }
    public string EditUrl { get; set; }
}
```

---

## SecurityAuditAnalyzer (IContentAnalyzer)

### Initialize()
- Clear old `SecurityAuditRecord` entries from DDS.
- Build an in-memory parent ACL cache: `Dictionary<int, HashSet<string>>` mapping content ID to the set of `"role:accessLevel"` strings. This is populated during traversal so child analysis can compare against its parent.

### Analyze(IContent content, ContentReference contentRef)
1. Call `_contentSecurityRepository.Get(contentRef)` to get the `IContentSecurityDescriptor`.
2. If null, record the node as having no restrictions.
3. Serialize ACL entries to the compact JSON format.
4. Determine `IsInheriting` from the descriptor's `IsInherited` property.
5. Compare against the parent's cached ACL to detect `ChildMorePermissive`:
   - A child is "more permissive" if it grants access to a role/user that the parent does not, or grants a higher access level to the same entity.
6. Check for `EveryoneCanPublish` / `EveryoneCanEdit` by examining entries where `Name == "Everyone"`.
7. Check `HasNoRestrictions`: no explicit ACL and inheriting from a parent that also has none, or ACL grants Read to "Everyone".
8. Save `SecurityAuditRecord` to DDS.
9. Cache this node's effective ACL for child comparison.

### Complete()
- Compute `SubtreeIssueCount` aggregates by walking stored records bottom-up (load all records, group by parent, accumulate).
- Save updated subtree counts back to DDS.

### Performance Considerations
- The analyzer adds one `IContentSecurityRepository.Get()` call per content item during the job. This is lightweight (it reads from the ACL cache in Optimizely, not the DB each time for most items).
- Parent ACL caching in a dictionary keeps memory bounded — only the current path's ACLs need to be retained if content is traversed depth-first. However, `GetDescendents` order is not guaranteed to be depth-first, so we cache all parent ACLs seen so far. For 100K items with ~200 bytes per cached entry, this is roughly 20 MB — acceptable.

---

## Service: SecurityAuditService

Reads from DDS and provides the query interface for the controller.

```csharp
public class SecurityAuditService
{
    private readonly SecurityAuditRepository _repository;
    private readonly IContentRepository _contentRepository;
    private readonly IContentSecurityRepository _contentSecurityRepository;
    private readonly ILogger<SecurityAuditService> _logger;

    // --- Content Tree View ---

    /// Returns top-level children of a node for lazy tree loading.
    public List<ContentPermissionNodeDto> GetChildren(int parentContentId);

    /// Returns the full ACL detail for a single content item (click-to-expand).
    public ContentPermissionNodeDto? GetNodeDetail(int contentId);

    // --- Role/User Explorer ---

    /// Lists all distinct roles and users found across all ACLs.
    public List<RoleAccessSummaryDto> GetAllRolesAndUsers();

    /// Returns all content accessible by a given role or user, grouped by access level.
    public RoleExplorerResultDto GetContentForRoleOrUser(
        string name, string entityType,
        string? accessLevelFilter = null,
        int page = 1, int pageSize = 50);

    // --- Issues Dashboard ---

    /// Returns all detected security issues, filterable by type and severity.
    public SecurityIssuesResultDto GetIssues(
        string? issueTypeFilter = null,
        string? severityFilter = null,
        int page = 1, int pageSize = 50);

    /// Returns summary counts for the issues dashboard header stats.
    public SecurityIssuesSummaryDto GetIssuesSummary();

    // --- Export ---

    /// Streams all permission data for CSV/Excel export.
    public IEnumerable<SecurityExportRow> ExportAll();

    // --- Job Status ---

    /// Returns when the analyzer last ran (from DDS timestamp).
    public DateTime? GetLastAnalysisTime();
}
```

### Repository: SecurityAuditRepository

Thin DDS wrapper following the same pattern as `ContentTypeStatisticsRepository` and `PersonalizationUsageRepository`.

```csharp
public class SecurityAuditRepository
{
    public void Clear();
    public void Save(SecurityAuditRecord record);
    public void SaveOrUpdate(SecurityAuditRecord record);
    public IEnumerable<SecurityAuditRecord> GetAll();
    public IEnumerable<SecurityAuditRecord> GetByParent(int parentContentId);
    public SecurityAuditRecord? GetByContentId(int contentId);
    public IEnumerable<SecurityAuditRecord> GetByRoleOrUser(string name);
    public IEnumerable<SecurityAuditRecord> GetWithIssues();
}
```

---

## API Endpoints

All under `[Route("editorpowertools/security-audit")]` with `[Authorize(Policy = "codeart:editorpowertools")]`.

### Content Tree View

| Method | Route | Description |
|--------|-------|-------------|
| `GET` | `api/tree/children?parentId={id}` | Get child nodes with permission summaries. `parentId=0` for root. Returns `List<ContentPermissionNodeDto>` without nested children (lazy). |
| `GET` | `api/tree/node/{contentId}` | Get full detail for a single node including all ACL entries. |
| `GET` | `api/tree/path/{contentId}` | Get the ancestor chain from root to this node (for "reveal in tree" navigation). Returns `List<int>` of content IDs to expand. |

### Role/User Explorer

| Method | Route | Description |
|--------|-------|-------------|
| `GET` | `api/roles` | List all roles and users with summary counts. Returns `List<RoleAccessSummaryDto>`. |
| `GET` | `api/roles/{name}/content?entityType={type}&access={level}&page={p}&pageSize={ps}` | Get content accessible by this role/user, optionally filtered by access level. Paginated. |

### Issues Dashboard

| Method | Route | Description |
|--------|-------|-------------|
| `GET` | `api/issues/summary` | Summary counts: total issues, by severity, by type. |
| `GET` | `api/issues?type={type}&severity={sev}&page={p}&pageSize={ps}` | Paginated list of issues. |

### Utility

| Method | Route | Description |
|--------|-------|-------------|
| `GET` | `api/status` | Last analysis timestamp, total content analyzed, total issues found. |
| `POST` | `api/export` | Export all permission data as CSV download. |

---

## CMS Doctor Checks

Three new `DoctorCheckBase` implementations, registered as `IDoctorCheck` via DI. These are `AnalyzerDoctorCheckBase` subclasses that read from the pre-computed DDS data (same pattern as existing analyzer-based doctor checks).

### 1. EveryonePublishRightsCheck

- **Group:** Security
- **Tags:** `["security", "permissions"]`
- **Logic:** Query `SecurityAuditRepository` for records where `EveryoneCanPublish == true`.
- **OK:** No content grants Publish to Everyone.
- **Warning:** N content items grant Publish or higher to the "Everyone" role. Lists the content names and breadcrumbs in details.

### 2. UnrestrictedContentCheck

- **Group:** Security
- **Tags:** `["security", "permissions"]`
- **Logic:** Query for records where `HasNoRestrictions == true` and `IsPage == true` (only pages, not blocks/media which are typically unrestricted).
- **OK:** All pages have access restrictions.
- **BadPractice:** N pages have no access restrictions (wide open). Potentially expected for public-facing pages, so this is a BadPractice rather than a Fault.

### 3. InconsistentInheritanceCheck

- **Group:** Security
- **Tags:** `["security", "permissions"]`
- **Logic:** Query for records where `ChildMorePermissive == true`.
- **OK:** No permission inheritance inconsistencies found.
- **Warning:** N content items are more permissive than their parent. This often indicates accidental ACL changes.

---

## UI Design

### Page Layout

Single page at menu route `Security Audit`, using `_PowertoolsLayout.cshtml`. Three tabs via `ept-tabs`:

1. **Content Tree** (default)
2. **Role/User Explorer**
3. **Issues** (with badge showing total issue count)

A stats row (`ept-stats`) at the top shows:
- Total content analyzed
- Unique roles/users
- Total issues
- Last analysis time

### Tab 1: Content Tree Permissions

**Structure:**
```
[ept-toolbar]
  [Search: filter tree by content name]
  [Dropdown: highlight by role (selects a role to emphasize in the tree)]
  [Toggle: show only nodes with issues]

[ept-tree] — lazy-loaded, expandable
  ▶ Start (root)                    [Everyone: Read] [Editors: Publish] [Admins: Full]
    ▶ Products                      [Everyone: Read] [Editors: Publish]        ⚠ 3 issues
      ▼ Secret Product Launch       [Everyone: Publish]                        🔴 CRITICAL
        [inline detail panel showing full ACL + issue description]
    ▶ News                          [inherits from parent]
    ...
```

**Tree node rendering:**
- Each node shows the content name on the left.
- Permission badges on the right: colored `ept-badge` per role/user showing the access level.
  - `ept-badge--default` for Read
  - `ept-badge--primary` for Edit
  - `ept-badge--warning` for Publish
  - `ept-badge--danger` for FullAccess / Administer
- Inheritance indicator: a small "inherited" label or chain-link icon if the node inherits.
- Issue indicators on the right edge:
  - Red dot for critical issues (Everyone with Publish)
  - Yellow dot for warnings (inconsistent inheritance)
  - Aggregate badge on collapsed parents: "3 issues in subtree"
- Clicking a node expands inline to show the full ACL table for that node.

**Lazy loading:**
- Initial load fetches root children via `GET api/tree/children?parentId=0`.
- Expanding a node fetches its children via `GET api/tree/children?parentId={id}`.
- The `HasChildren` flag on the DTO controls whether the expand arrow is shown.

**Role highlight mode:**
- When a role is selected from the toolbar dropdown, all tree nodes are annotated with that role's access level prominently. Nodes where the role has no access are dimmed.

### Tab 2: Role/User Explorer

**Structure:**
```
[ept-toolbar]
  [Dropdown: select role or user] (populated from GET api/roles)
  [Dropdown: filter by access level — All / FullAccess / Publish / Edit / Read]

[ept-stats]
  Full Access: 12 | Publish: 45 | Edit: 120 | Read Only: 340

[ept-table] — paginated
  Content Name | Breadcrumb | Content Type | Access Level | Inherited? | Edit Link
  ──────────────────────────────────────────────────────────────────────────────────
  Start Page   | Root       | StartPage    | Full Access  | Explicit   | [edit]
  About Us     | Root > ... | StandardPage | Publish      | Inherited  | [edit]
  ...
```

**Interaction:**
- Selecting a role/user loads the summary counts and the paginated content table.
- Access level filter narrows the table.
- Each row links to the content's edit mode URL and also has a "Show in tree" action that switches to Tab 1 and expands to that node (using `GET api/tree/path/{contentId}`).

### Tab 3: Issues Dashboard

**Structure:**
```
[ept-stats — severity summary]
  Critical: 3 | Warning: 12 | Info: 5

[ept-toolbar]
  [Dropdown: filter by issue type]
  [Dropdown: filter by severity]

[ept-table] — paginated, sortable
  Severity | Issue Type              | Content Name    | Breadcrumb        | Action
  ─────────────────────────────────────────────────────────────────────────────────────
  🔴 Crit  | Everyone can Publish    | Secret Launch   | Products > ...    | [Edit] [Show in tree]
  🟡 Warn  | Inconsistent inheritance| Partner Area    | About > Partners  | [Edit] [Show in tree]
  ℹ Info   | No restrictions         | Public News     | News > ...        | [Edit] [Show in tree]
  ...
```

**Issue types and severities:**

| Issue Type | Severity | Description |
|------------|----------|-------------|
| `EveryonePublish` | Critical | "Everyone" role has Publish or higher |
| `EveryoneEdit` | Critical | "Everyone" role has Edit or higher |
| `ChildMorePermissive` | Warning | Node grants broader access than its parent |
| `NoRestrictions` | Info | Page has no access restrictions set (may be intentional for public pages) |

**Actions:**
- "Edit" opens the content in CMS edit mode.
- "Show in tree" switches to Tab 1 and navigates/expands to that node.

### CSS

No new CSS classes needed beyond the existing design system. The tool uses:
- `ept-tabs` for the three views
- `ept-tree` for the content tree (already supports expand/collapse)
- `ept-table` for tabular views
- `ept-badge--{severity}` for permission level and issue severity indicators
- `ept-toolbar` with `ept-search` for filters
- `ept-stats` for summary counters
- `ept-dialog` for node detail popups (if needed)

One addition: a CSS modifier `ept-tree__item--dimmed` for nodes dimmed during role highlight mode, and `ept-tree__item--flagged` for nodes with issues (subtle left-border color).

### JavaScript

Single file: `wwwroot/js/security-audit.js` as an IIFE using the shared `EPT` utilities.

Key functions:
- `renderTree(parentEl, nodes)` — renders tree nodes with permission badges, wires expand/collapse.
- `loadChildren(parentId)` — fetches children via API, returns promise.
- `renderRoleExplorer(role, accessFilter, page)` — fetches and renders the role content table.
- `renderIssues(typeFilter, sevFilter, page)` — fetches and renders the issues table.
- `showInTree(contentId)` — fetches ancestor path, switches to tree tab, expands to node.
- Tab switching, toolbar filter wiring, preference persistence.

**Preferences persisted** (via `GET/POST /editorpowertools/api/preferences/SecurityAudit`):
- Active tab
- Selected role in explorer
- Access level filter
- Issue type/severity filters
- Tree expand state (top 2 levels only, not deep)

---

## File Structure

```
src/EditorPowertools/
├── Tools/SecurityAudit/
│   ├── SecurityAuditController.cs          # API controller
│   ├── SecurityAuditService.cs             # Business logic, reads from DDS
│   ├── SecurityAuditRepository.cs          # DDS CRUD
│   └── Models/
│       ├── SecurityAuditRecord.cs          # DDS entity
│       ├── AclEntryDto.cs
│       ├── ContentPermissionNodeDto.cs
│       ├── RoleAccessSummaryDto.cs
│       ├── RoleExplorerResultDto.cs
│       ├── SecurityIssueDto.cs
│       ├── SecurityIssuesSummaryDto.cs
│       └── SecurityExportRow.cs
├── Services/Analyzers/
│   └── SecurityAuditAnalyzer.cs            # IContentAnalyzer for unified job
├── Tools/CmsDoctor/Checks/
│   ├── EveryonePublishRightsCheck.cs       # IDoctorCheck
│   ├── UnrestrictedContentCheck.cs         # IDoctorCheck
│   └── InconsistentInheritanceCheck.cs     # IDoctorCheck
└── wwwroot/
    └── js/
        └── security-audit.js               # UI (vanilla JS IIFE)
```

Plus additions to existing files:
- `FeatureToggles.cs` — add `public bool SecurityAudit { get; set; } = true;`
- `EditorPowertoolsPermissions.cs` — add `SecurityAudit` permission type
- `EditorPowertoolsMenuProvider.cs` — add menu item
- `ServiceCollectionExtensions.cs` — register `SecurityAuditService`, `SecurityAuditRepository`, `SecurityAuditAnalyzer`, and the three doctor checks
- `Views/EditorPowertools/SecurityAudit.cshtml` — Razor page (layout + mount point)
- `Resources/Translations/security-audit.xml` — localized strings

---

## Registration

In `ServiceCollectionExtensions.cs`:

```csharp
services.AddTransient<SecurityAuditService>();
services.AddSingleton<SecurityAuditRepository>();
services.AddTransient<IContentAnalyzer, SecurityAuditAnalyzer>();
services.AddTransient<IDoctorCheck, EveryonePublishRightsCheck>();
services.AddTransient<IDoctorCheck, UnrestrictedContentCheck>();
services.AddTransient<IDoctorCheck, InconsistentInheritanceCheck>();
```

The `IContentAnalyzer` registration means the unified job automatically picks it up — no job changes needed.

The `IDoctorCheck` registration means CMS Doctor automatically includes the three security checks — no doctor changes needed.

---

## Edge Cases and Open Questions

1. **Blocks and media ACLs** — Blocks and media typically inherit ACLs from their asset folder, not from the pages they appear on. The tree view should still show them under their actual parent (asset folder), but the Issues Dashboard should treat "no restrictions on a block" as normal (Info at most, not Warning). Only pages with no restrictions should be flagged as noteworthy.

2. **Visitor groups in ACLs** — Visitor groups can appear as security entities. The tree view should display these alongside roles/users with a distinct badge style. The PersonalizationAnalyzer already detects VG usage in ACLs, so the Security Audit should complement (not duplicate) that data.

3. **Very large sites (100K+ items)** — The DDS query `GetByParent` must be indexed on `ParentContentId`. DDS does not support custom indexes, so if performance is poor, consider an in-memory cache of the full dataset (loaded once on first request, invalidated when the job runs). The full dataset at 100K items with ~500 bytes per record is ~50 MB — feasible for in-memory caching with TTL.

4. **Real-time vs. stale data** — The UI should clearly show when the data was last analyzed (prominent timestamp). Consider adding a "Refresh now" button that triggers the unified job (same pattern as Link Checker and Content Type Audit, which have job trigger buttons).

5. **Multi-site** — For multi-site setups, the tree view naturally handles this since each site has its own root under the CMS root. No special handling needed.

6. **Export** — The CSV export should include one row per content item with columns: Content ID, Name, Breadcrumb, Content Type, Is Page, ACL Entries (semicolon-separated), Is Inheriting, Issues (semicolon-separated). This matches the pattern used by Content Audit export.

---

## Implementation Order

1. **Phase 1: Data layer** — `SecurityAuditRecord`, `SecurityAuditRepository`, `SecurityAuditAnalyzer`. Run the job and verify data is captured correctly.
2. **Phase 2: Service + API** — `SecurityAuditService`, `SecurityAuditController` with all endpoints. Test via HTTP.
3. **Phase 3: UI — Issues Dashboard** — Highest immediate value, simplest UI (just a table). Get the Razor page, JS, and tab structure working with this tab first.
4. **Phase 4: UI — Content Tree** — Tree rendering with lazy loading, permission badges, issue indicators.
5. **Phase 5: UI — Role Explorer** — Dropdown, table, "show in tree" cross-navigation.
6. **Phase 6: CMS Doctor checks** — Three check classes, verify they appear in CMS Doctor.
7. **Phase 7: Polish** — Preferences persistence, export, localization, role highlight mode.
