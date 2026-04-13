# Module-Path-Aware Routing Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace all hardcoded `/editorpowertools/api/...` routes with module-path-aware conventional routing so the add-on works regardless of how Optimizely resolves its virtual path.

**Architecture:** Register a single conventional route at the Optimizely module path in `MapEditorPowertools`. Remove all hardcoded `[Route("editorpowertools/api/...")]` attributes from every controller. JS switches from `EPT_API_URL + '/tool'` to `EPT_BASE_URL + 'ControllerName/ActionName'`. `EPT_API_URL` is removed; `EPT_HUB_URL` becomes module-path-aware.

**Tech Stack:** ASP.NET Core MVC conventional routing, Optimizely `Paths.ToResource()`, vanilla JS `fetch`.

**Spec:** `docs/superpowers/specs/2026-04-10-module-path-routing-design.md`

---

## Routing conventions used in this plan

Conventional route registered (Task 1):
```
{EPT_BASE_URL}{controller}/{action}/{id?}
```

Every API controller action therefore responds at:
```
{EPT_BASE_URL}ControllerClassName-minus-"Controller"/{MethodName}[/{id}]
```

Rules applied to ALL controllers:
- Remove class-level `[Route("...")]` entirely
- Remove explicit path strings from action attributes — `[HttpGet("sub")]` → `[HttpGet]`
- Keep `[HttpGet]`, `[HttpPost]`, `[HttpDelete]`, `[HttpPut]` as method constraints only
- Single route-param actions: rename parameter to `id` (or use `[FromRoute(Name="id")]`)
- Multi-param and named-string params: change to `[FromQuery]`
- `[ApiController]` is NOT used anywhere; these are plain MVC controllers

---

## Task 1 — Register conventional route and fix SignalR hub path

**Files:**
- Modify: `src/EditorPowertools/Infrastructure/ApplicationBuilderExtensions.cs`

- [ ] **Step 1: Update ApplicationBuilderExtensions**

Replace the entire file with:

```csharp
using EPiServer.Shell;
using UmageAI.Optimizely.EditorPowerTools.Menu;
using UmageAI.Optimizely.EditorPowerTools.Tools.ActiveEditors;
using UmageAI.Optimizely.EditorPowerTools.Tools.VisitorGroupTester;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace UmageAI.Optimizely.EditorPowerTools.Infrastructure;

public static class ApplicationBuilderExtensions
{
    public static IApplicationBuilder UseEditorPowertools(this IApplicationBuilder app)
    {
        app.UseMiddleware<VisitorGroupTesterMiddleware>();
        return app;
    }

    public static IEndpointRouteBuilder MapEditorPowertools(this IEndpointRouteBuilder endpoints)
    {
        // Register conventional route at the module's virtual path so all API controllers
        // are accessible at {modulePath}/{controller}/{action}/{id?} without hardcoded prefixes.
        var basePath = Paths.ToResource(typeof(EditorPowertoolsMenuProvider), "")
            .TrimStart('/').TrimEnd('/');
        endpoints.MapControllerRoute(
            name: "EditorPowertoolsDefault",
            pattern: $"{basePath}/{{controller}}/{{action}}/{{id?}}");

        // Hub path is now module-path-aware
        endpoints.MapHub<ActiveEditorsHub>(
            Paths.ToResource(typeof(EditorPowertoolsMenuProvider), "hubs/active-editors"));

        return endpoints;
    }
}
```

- [ ] **Step 2: Build to verify compilation**

```
dotnet build src/EditorPowertools/EditorPowertools.csproj
```

Expected: 0 errors

- [ ] **Step 3: Commit**

```bash
git add src/EditorPowertools/Infrastructure/ApplicationBuilderExtensions.cs
git commit -m "feat(routing): register module-path conventional route and fix SignalR hub path"
```

---

## Task 2 — Fix layout: remove EPT_API_URL, make EPT_HUB_URL dynamic

**Files:**
- Modify: `src/EditorPowertools/Views/Shared/_PowertoolsLayout.cshtml`

- [ ] **Step 1: Replace the hardcoded JS globals block**

In `_PowertoolsLayout.cshtml`, find and replace lines 57–58:

```js
        window.EPT_API_URL = '/editorpowertools/api';
        window.EPT_HUB_URL = '/editorpowertools/hubs';
```

Replace with:

```js
        window.EPT_HUB_URL = '@Html.Raw(Paths.ToResource(typeof(UmageAI.Optimizely.EditorPowerTools.Menu.EditorPowertoolsMenuProvider), "hubs"))';
```

(`EPT_API_URL` is removed entirely; all JS will use `EPT_BASE_URL` directly.)

- [ ] **Step 2: Build and verify**

```
dotnet build src/EditorPowertools/EditorPowertools.csproj
```

Expected: 0 errors

- [ ] **Step 3: Commit**

```bash
git add src/EditorPowertools/Views/Shared/_PowertoolsLayout.cshtml
git commit -m "feat(routing): remove hardcoded EPT_API_URL; compute EPT_HUB_URL via Paths.ToResource"
```

---

## Task 3 — Fix ActivityTimelineApiController

**Files:**
- Modify: `src/EditorPowertools/Tools/ActivityTimeline/ActivityTimelineApiController.cs`

**Old → New URL mapping:**
- `activity/timeline` → `ActivityTimelineApi/GetTimeline`
- `activity/stats` → `ActivityTimelineApi/GetStats`
- `activity/compare/{contentId}/{versionId}` → `ActivityTimelineApi/CompareVersions?contentId=&versionId=`
- `activity/users` → `ActivityTimelineApi/GetUsers`
- `activity/content-types` → `ActivityTimelineApi/GetContentTypes`

- [ ] **Step 1: Remove all [Route] attributes from actions**

In `ActivityTimelineApiController.cs`, remove the four `[Route("editorpowertools/api/activity/...")]` lines (lines 27, 60, 73, 86, 99). Keep `[HttpGet]` on each action unchanged.

For `CompareVersions`, the two route params become query params — change the signature from:
```csharp
[Route("editorpowertools/api/activity/compare/{contentId}/{versionId}")]
public IActionResult CompareVersions(int contentId, int versionId, [FromQuery] string? language = null)
```
to:
```csharp
[HttpGet]
public IActionResult CompareVersions([FromQuery] int contentId, [FromQuery] int versionId, [FromQuery] string? language = null)
```

Full updated file:

