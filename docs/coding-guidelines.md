# Coding Guidelines

How each tool in EditorPowertools should be structured and implemented.

## General Principles

1. **Self-contained** - Each tool should be independently usable. Editors/admins opt in via feature toggles.
2. **Non-invasive** - Never alter existing content, schema, or behavior unless explicitly triggered by the user.
3. **Performant** - Tools that scan content must handle large sites (100k+ pages). Use paging, async, and cancellation tokens.
4. **Secure** - Respect Optimizely's access rights. Use the three-layer permission model (see below).

## Project Structure

```
src/EditorPowertools/
├── Configuration/           # Options, feature toggles
│   ├── EditorPowertoolsOptions.cs
│   └── FeatureToggles.cs
├── Infrastructure/          # DI registration, middleware
│   ├── ServiceCollectionExtensions.cs
│   └── ApplicationBuilderExtensions.cs
├── Menu/                    # CMS menu provider
│   └── EditorPowertoolsMenuProvider.cs
├── Permissions/             # Permission types and access checker
│   ├── EditorPowertoolsPermissions.cs
│   └── FeatureAccessChecker.cs
├── Tools/
│   ├── ContentTypeAudit/    # One folder per tool
│   │   ├── ContentTypeAuditController.cs
│   │   ├── ContentTypeAuditService.cs
│   │   └── ContentTypeAuditViewModel.cs
│   └── ...
└── wwwroot/                 # Static assets (CSS, JS)
```

Each tool gets its own folder under `Tools/`. Keep controllers, services, and view models together.

## Registration Pattern

The plugin is registered in the consuming site's `Startup.cs`:

```csharp
// In ConfigureServices:
services.AddEditorPowertools(options =>
{
    options.CheckPermissionForEachFeature = true;
    options.Features.ContentTypeAudit = true;
    // ...
});

// In Configure:
app.UseEditorPowertools();
```

Options can also be set via `appsettings.json`:

```json
{
  "CodeArt": {
    "EditorPowertools": {
      "checkPermissionForEachFeature": false,
      "features": {
        "contentTypeAudit": true,
        "personalizationUsageAudit": true
      }
    }
  }
}
```

## Three-Layer Permission Model

Each tool is protected by three independent checks:

1. **Feature Toggle** - Is the tool enabled at all? (`FeatureToggles` in options)
2. **Authorization Policy** - Is the user in an authorized role? (default: WebAdmins/CmsAdmins/Administrators)
3. **EPiServer Permission** - Optional per-user check via `PermissionTypes` (when `CheckPermissionForEachFeature = true`)

**Every tool must be toggleable.** When adding a new tool, always:
1. Add a `bool` property to `FeatureToggles` (default `true`)
2. Add a `PermissionType` to `EditorPowertoolsPermissions`
3. Check both in the controller and in the menu provider's `IsAvailable`

Use `FeatureAccessChecker` to perform these checks. Menu items use it for `IsAvailable`. Controllers use `[Authorize(Policy = "codeart:editorpowertools")]` plus service-level checks.

## Tool Anatomy

### 1. Service Layer
- All business logic in a service class, registered via DI
- Constructor-inject Optimizely services (`IContentRepository`, `IContentLoader`, etc.)
- Use `async`/`await` throughout
- Accept `CancellationToken` on long-running operations

```csharp
public class ContentTypeAuditService
{
    private readonly IContentTypeRepository _contentTypeRepository;
    private readonly IContentModelUsage _contentModelUsage;

    public ContentTypeAuditService(
        IContentTypeRepository contentTypeRepository,
        IContentModelUsage contentModelUsage)
    {
        _contentTypeRepository = contentTypeRepository;
        _contentModelUsage = contentModelUsage;
    }

    public async Task<IEnumerable<ContentTypeAuditResult>> GetAuditAsync(
        CancellationToken cancellationToken = default)
    {
        // ...
    }
}
```

### 2. Controller
- ASP.NET Core controller
- Route under `/editorpowertools/{tool-name}/`
- Authorize with the shared policy
- Check feature toggle + permission in action methods
- Keep thin - delegate to service
- Return JSON for API endpoints consumed by JS/React UI

