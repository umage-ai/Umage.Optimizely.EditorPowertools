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

## Data Persistence

- Use Optimizely's **DynamicDataStore** for tool-specific data (rules, analysis results).
- Use **in-memory caching** with TTL for expensive queries (content type counts, etc.).
- Never use a separate database.

## NuGet Packaging

- Ships as `CodeArt.Optimizely.EditorPowertools`.
- Razor class library (SDK: `Microsoft.NET.Sdk.Razor`) for embedded views and static files.
- Minimum dependency: `EPiServer.CMS` only.
- Registered as a protected module via `ProtectedModuleOptions`.