```csharp
using UmageAI.Optimizely.EditorPowerTools.Permissions;
using UmageAI.Optimizely.EditorPowerTools.Tools.ActivityTimeline.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace UmageAI.Optimizely.EditorPowerTools.Tools.ActivityTimeline;

/// <summary>
/// API controller for Activity Timeline data endpoints.
/// The page view is served by EditorPowertoolsController.ActivityTimeline().
/// </summary>
[Authorize(Policy = "codeart:editorpowertools")]
public class ActivityTimelineApiController : Controller
{
    private readonly ActivityTimelineService _service;
    private readonly FeatureAccessChecker _accessChecker;

    public ActivityTimelineApiController(
        ActivityTimelineService service,
        FeatureAccessChecker accessChecker)
    {
        _service = service;
        _accessChecker = accessChecker;
    }

    [HttpGet]
    public IActionResult GetTimeline(
        [FromQuery] int skip = 0,
        [FromQuery] int take = 50,
        [FromQuery] string? user = null,
        [FromQuery] string? action = null,
        [FromQuery] string? contentType = null,
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        [FromQuery] int? contentId = null)
    {
        if (!_accessChecker.HasAccess(HttpContext,
            nameof(Configuration.FeatureToggles.ActivityTimeline),
            EditorPowertoolsPermissions.ActivityTimeline))
            return Forbid();

        var request = new ActivityFilterRequest
        {
            Skip = skip,
            Take = take,
            User = user,
            Action = action,
            ContentTypeName = contentType,
            FromUtc = from,
            ToUtc = to,
            ContentId = contentId
        };

        var result = _service.GetActivities(request);
        return Ok(result);
    }

    [HttpGet]
    public IActionResult GetStats()
    {
        if (!_accessChecker.HasAccess(HttpContext,
            nameof(Configuration.FeatureToggles.ActivityTimeline),
            EditorPowertoolsPermissions.ActivityTimeline))
            return Forbid();

        var stats = _service.GetStats();
        return Ok(stats);
    }

    [HttpGet]
    public IActionResult CompareVersions(
        [FromQuery] int contentId,
        [FromQuery] int versionId,
        [FromQuery] string? language = null)
    {
        if (!_accessChecker.HasAccess(HttpContext,
            nameof(Configuration.FeatureToggles.ActivityTimeline),
            EditorPowertoolsPermissions.ActivityTimeline))
            return Forbid();

        var result = _service.CompareVersions(contentId, versionId, language);
        return Ok(result);
    }

    [HttpGet]
    public IActionResult GetUsers()
    {
        if (!_accessChecker.HasAccess(HttpContext,
            nameof(Configuration.FeatureToggles.ActivityTimeline),
            EditorPowertoolsPermissions.ActivityTimeline))
            return Forbid();

        var users = _service.GetDistinctUsers();
        return Ok(users);
    }

    [HttpGet]
    public IActionResult GetContentTypes()
    {
        if (!_accessChecker.HasAccess(HttpContext,
            nameof(Configuration.FeatureToggles.ActivityTimeline),
            EditorPowertoolsPermissions.ActivityTimeline))
            return Forbid();

        var types = _service.GetDistinctContentTypes();
        return Ok(types);
    }
}
```

- [ ] **Step 2: Build**

```
dotnet build src/EditorPowertools/EditorPowertools.csproj
```

Expected: 0 errors

- [ ] **Step 3: Commit**

```bash
git add src/EditorPowertools/Tools/ActivityTimeline/ActivityTimelineApiController.cs
git commit -m "feat(routing): remove hardcoded routes from ActivityTimelineApiController"
```

---

## Task 4 — Fix remaining Tool controllers (group A)

Fix these four controllers using the same pattern: remove class-level `[Route]`, remove path strings from action attributes. The files and exact changes are listed below.

**Files:**
- Modify: `src/EditorPowertools/Tools/AudienceManager/AudienceManagerApiController.cs`
- Modify: `src/EditorPowertools/Tools/BulkPropertyEditor/BulkPropertyEditorApiController.cs`
- Modify: `src/EditorPowertools/Tools/CmsDoctor/CmsDoctorApiController.cs`
- Modify: `src/EditorPowertools/Tools/ContentDetails/ContentDetailsApiController.cs`

**AudienceManagerApiController changes:**
- Remove class-level `[Route("editorpowertools/api/audience")]` (or per-action routes)
- Remove route strings from all action `[HttpGet("...")]` → `[HttpGet]`
- `GetCriteria(int id)` and `GetUsages(int id)`: if param currently named `visitorGroupId` or similar, rename to `id`
- New URLs: `AudienceManagerApi/GetVisitorGroups`, `AudienceManagerApi/GetCriteria/{id}`, `AudienceManagerApi/GetUsages/{id}`

**BulkPropertyEditorApiController changes:**
- Remove class-level `[Route("editorpowertools/api/bulk-editor")]`
- Remove path strings: `"content-types"` → none, `"languages"` → none, etc.
- `GetProperties(int contentTypeId)`: rename param to `id`
- `GetReferences(int contentId)`: rename param to `id`  
- `Publish(int contentId)`: rename param to `id`
- New URLs: `BulkPropertyEditorApi/GetContentTypes`, `BulkPropertyEditorApi/GetProperties/{id}`, `BulkPropertyEditorApi/Save`, etc.

**CmsDoctorApiController changes:**
- Remove class-level `[Route("editorpowertools/api/cms-doctor")]`
- `RunCheck(string checkType)`, `FixCheck(string checkType)`, `DismissCheck(string checkType)`, `RestoreCheck(string checkType)`: change route param to `[FromQuery] string id` (or rename checkType to `id`)
- New URLs: `CmsDoctorApi/GetDashboard`, `CmsDoctorApi/RunAll`, `CmsDoctorApi/RunCheck?id=memorycheck`, etc.

**ContentDetailsApiController changes:**
- Remove per-action `[Route("editorpowertools/api/content-details/{contentId:int}")]`
- Param `int contentId` → rename to `int id`
- `[HttpGet("{contentId:int}")]` → `[HttpGet]` (id bound via `{id?}` convention)
- New URL: `ContentDetailsApi/GetDetails/{id}`

- [ ] **Step 1: Update AudienceManagerApiController**

Read `src/EditorPowertools/Tools/AudienceManager/AudienceManagerApiController.cs`, then apply: remove class-level (or per-action) `[Route("editorpowertools/api/...")]`, remove path strings from `[HttpGet("...")]` → `[HttpGet]`, rename any non-`id` route params to `id`.