```csharp
[Authorize(Policy = "codeart:editorpowertools")]
[Route("editorpowertools/content-type-audit")]
public class ContentTypeAuditController : Controller
{
    private readonly ContentTypeAuditService _service;
    private readonly FeatureAccessChecker _accessChecker;

    [HttpGet("api/types")]
    public async Task<IActionResult> GetTypes(CancellationToken ct)
    {
        if (!_accessChecker.HasAccess(User, "ContentTypeAudit",
            EditorPowertoolsPermissions.ContentTypeAudit))
            return Forbid();

        var results = await _service.GetAuditAsync(ct);
        return Ok(results);
    }
}
```

### 3. UI (Vanilla JS or React)

**Not Blazor.** The old project used Blazor/MudBlazor. The new project uses either:

- **Vanilla JS** - For simpler tools. Lightweight, no build step needed.
- **React components** - For complex interactive tools. Follow the Optimizely pattern: https://docs.developers.optimizely.com/content-management-system/docs/creating-a-react-component

UI files go in `wwwroot/` and are served as static files from the Razor class library.

### 4. Shared UI Design System

All tools MUST use the shared design system defined in `wwwroot/css/editorpowertools.css` and `wwwroot/js/editorpowertools.js`. This ensures visual consistency across all tools.

**Layout:** Every tool page uses the `_PowertoolsLayout.cshtml` shared layout which provides the header, navigation, and shell structure.

**CSS classes (prefix: `ept-`):**

| Component | Classes | Purpose |
|-----------|---------|---------|
| Page header | `ept-page-header` | Tool title + description |
| Stats row | `ept-stats`, `ept-stat` | Summary counters at top |
| Card | `ept-card`, `ept-card__header`, `ept-card__body` | Content containers |
| Toolbar | `ept-toolbar`, `ept-toolbar__spacer` | Filter/action bar |
| Search | `ept-search` | Search input with icon |
| Table | `ept-table` | Sortable data table with sticky headers |
| Dialog | `ept-dialog-backdrop`, `ept-dialog` | Modal popup for drill-down |
| Badge | `ept-badge--{default,primary,success,warning,danger}` | Status indicators |
| Button | `ept-btn`, `ept-btn--primary`, `ept-btn--sm` | Actions |
| Tree | `ept-tree`, `ept-tree__item` | Hierarchical tree view |
| Tabs | `ept-tabs`, `ept-tab` | Tab navigation |
| Tool card | `ept-tool-card` | Overview page tool links |

**JS utilities (`EPT` global object):**

| Method | Purpose |
|--------|---------|
| `EPT.fetchJson(url)` | Fetch JSON with error handling |
| `EPT.showLoading(el)` | Render spinner in element |
| `EPT.showEmpty(el, msg)` | Render empty state |
| `EPT.openDialog(title, opts)` | Open modal dialog, returns `{body, close}` |
| `EPT.createTable(columns, data, opts)` | Sortable table with column config |
| `EPT.downloadCsv(filename, columns, data)` | Export data as CSV download |
| `EPT.icons.*` | SVG icon strings (search, edit, link, list, etc.) |

**Row variants:** Use `ept-row--orphaned` (red tint), `ept-row--inherited` (green tint), `ept-row--system` (muted text) for table row styling.

**Color variables:** All colors use CSS custom properties (`--ept-*`) for future theming support.

**Tool-specific JS:** Each tool creates its own JS file (e.g. `content-type-audit.js`) as an IIFE that uses the shared `EPT` utilities.

### 5. Reusable Components

Shared components live in `Components/` (backend) and `wwwroot/js/components.js` (frontend). They are loaded globally on all tool pages via the shared layout.

**Content Picker** (`EPT.contentPicker(opts)`):
- Tree-based content browser with lazy-loading children
- Search by name with debounced input
- Returns a Promise resolving to the selected item `{id, name, typeName}` or `null`
- Options: `rootId` (default: RootPage), `title`