- [ ] **Step 2: Update BulkPropertyEditorApiController**

Read `src/EditorPowertools/Tools/BulkPropertyEditor/BulkPropertyEditorApiController.cs`, apply: remove `[Route("editorpowertools/api/bulk-editor")]` class attribute, remove sub-path strings from all action attributes, rename `contentTypeId`/`contentId` route params to `id`.

- [ ] **Step 3: Update CmsDoctorApiController**

Read `src/EditorPowertools/Tools/CmsDoctor/CmsDoctorApiController.cs`, apply: remove `[Route("editorpowertools/api/cms-doctor")]` class attribute, remove sub-path strings, change `RunCheck`/`FixCheck`/`DismissCheck`/`RestoreCheck` route string params to `[FromQuery] string id`.

- [ ] **Step 4: Update ContentDetailsApiController**

Read `src/EditorPowertools/Tools/ContentDetails/ContentDetailsApiController.cs`, apply: remove per-action `[Route("editorpowertools/api/content-details/{contentId:int}")]`, change `[HttpGet("{contentId:int}")]` to `[HttpGet]`, rename param to `id`.

- [ ] **Step 5: Build**

```
dotnet build src/EditorPowertools/EditorPowertools.csproj
```

Expected: 0 errors

- [ ] **Step 6: Commit**

```bash
git add src/EditorPowertools/Tools/AudienceManager/AudienceManagerApiController.cs \
        src/EditorPowertools/Tools/BulkPropertyEditor/BulkPropertyEditorApiController.cs \
        src/EditorPowertools/Tools/CmsDoctor/CmsDoctorApiController.cs \
        src/EditorPowertools/Tools/ContentDetails/ContentDetailsApiController.cs
git commit -m "feat(routing): remove hardcoded routes from Audience, BulkEditor, CmsDoctor, ContentDetails controllers"
```

---

## Task 5 — Fix ContentAuditApiController

**Files:**
- Modify: `src/EditorPowertools/Tools/ContentAudit/ContentAuditController.cs`

**Old → New:**
- `GET content-audit` (class route + `[HttpGet]`) → `ContentAuditApi/GetContent`
- `GET content-audit/export` → `ContentAuditApi/Export`
- `POST content-audit/export-request` → `ContentAuditApi/RequestExport`
- `GET content-audit/export-status` → `ContentAuditApi/GetExportStatus`

- [ ] **Step 1: Update ContentAuditController.cs**

Remove line 19: `[Route("editorpowertools/api/content-audit")]`

Change:
```csharp
[HttpGet("export")]
public IActionResult Export(...)
```
to:
```csharp
[HttpGet]
public IActionResult Export(...)
```

Change:
```csharp
[HttpPost("export-request")]
[RequireAjax]
public async Task<IActionResult> RequestExport(...)
```
to:
```csharp
[HttpPost]
[RequireAjax]
public async Task<IActionResult> RequestExport(...)
```

Change:
```csharp
[HttpGet("export-status")]
public IActionResult GetExportStatus(...)
```
to:
```csharp
[HttpGet]
public IActionResult GetExportStatus(...)
```

The `[HttpGet]` on `GetContent` at line 48 is already correct — no path string.

- [ ] **Step 2: Build**

```
dotnet build src/EditorPowertools/EditorPowertools.csproj
```

Expected: 0 errors

- [ ] **Step 3: Commit**

```bash
git add src/EditorPowertools/Tools/ContentAudit/ContentAuditController.cs
git commit -m "feat(routing): remove hardcoded routes from ContentAuditApiController"
```

---

## Task 6 — Fix Tool controllers (group B)

**Files:**
- Modify: `src/EditorPowertools/Tools/ContentImporter/ContentImporterApiController.cs`
- Modify: `src/EditorPowertools/Tools/ContentStatistics/ContentStatisticsApiController.cs`
- Modify: `src/EditorPowertools/Tools/ContentTypeAudit/ContentTypeAuditController.cs`
- Modify: `src/EditorPowertools/Tools/ContentTypeRecommendations/ContentTypeRecommendationsController.cs`

**ContentImporterApiController changes:**
- Remove per-action `[Route("editorpowertools/api/content-importer/...")]` from every action
- `GetContentType(int id)`: remove `[HttpGet("content-types/{id:int}")]` → `[HttpGet]`, rename param to `id`
- `GetProgress(Guid sessionId)`: remove `[HttpGet("progress/{sessionId:guid}")]` → `[HttpGet]`, rename param to `id`
- All other actions: remove path strings, keep `[HttpGet]`/`[HttpPost]`

**ContentStatisticsApiController changes:**
- Remove per-action `[Route("editorpowertools/api/content-statistics/...")]`
- Remove path strings from `[HttpGet("dashboard")]` → `[HttpGet]`, `[HttpPost("aggregation-start")]` → `[HttpPost]`

**ContentTypeAuditController changes:**
- Remove per-action `[Route("editorpowertools/api/content-types/...")]`
- `GetProperties(int id)`, `GetContentOfType(int id)`, `GetContentReferences(int id)`: ensure param named `id`
- `GetInheritanceTree`, `GetAggregationStatus`, `StartAggregationJob`: no route params, keep `[HttpGet]`/`[HttpPost]`

**ContentTypeRecommendationsController changes:**
- Remove per-action `[Route("editorpowertools/api/recommendations/...")]`
- `DeleteRule(Guid id)`: ensure param named `id`
- `GetRule(Guid id)` (if any): ensure param named `id`
- GET/POST/DELETE actions: remove path strings

- [ ] **Step 1: Update ContentImporterApiController** — read file, apply changes above

- [ ] **Step 2: Update ContentStatisticsApiController** — read file, apply changes above

- [ ] **Step 3: Update ContentTypeAuditController** — read file, apply changes above

- [ ] **Step 4: Update ContentTypeRecommendationsController** — read file, apply changes above

- [ ] **Step 5: Build**

```
dotnet build src/EditorPowertools/EditorPowertools.csproj
```

Expected: 0 errors

- [ ] **Step 6: Commit**

```bash
git add src/EditorPowertools/Tools/ContentImporter/ContentImporterApiController.cs \
        src/EditorPowertools/Tools/ContentStatistics/ContentStatisticsApiController.cs \
        src/EditorPowertools/Tools/ContentTypeAudit/ContentTypeAuditController.cs \
        src/EditorPowertools/Tools/ContentTypeRecommendations/ContentTypeRecommendationsController.cs
git commit -m "feat(routing): remove hardcoded routes from ContentImporter, ContentStatistics, ContentTypeAudit, ContentTypeRecommendations controllers"
```

---

## Task 7 — Fix Tool controllers (group C)

**Files:**
- Modify: `src/EditorPowertools/Tools/LanguageAudit/LanguageAuditApiController.cs`
- Modify: `src/EditorPowertools/Tools/LinkChecker/LinkCheckerApiController.cs`
- Modify: `src/EditorPowertools/Tools/ManageChildren/ManageChildrenApiController.cs`
- Modify: `src/EditorPowertools/Tools/PersonalizationAudit/PersonalizationAuditApiController.cs`
- Modify: `src/EditorPowertools/Tools/ScheduledJobsGantt/ScheduledJobsGanttApiController.cs`

**For all:** Remove per-action `[Route("editorpowertools/api/...")]` or class-level route, remove path strings from `[HttpGet("sub")]` → `[HttpGet]`.

**ManageChildrenApiController specifics:**
- `GetChildren`: has `[Route("editorpowertools/api/manage-children/{parentId:int}")]` — remove route, keep `[HttpGet]`, rename param to `id`
- `GetParent`: has `[Route("editorpowertools/api/manage-children/parent/{contentId:int}")]` — remove route, keep `[HttpGet]`, rename param to `id`
- Bulk operations (BulkDelete, BulkPublish, etc.): remove route path strings, keep `[HttpPost]`

- [ ] **Step 1: Update LanguageAuditApiController** — read file, remove all `[Route("editorpowertools/api/language-audit/...")]`, remove path strings from action attrs

- [ ] **Step 2: Update LinkCheckerApiController** — read file, remove all route attrs, remove path strings

- [ ] **Step 3: Update ManageChildrenApiController** — read file, remove routes, rename `parentId`/`contentId` to `id`

- [ ] **Step 4: Update PersonalizationAuditApiController** — read file, remove all route attrs, remove path strings

- [ ] **Step 5: Update ScheduledJobsGanttApiController** — read file, remove all route attrs, remove path strings

- [ ] **Step 6: Build**

```
dotnet build src/EditorPowertools/EditorPowertools.csproj
```

Expected: 0 errors

- [ ] **Step 7: Commit**

```bash
git add src/EditorPowertools/Tools/LanguageAudit/LanguageAuditApiController.cs \
        src/EditorPowertools/Tools/LinkChecker/LinkCheckerApiController.cs \
        src/EditorPowertools/Tools/ManageChildren/ManageChildrenApiController.cs \
        src/EditorPowertools/Tools/PersonalizationAudit/PersonalizationAuditApiController.cs \
        src/EditorPowertools/Tools/ScheduledJobsGantt/ScheduledJobsGanttApiController.cs
git commit -m "feat(routing): remove hardcoded routes from LanguageAudit, LinkChecker, ManageChildren, Personalization, ScheduledJobsGantt controllers"
```

---

## Task 8 — Fix SecurityAuditApiController and VisitorGroupTesterApiController

**Files:**
- Modify: `src/EditorPowertools/Tools/SecurityAudit/SecurityAuditApiController.cs`
- Modify: `src/EditorPowertools/Tools/VisitorGroupTester/VisitorGroupTesterApiController.cs`

**SecurityAuditApiController specifics:**
- Remove class-level `[Route("editorpowertools/api/security-audit")]`
- Remove sub-paths from all action attributes
- `GetNodeDetail(int contentId)`, `GetPathToContent(int contentId)`: rename param to `id`, remove `{contentId:int}` from `[HttpGet]`
- `GetContentForRole(string name)`: change `[HttpGet("roles/{name}/content")]` → `[HttpGet]`, change param to `[FromQuery] string id` (or keep `name` as `[FromQuery]`)
- New URLs: `SecurityAuditApi/GetChildren`, `SecurityAuditApi/GetNodeDetail/{id}`, `SecurityAuditApi/GetContentForRole?name=Editors`, etc.

**VisitorGroupTesterApiController specifics:**
- Remove per-action `[Route("editorpowertools/api/visitor-group-tester/groups")]`
- `GetGroups`: `[HttpGet]` only
- New URL: `VisitorGroupTesterApi/GetGroups`

- [ ] **Step 1: Update SecurityAuditApiController** — read file, apply changes above

- [ ] **Step 2: Update VisitorGroupTesterApiController** — read file, remove route attr

- [ ] **Step 3: Build**

```
dotnet build src/EditorPowertools/EditorPowertools.csproj
```

Expected: 0 errors

- [ ] **Step 4: Commit**

```bash
git add src/EditorPowertools/Tools/SecurityAudit/SecurityAuditApiController.cs \
        src/EditorPowertools/Tools/VisitorGroupTester/VisitorGroupTesterApiController.cs
git commit -m "feat(routing): remove hardcoded routes from SecurityAudit and VisitorGroupTester controllers"
```

---

## Task 9 — Fix Component controllers

**Files:**
- Modify: `src/EditorPowertools/Components/ComponentsApiController.cs`
- Modify: `src/EditorPowertools/Components/FeaturesApiController.cs`
- Modify: `src/EditorPowertools/Components/PreferencesApiController.cs`
- Modify: `src/EditorPowertools/Localization/UiStringsController.cs`

**ComponentsApiController specifics:**
- Remove class-level `[Route("editorpowertools/api/components")]`
- `GetContent(int id)`: remove `[HttpGet("content/{id}")]` → `[HttpGet]`, param already named `id` ✓
- `GetChildren(int id)`: remove `[HttpGet("content/{id}/children")]` → `[HttpGet]`, param already named `id` ✓
- `SearchContent`: remove `[HttpGet("content/search")]` → `[HttpGet]`, params already `[FromQuery]` ✓
- `GetContentTypes`: remove `[HttpGet("content-types")]` → `[HttpGet]`, param already `[FromQuery]` ✓
- New URLs: `ComponentsApi/GetContent/{id}`, `ComponentsApi/GetChildren/{id}`, `ComponentsApi/SearchContent?q=foo`, `ComponentsApi/GetContentTypes`