```javascript
const selected = await EPT.contentPicker({ title: 'Pick parent page' });
if (selected) console.log('Selected:', selected.id, selected.name);
```

**Content Type Picker** (`EPT.contentTypePicker(opts)`):
- Filterable list of all content types, grouped by GroupName
- Search/filter by display name or technical name
- Returns a Promise resolving to the selected type `{id, name, displayName, groupName, base}` or `null`
- Options: `title`, `includeSystem` (default: false)

```javascript
const type = await EPT.contentTypePicker({ title: 'Select target type' });
if (type) console.log('Type:', type.displayName);
```

**Backend API** (`ComponentsApiController`):
- `GET editorpowertools/api/components/content/{id}` - Get content node
- `GET editorpowertools/api/components/content/{id}/children` - Get children (lazy tree)
- `GET editorpowertools/api/components/content/search?q=...` - Search content by name
- `GET editorpowertools/api/components/content-types?q=...` - List/search content types

When building new tools, always prefer using these shared components over building custom pickers.

### 4. Scheduled Jobs (where needed)
- Extend `ScheduledJobBase`
- Use `[ScheduledPlugIn]` attribute
- Support cancellation via `_stopSignaled`
- Report progress via `OnStatusChanged`

```csharp
[ScheduledPlugIn(DisplayName = "[EditorPowertools] Analyze Personalization",
    Description = "Scans content for personalization usage")]
public class AnalyzePersonalizationJob : ScheduledJobBase
{
    // ...
}
```

## Coding Standards

- **Nullable reference types**: Enabled. No `null` without `?`.
- **Naming**: PascalCase for public members, `_camelCase` for private fields.
- **No static state**: Everything through DI.
- **Logging**: Inject `ILogger<T>`, use structured logging.
- **Error handling**: Let exceptions propagate unless you can handle them meaningfully.

## Localization

All user-facing text MUST be localized using Optimizely's built-in translation system. Never hardcode UI strings.

**Language XML files** go in `Resources/Translations/` within the plugin project:

```xml
<?xml version="1.0" encoding="utf-8"?>
<languages>
  <language name="en" id="en">
    <editorpowertools>
      <contenttypeaudit>
        <title>Content Type Audit</title>
        <description>Overview of all content types, their usage, properties, and inheritance.</description>
        <columns>
          <name>Name</name>
          <base>Base</base>
          <group>Group</group>
          <properties>Properties</properties>
          <contentcount>Content</contentcount>
        </columns>
      </contenttypeaudit>
    </editorpowertools>
  </language>
</languages>
```

**In Razor views**, use `@Html.Translate("/editorpowertools/contenttypeaudit/title")` or inject `ILocalizedStringProvider`.

**In controllers/services**, inject `LocalizationService` and call `localizationService.GetString("/editorpowertools/contenttypeaudit/title")`.

**In JavaScript**, pass translated strings from the Razor view to JS via a data attribute or inline script block:

```html
<div id="audit-content" data-translations='@Html.Raw(Json.Serialize(new {
    title = Html.Translate("/editorpowertools/contenttypeaudit/title"),
    search = Html.Translate("/editorpowertools/contenttypeaudit/search")
}))'></div>
```

This makes it straightforward to add new UI languages by adding additional `<language>` blocks to the XML files.

## Data Persistence

- Use Optimizely's **DynamicDataStore (DDS)** for aggregated/pre-computed data (content type statistics, personalization usage, etc.).
- A **single shared scheduled job** (`ContentTypeStatisticsJob`) traverses all content and collects statistics for all tools in one pass. Extend it when adding new tools that need aggregated data.
- Use **in-memory caching** with TTL for frequently accessed but cheap-to-compute data.
- Never use a separate database.

## NuGet Packaging

- Ships as `CodeArt.Optimizely.EditorPowertools`.
- Razor class library (SDK: `Microsoft.NET.Sdk.Razor`) for embedded views and static files.
- Minimum dependency: `EPiServer.CMS` only.
- Registered as a protected module via `ProtectedModuleOptions`.