**FeaturesApiController specifics:**
- Remove class-level `[Route("editorpowertools/api")]`
- `GetFeatures`: remove `[HttpGet("features")]` → `[HttpGet]`
- New URL: `FeaturesApi/GetFeatures`

**PreferencesApiController specifics:**
- Remove class-level `[Route("editorpowertools/api/preferences")]`
- `Get(string toolName)`: remove `[HttpGet("{toolName}")]` → `[HttpGet]`, change `toolName` to `[FromQuery] string id`
- `Save(string toolName, ...)`: remove `[HttpPost("{toolName}")]` → `[HttpPost]`, change to `[FromQuery] string id`
- New URLs: `PreferencesApi/Get?id=content-audit`, `PreferencesApi/Save?id=content-audit`

**UiStringsController specifics:**
- Remove class-level `[Route("editorpowertools/api/ui-strings")]`
- Action `Get()`: ensure `[HttpGet]` only, no path string
- New URL: `UiStrings/Get`

- [ ] **Step 1: Update ComponentsApiController**

```csharp
using EPiServer;
using EPiServer.Core;
using EPiServer.DataAbstraction;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace UmageAI.Optimizely.EditorPowerTools.Components;

/// <summary>
/// API endpoints for reusable UI components (content picker, content type picker, etc.).
/// </summary>
[Authorize(Policy = "codeart:editorpowertools")]
public class ComponentsApiController : Controller
{
    private readonly IContentLoader _contentLoader;
    private readonly IContentRepository _contentRepository;
    private readonly IContentTypeRepository _contentTypeRepository;

    public ComponentsApiController(
        IContentLoader contentLoader,
        IContentRepository contentRepository,
        IContentTypeRepository contentTypeRepository)
    {
        _contentLoader = contentLoader;
        _contentRepository = contentRepository;
        _contentTypeRepository = contentTypeRepository;
    }

    [HttpGet]
    public IActionResult GetContent(int id)
    {
        var contentRef = id == 0 ? ContentReference.RootPage : new ContentReference(id);
        if (!_contentLoader.TryGet<IContent>(contentRef, out var content))
            return NotFound();

        return Ok(MapContent(content));
    }

    [HttpGet]
    public IActionResult GetChildren(int id)
    {
        var contentRef = id == 0 ? ContentReference.RootPage : new ContentReference(id);
        var children = _contentLoader.GetChildren<IContent>(
                contentRef,
                new LoaderOptions { LanguageLoaderOption.FallbackWithMaster() })
            .Select(MapContent)
            .ToList();

        return Ok(children);
    }

    [HttpGet]
    public IActionResult SearchContent([FromQuery] string q, [FromQuery] int rootId = 0)
    {
        if (string.IsNullOrWhiteSpace(q) || q.Length < 2)
            return Ok(Array.Empty<object>());

        var root = rootId == 0 ? ContentReference.RootPage : new ContentReference(rootId);
        var results = new List<object>();

        try
        {
            var descendants = _contentRepository.GetDescendents(root);
            foreach (var descRef in descendants.Take(2000))
            {
                if (results.Count >= 50) break;

                if (_contentLoader.TryGet<IContent>(descRef, out var content) &&
                    content.Name.Contains(q, StringComparison.OrdinalIgnoreCase))
                {
                    results.Add(MapContent(content));
                }
            }
        }
        catch
        {
            // Fallback: return empty on error
        }

        return Ok(results);
    }

    [HttpGet]
    public IActionResult GetContentTypes([FromQuery] string? q)
    {
        var types = _contentTypeRepository.List()
            .Where(t => t.ModelType != null)
            .Select(ct => new
            {
                ct.ID,
                ct.Name,
                DisplayName = ct.DisplayName ?? ct.Name,
                ct.Description,
                ct.GroupName,
                Base = ct.Base.ToString(),
                IsSystemType = ct.ModelType?.Namespace?.StartsWith("EPiServer", StringComparison.OrdinalIgnoreCase) == true
            });

        if (!string.IsNullOrWhiteSpace(q))
        {
            types = types.Where(t =>
                t.DisplayName.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                t.Name.Contains(q, StringComparison.OrdinalIgnoreCase));
        }

        return Ok(types.OrderBy(t => t.DisplayName).ToList());
    }

    private object MapContent(IContent content)
    {
        bool hasChildren;
        try
        {
            hasChildren = _contentLoader.GetChildren<IContent>(
                content.ContentLink,
                new LoaderOptions { LanguageLoaderOption.FallbackWithMaster() })
                .Any();
        }
        catch
        {
            hasChildren = false;
        }

        var contentType = _contentTypeRepository.Load(content.ContentTypeID);

        return new
        {
            Id = content.ContentLink.ID,
            content.Name,
            TypeName = contentType?.DisplayName ?? contentType?.Name ?? "Unknown",
            HasChildren = hasChildren
        };
    }
}
```

- [ ] **Step 2: Update FeaturesApiController** — read file, remove `[Route("editorpowertools/api")]` class attr, remove `"features"` path string from `[HttpGet("features")]` → `[HttpGet]`

- [ ] **Step 3: Update PreferencesApiController** — read file, remove `[Route("editorpowertools/api/preferences")]` class attr, change `[HttpGet("{toolName}")]` → `[HttpGet]` and `[HttpPost("{toolName}")]` → `[HttpPost]`, change `string toolName` parameter to `[FromQuery] string id`

- [ ] **Step 4: Update UiStringsController** — read file, remove `[Route("editorpowertools/api/ui-strings")]` class attr, ensure action has only `[HttpGet]`

- [ ] **Step 5: Build**

```
dotnet build src/EditorPowertools/EditorPowertools.csproj
```

Expected: 0 errors

- [ ] **Step 6: Commit**

```bash
git add src/EditorPowertools/Components/ComponentsApiController.cs \
        src/EditorPowertools/Components/FeaturesApiController.cs \
        src/EditorPowertools/Components/PreferencesApiController.cs \
        src/EditorPowertools/Localization/UiStringsController.cs
git commit -m "feat(routing): remove hardcoded routes from Components, Features, Preferences, UiStrings controllers"
```

---

## Task 10 — Fix activity-timeline.js

**Files:**
- Modify: `src/EditorPowertools/modules/_protected/EditorPowertools/ClientResources/js/activity-timeline.js`

**URL changes:**
```
window.EPT_API_URL + '/activity'            → window.EPT_BASE_URL + 'ActivityTimelineApi/'
${API}/timeline                             → ${API}GetTimeline
${API}/stats                                → ${API}GetStats
${API}/users                                → ${API}GetUsers
${API}/content-types                        → ${API}GetContentTypes
${API}/compare/${contentId}/${versionId}    → ${API}CompareVersions?contentId=${contentId}&versionId=${versionId}
${API}/aggregation-status                   → (check if referenced; if so → ContentTypeAuditApi/GetAggregationStatus)
```

- [ ] **Step 1: Read the full file and apply replacements**

In `activity-timeline.js`:
1. Find the line `const API = window.EPT_API_URL + '/activity';` (or similar) and change to:
   ```js
   const API = window.EPT_BASE_URL + 'ActivityTimelineApi/';
   ```
2. Replace all sub-path string concatenations:
   - `` `${API}/timeline` `` or `API + '/timeline'` → `` `${API}GetTimeline` ``
   - `` `${API}/stats` `` → `` `${API}GetStats` ``
   - `` `${API}/users` `` → `` `${API}GetUsers` ``
   - `` `${API}/content-types` `` → `` `${API}GetContentTypes` ``
   - `` `${API}/compare/${c}/${v}` `` → `` `${API}CompareVersions?contentId=${c}&versionId=${v}` ``
   - Any `EPT_API_URL + '/aggregation-status'` → `window.EPT_BASE_URL + 'ActivityTimelineApi/GetAggregationStatus'` (or the appropriate controller)
   - Any `EPT_API_URL + '/features'` → `window.EPT_BASE_URL + 'FeaturesApi/GetFeatures'`

- [ ] **Step 2: Commit**

```bash
git add src/EditorPowertools/modules/_protected/EditorPowertools/ClientResources/js/activity-timeline.js
git commit -m "feat(routing): update activity-timeline.js to use EPT_BASE_URL"
```

---

## Task 11 — Fix audience-manager.js

**Files:**
- Modify: `src/EditorPowertools/modules/_protected/EditorPowertools/ClientResources/js/audience-manager.js`

**URL changes (apply same pattern):**
```
window.EPT_API_URL + '/audience'         → window.EPT_BASE_URL + 'AudienceManagerApi/'
${API}/visitor-groups                    → ${API}GetVisitorGroups
${API}/visitor-groups/${id}/criteria     → ${API}GetCriteria/${id}
${API}/visitor-groups/${id}/usages       → ${API}GetUsages/${id}
```

- [ ] **Step 1: Read the full file, find API base line and all fetch calls, apply replacements above**

- [ ] **Step 2: Commit**

```bash
git add src/EditorPowertools/modules/_protected/EditorPowertools/ClientResources/js/audience-manager.js
git commit -m "feat(routing): update audience-manager.js to use EPT_BASE_URL"
```

---

## Task 12 — Fix bulk-property-editor.js

**Files:**
- Modify: `src/EditorPowertools/modules/_protected/EditorPowertools/ClientResources/js/bulk-property-editor.js`

**URL changes:**
```
window.EPT_API_URL + '/bulk-editor'        → window.EPT_BASE_URL + 'BulkPropertyEditorApi/'
${API}/content-types                       → ${API}GetContentTypes
${API}/languages                           → ${API}GetLanguages
${API}/properties/${id}                    → ${API}GetProperties/${id}
${API}/content                             → ${API}GetContent
${API}/references/${id}                    → ${API}GetReferences/${id}
${API}/save                                → ${API}Save
${API}/publish/${id}                       → ${API}Publish/${id}
${API}/bulk-save                           → ${API}BulkSave
```

- [ ] **Step 1: Read the full file, find API base line and all fetch/post calls, apply replacements above**

- [ ] **Step 2: Commit**

```bash
git add src/EditorPowertools/modules/_protected/EditorPowertools/ClientResources/js/bulk-property-editor.js
git commit -m "feat(routing): update bulk-property-editor.js to use EPT_BASE_URL"
```

---

## Task 13 — Fix cms-doctor.js

**Files:**
- Modify: `src/EditorPowertools/modules/_protected/EditorPowertools/ClientResources/js/cms-doctor.js`

**URL changes:**
```
window.EPT_API_URL + '/cms-doctor'         → window.EPT_BASE_URL + 'CmsDoctorApi/'
${API}/dashboard                           → ${API}GetDashboard
${API}/run-all                             → ${API}RunAll
${API}/run/${type}                         → ${API}RunCheck?id=${type}
${API}/fix/${type}                         → ${API}FixCheck?id=${type}
${API}/dismiss/${type}                     → ${API}DismissCheck?id=${type}
${API}/restore/${type}                     → ${API}RestoreCheck?id=${type}
${API}/tags                                → ${API}GetTags
```

- [ ] **Step 1: Read the full file, apply replacements above**

- [ ] **Step 2: Commit**

```bash
git add src/EditorPowertools/modules/_protected/EditorPowertools/ClientResources/js/cms-doctor.js
git commit -m "feat(routing): update cms-doctor.js to use EPT_BASE_URL"
```

---

## Task 14 — Fix content-audit.js

**Files:**
- Modify: `src/EditorPowertools/modules/_protected/EditorPowertools/ClientResources/js/content-audit.js`

**URL changes:**
```
window.EPT_API_URL + '/content-audit'      → window.EPT_BASE_URL + 'ContentAuditApi/'
${API} (root GET)                          → ${API}GetContent
${API}/export                              → ${API}Export
${API}/export-request                      → ${API}RequestExport
${API}/export-status                       → ${API}GetExportStatus
```

- [ ] **Step 1: Read the full file, find the API base variable and all fetch calls, apply replacements**

- [ ] **Step 2: Commit**

```bash
git add src/EditorPowertools/modules/_protected/EditorPowertools/ClientResources/js/content-audit.js
git commit -m "feat(routing): update content-audit.js to use EPT_BASE_URL"
```

---

## Task 15 — Fix content-importer.js and content-statistics.js

**Files:**
- Modify: `src/EditorPowertools/modules/_protected/EditorPowertools/ClientResources/js/content-importer.js`
- Modify: `src/EditorPowertools/modules/_protected/EditorPowertools/ClientResources/js/content-statistics.js`

**content-importer.js URL changes (current: `var API = window.EPT_API_URL + '/content-importer'`):**
```
window.EPT_API_URL + '/content-importer'   → window.EPT_BASE_URL + 'ContentImporterApi/'
API + '/content-types'                     → API + 'GetContentTypes'
API + '/content-types/' + id               → API + 'GetContentType/' + id
API + '/block-types'                       → API + 'GetBlockTypes'
API + '/languages'                         → API + 'GetLanguages'
API + '/upload'                            → API + 'Upload'
API + '/dry-run'                           → API + 'DryRun'
API + '/execute'                           → API + 'Execute'
API + '/progress/' + sessionId             → API + 'GetProgress/' + sessionId
```

**content-statistics.js URL changes (current: `var API_URL = window.EPT_API_URL + '/content-statistics/dashboard'`):**
```js
// Replace the two separate variables:
var API_URL = window.EPT_API_URL + '/content-statistics/dashboard';
// and references to window.EPT_API_URL + '/aggregation-status'
// and window.EPT_API_URL + '/content-statistics/aggregation-start'

// With:
var API_URL = window.EPT_BASE_URL + 'ContentStatisticsApi/GetDashboard';
// EPT_API_URL + '/aggregation-status' → window.EPT_BASE_URL + 'ContentTypeAuditApi/GetAggregationStatus'
// EPT_API_URL + '/content-statistics/aggregation-start' → window.EPT_BASE_URL + 'ContentStatisticsApi/StartAggregationJob'
```

- [ ] **Step 1: Read content-importer.js in full, apply URL replacements**

- [ ] **Step 2: Read content-statistics.js in full, apply URL replacements**

- [ ] **Step 3: Commit**

```bash
git add src/EditorPowertools/modules/_protected/EditorPowertools/ClientResources/js/content-importer.js \
        src/EditorPowertools/modules/_protected/EditorPowertools/ClientResources/js/content-statistics.js
git commit -m "feat(routing): update content-importer.js and content-statistics.js to use EPT_BASE_URL"
```

---

## Task 16 — Fix remaining tool JS files

**Files:**
- Modify: `src/EditorPowertools/modules/_protected/EditorPowertools/ClientResources/js/content-type-recommendations.js`
- Modify: `src/EditorPowertools/modules/_protected/EditorPowertools/ClientResources/js/language-audit.js`
- Modify: `src/EditorPowertools/modules/_protected/EditorPowertools/ClientResources/js/link-checker.js`
- Modify: `src/EditorPowertools/modules/_protected/EditorPowertools/ClientResources/js/manage-children.js`
- Modify: `src/EditorPowertools/modules/_protected/EditorPowertools/ClientResources/js/personalization-audit.js`
- Modify: `src/EditorPowertools/modules/_protected/EditorPowertools/ClientResources/js/scheduled-jobs-gantt.js`
- Modify: `src/EditorPowertools/modules/_protected/EditorPowertools/ClientResources/js/security-audit.js`
- Modify: `src/EditorPowertools/modules/_protected/EditorPowertools/ClientResources/js/visitor-group-tester.js` (if exists)

**URL changes per file:**

`content-type-recommendations.js` (`const API = window.EPT_API_URL + '/recommendations'`):
```
API → window.EPT_BASE_URL + 'ContentTypeRecommendationsApi/'
${API}/rules            → ${API}GetRules  (GET) / ${API}SaveRule (POST)
${API}/rules/${id}      → ${API}DeleteRule/${id}
${API}/evaluate         → ${API}EvaluateRules
${API}/content-types    → ${API}GetContentTypes
```

`language-audit.js` (`const API = window.EPT_API_URL`):
```
API → window.EPT_BASE_URL + 'LanguageAuditApi/'  (but note it currently uses bare EPT_API_URL)
${API}/language-audit/overview      → ${API}GetOverview
${API}/language-audit/missing       → ${API}GetMissingTranslations
${API}/language-audit/coverage-tree → ${API}GetCoverageTree
${API}/language-audit/stale         → ${API}GetStaleTranslations
${API}/language-audit/queue         → ${API}GetTranslationQueue
${API}/language-audit/export        → ${API}ExportTranslationQueue
${API}/aggregation-status           → window.EPT_BASE_URL + 'LanguageAuditApi/GetAggregationStatus'  (check which controller)
${API}/aggregation-start            → window.EPT_BASE_URL + 'LanguageAuditApi/StartAggregationJob'
```

`link-checker.js` (`const API = window.EPT_API_URL + '/link-checker'`):
```
API → window.EPT_BASE_URL + 'LinkCheckerApi/'
${API}/links        → ${API}GetLinks
${API}/stats        → ${API}GetStats
${API}/job-status   → ${API}GetJobStatus
${API}/job-start    → ${API}StartJob
```

`manage-children.js`:
```
window.EPT_API_URL + '/manage-children' → window.EPT_BASE_URL + 'ManageChildrenApi/'
${API}/${parentId}              → ${API}GetChildren/${parentId}
${API}/parent/${id}             → ${API}GetParent/${id}
${API}/delete                   → ${API}BulkDelete
${API}/delete-permanently       → ${API}BulkDeletePermanently
${API}/publish                  → ${API}BulkPublish
${API}/unpublish                → ${API}BulkUnpublish
${API}/move                     → ${API}BulkMove
```

`personalization-audit.js` (`const API = window.EPT_API_URL + '/personalization'`):
```
API → window.EPT_BASE_URL + 'PersonalizationAuditApi/'
${API}/usages           → ${API}GetUsages
${API}/visitor-groups   → ${API}GetVisitorGroups
${API}/job-status       → ${API}GetJobStatus
${API}/job-start        → ${API}StartJob
```

`scheduled-jobs-gantt.js` (`var API_BASE = window.EPT_API_URL + '/jobs-gantt/'`):
```
API_BASE → window.EPT_BASE_URL + 'ScheduledJobsGanttApi/'
API_BASE + 'jobs'           → API_BASE + 'GetJobs'
API_BASE + 'executions'     → API_BASE + 'GetExecutions'
API_BASE + 'gantt-data'     → API_BASE + 'GetGanttData'
```

`security-audit.js` (`var API = window.EPT_API_URL + '/security-audit'`):
```
API → window.EPT_BASE_URL + 'SecurityAuditApi/'
${API}/tree/children            → ${API}GetChildren
${API}/tree/node/${id}          → ${API}GetNodeDetail/${id}
${API}/tree/path/${id}          → ${API}GetPathToContent/${id}
${API}/roles                    → ${API}GetRoles
${API}/roles/${name}/content    → ${API}GetContentForRole?name=${name}
${API}/issues/summary           → ${API}GetIssuesSummary
${API}/issues                   → ${API}GetIssues
${API}/status                   → ${API}GetStatus
${API}/aggregation-start        → ${API}StartAggregationJob
${API}/export                   → ${API}Export
```

`visitor-group-tester.js` (if exists, `EPT_API_URL + '/visitor-group-tester/groups'`):
```
window.EPT_API_URL + '/visitor-group-tester/groups' → window.EPT_BASE_URL + 'VisitorGroupTesterApi/GetGroups'
```

- [ ] **Step 1: Read and update each file listed above, applying the URL changes**

- [ ] **Step 2: Build (JS is not compiled, but verify no EPT_API_URL references remain)**

```bash
grep -r "EPT_API_URL" src/EditorPowertools/modules/_protected/EditorPowertools/ClientResources/js/
```

Expected: no output

- [ ] **Step 3: Commit**

```bash
git add src/EditorPowertools/modules/_protected/EditorPowertools/ClientResources/js/
git commit -m "feat(routing): update remaining tool JS files to use EPT_BASE_URL"
```

---

## Task 17 — Fix components.js and active-editors JS files

**Files:**
- Modify: `src/EditorPowertools/modules/_protected/EditorPowertools/ClientResources/js/components.js`
- Modify: `src/EditorPowertools/modules/_protected/EditorPowertools/ClientResources/js/active-editors-overview.js`
- Modify: `src/EditorPowertools/modules/_protected/EditorPowertools/ClientResources/js/ContentDetailsWidget.js`
- Modify: `src/EditorPowertools/modules/_protected/EditorPowertools/ClientResources/js/EditorPowertoolsCommandsInitializer.js` (if it uses EPT_API_URL)

**components.js** (`var API = window.EPT_API_URL + '/components'`):
```
API → window.EPT_BASE_URL + 'ComponentsApi/'
${API}/content/${id}            → ${API}GetContent/${id}
${API}/content/${id}/children   → ${API}GetChildren/${id}
${API}/content/search           → ${API}SearchContent
${API}/content-types            → ${API}GetContentTypes
```

**active-editors-overview.js:**
```
window.EPT_API_URL + '/features'                      → window.EPT_BASE_URL + 'FeaturesApi/GetFeatures'
window.EPT_HUB_URL + '/active-editors'                → window.EPT_HUB_URL + '/active-editors'  ← no change needed (EPT_HUB_URL already fixed in Task 2)
```

**active-editors-tracker.js:**
```
window.EPT_HUB_URL + "/active-editors"                → no change (EPT_HUB_URL already fixed in Task 2)
```

**ContentDetailsWidget.js:**
```
window.EPT_API_URL + "/content-details/" + contentId  → window.EPT_BASE_URL + 'ContentDetailsApi/GetDetails/' + contentId
```

- [ ] **Step 1: Read and update components.js**

- [ ] **Step 2: Read and update active-editors-overview.js** (only the `EPT_API_URL + '/features'` call)

- [ ] **Step 3: Read and update ContentDetailsWidget.js** (the single `EPT_API_URL` usage)

- [ ] **Step 4: Search for any remaining EPT_API_URL usages**

```bash
grep -r "EPT_API_URL" src/EditorPowertools/modules/_protected/EditorPowertools/ClientResources/
```

Expected: no output

- [ ] **Step 5: Also scan Razor views and C# for any stray references**

```bash
grep -r "EPT_API_URL\|editorpowertools/api\|editorpowertools/hubs" src/EditorPowertools/ --include="*.cs" --include="*.cshtml"
```

Expected: no output (the route strings should be gone from all C# and Razor files)

- [ ] **Step 6: Commit**

```bash
git add src/EditorPowertools/modules/_protected/EditorPowertools/ClientResources/js/
git commit -m "feat(routing): update components.js, active-editors, ContentDetailsWidget to use EPT_BASE_URL"
```

---

## Task 18 — Fix UiStrings widget endpoint usage

**Files:**
- Check: `src/EditorPowertools/modules/_protected/EditorPowertools/ClientResources/js/ActiveEditorsWidget.js`
- Check: `src/EditorPowertools/modules/_protected/EditorPowertools/ClientResources/js/ContentDetailsWidget.js`

Both widgets fetch `EPT_BASE_URL + 'EditorPowertools/WidgetStrings'` — this is the page controller action, not an API route, and is already module-path-aware. **No change needed for this pattern.**

Also check if `UiStrings` endpoint is called anywhere (was at `EPT_API_URL + '/ui-strings'`):
```bash
grep -r "ui-strings\|UiStrings" src/EditorPowertools/modules/_protected/EditorPowertools/ClientResources/
```

If found, update to `window.EPT_BASE_URL + 'UiStrings/Get'`.

- [ ] **Step 1: Run the grep above**

- [ ] **Step 2: If any hits, update those JS files to use `window.EPT_BASE_URL + 'UiStrings/Get'`**

- [ ] **Step 3: Final comprehensive verification**

```bash
# No JS files should reference EPT_API_URL
grep -r "EPT_API_URL" src/EditorPowertools/ --include="*.js" --include="*.cshtml" --include="*.cs"

# No controllers should have hardcoded route prefixes
grep -r "editorpowertools/api\|editorpowertools/hubs" src/EditorPowertools/ --include="*.cs"
```

Both commands: Expected: no output

- [ ] **Step 4: Build the full solution**

```
dotnet build
```

Expected: 0 errors, 0 warnings related to routing

- [ ] **Step 5: Commit and tag if clean**

```bash
git add -A
git commit -m "feat(routing): final cleanup — all API URLs now module-path-aware"
```

---

## Self-review checklist

- [x] All 21 API controllers covered (Tasks 3–9)
- [x] All JS files covered (Tasks 10–18)
- [x] `EPT_API_URL` removed from layout (Task 2)
- [x] `EPT_HUB_URL` made dynamic via `Paths.ToResource()` (Tasks 1 + 2)
- [x] Conventional route registered at module path (Task 1)
- [x] Multi-param routes changed to query strings (ActivityTimeline CompareVersions, SecurityAudit GetContentForRole)
- [x] Route params renamed to `id` where needed (ManageChildren, ContentDetails, Components, etc.)
- [x] `PreferencesApiController` `toolName` changed to `[FromQuery] string id`
- [x] Verification steps check zero remaining `EPT_API_URL` / hardcoded routes
- [x] Each task ends with a build and commit
