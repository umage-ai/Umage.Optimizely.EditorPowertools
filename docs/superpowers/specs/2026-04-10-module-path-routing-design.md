# Module-Path-Aware Routing Design

**Date:** 2026-04-10  
**Status:** Approved

## Problem

All ~21 API controllers and the SignalR hub currently use hardcoded paths like
`editorpowertools/api/...` and `/editorpowertools/hubs/...`. These bypass the
Optimizely module virtual-path system, meaning the URLs are fixed regardless of
how/where the module is installed. The JS globals `EPT_API_URL` and `EPT_HUB_URL`
are likewise hardcoded in `_PowertoolsLayout.cshtml`.

`EPT_BASE_URL` is already computed correctly via `Paths.ToResource()` but is
underused — it is the correct base for all module URLs.

## Design

### 1. Conventional route registration (`ApplicationBuilderExtensions.cs`)

Register a module-path-aware conventional route in `MapEditorPowertools`:

```csharp
var modulePath = Paths.ToResource(typeof(EditorPowertoolsMenuProvider), "").TrimStart('/').TrimEnd('/');
endpoints.MapControllerRoute(
    "EditorPowertoolsDefault",
    modulePath + "/{controller}/{action}/{id?}");
endpoints.MapHub<ActiveEditorsHub>(
    Paths.ToResource(typeof(EditorPowertoolsMenuProvider), "hubs/active-editors"));
```

This makes every module controller accessible at `{EPT_BASE_URL}{ControllerName}/{ActionName}` without any hardcoded prefix.

### 2. Backend controllers — remove all hardcoded `[Route]` attributes

**Remove** all class-level `[Route("editorpowertools/api/...")]` attributes and all
action-level explicit path strings (e.g. `[HttpGet("activity/timeline")]`).

Keep HTTP method attributes **without** path strings:
- `[HttpGet]`, `[HttpPost]`, `[HttpDelete]`, `[HttpPut]` — method constraint only

Conventional routing maps `{controller}/{action}` automatically. The controller
class name minus the `Controller` suffix becomes the URL segment:
- `ContentAuditApiController.GetContent` → `ContentAuditApi/GetContent`
- `ActivityTimelineApiController.GetTimeline` → `ActivityTimelineApi/GetTimeline`

For route parameters, use `[HttpGet("{id}")]` (single-param form only). Actions
currently accepting multiple route segments (e.g. `compare/{contentId}/{versionId}`)
are switched to query-string parameters instead; the action URL becomes
`ActivityTimelineApi/CompareVersions?contentId=1&versionId=2`.

### 3. Layout — fix `EPT_API_URL` and `EPT_HUB_URL`

In `_PowertoolsLayout.cshtml`, replace:
```js
window.EPT_API_URL = '/editorpowertools/api';
window.EPT_HUB_URL = '/editorpowertools/hubs';
```
with:
```js
// EPT_API_URL removed — JS uses EPT_BASE_URL directly
window.EPT_HUB_URL = '@Html.Raw(Paths.ToResource(typeof(EditorPowertoolsMenuProvider), "hubs"))';
```

`EPT_BASE_URL` already holds the correct module path and is the sole base for API calls.

### 4. JS files — update all API call URLs

All JS files change from:
```js
fetch(window.EPT_API_URL + '/content-audit')
```
to:
```js
fetch(window.EPT_BASE_URL + 'ContentAuditApi/GetContent')
```

Shell widgets that fetch strings (`WidgetStrings` endpoint) already use `EPT_BASE_URL`
and stay unchanged.

## URL Mapping Table

| Old hardcoded URL | New URL (relative to EPT_BASE_URL) |
|---|---|
| `/activity/timeline` | `ActivityTimelineApi/GetTimeline` |
| `/activity/stats` | `ActivityTimelineApi/GetStats` |
| `/activity/compare/{c}/{v}` | `ActivityTimelineApi/CompareVersions?contentId={c}&versionId={v}` |
| `/activity/users` | `ActivityTimelineApi/GetUsers` |
| `/activity/content-types` | `ActivityTimelineApi/GetContentTypes` |
| `/audience/visitor-groups` | `AudienceManagerApi/GetVisitorGroups` |
| `/audience/visitor-groups/{id}/criteria` | `AudienceManagerApi/GetCriteria/{id}` |
| `/audience/visitor-groups/{id}/usages` | `AudienceManagerApi/GetUsages/{id}` |
| `/bulk-editor/content-types` | `BulkPropertyEditorApi/GetContentTypes` |
| `/bulk-editor/languages` | `BulkPropertyEditorApi/GetLanguages` |
| `/bulk-editor/properties/{id}` | `BulkPropertyEditorApi/GetProperties/{id}` |
| `/bulk-editor/content` | `BulkPropertyEditorApi/GetContent` |
| `/bulk-editor/references/{id}` | `BulkPropertyEditorApi/GetReferences/{id}` |
| `/bulk-editor/save` | `BulkPropertyEditorApi/Save` |
| `/bulk-editor/publish/{id}` | `BulkPropertyEditorApi/Publish/{id}` |
| `/bulk-editor/bulk-save` | `BulkPropertyEditorApi/BulkSave` |
| `/cms-doctor/dashboard` | `CmsDoctorApi/GetDashboard` |
| `/cms-doctor/run-all` | `CmsDoctorApi/RunAll` |
| `/cms-doctor/run/{type}` | `CmsDoctorApi/RunCheck/{type}` |
| `/cms-doctor/fix/{type}` | `CmsDoctorApi/FixCheck/{type}` |
| `/cms-doctor/dismiss/{type}` | `CmsDoctorApi/DismissCheck/{type}` |
| `/cms-doctor/restore/{type}` | `CmsDoctorApi/RestoreCheck/{type}` |
| `/cms-doctor/tags` | `CmsDoctorApi/GetTags` |
| `/content-audit` | `ContentAuditApi/GetContent` |
| `/content-audit/export` | `ContentAuditApi/Export` |
| `/content-audit/export-request` | `ContentAuditApi/RequestExport` |
| `/content-audit/export-status` | `ContentAuditApi/GetExportStatus` |
| `/content-details/{id}` | `ContentDetailsApi/GetDetails/{id}` |
| `/content-importer/upload` | `ContentImporterApi/Upload` |
| `/content-importer/content-types` | `ContentImporterApi/GetContentTypes` |
| `/content-importer/content-types/{id}` | `ContentImporterApi/GetContentType/{id}` |
| `/content-importer/block-types` | `ContentImporterApi/GetBlockTypes` |
| `/content-importer/languages` | `ContentImporterApi/GetLanguages` |
| `/content-importer/dry-run` | `ContentImporterApi/DryRun` |
| `/content-importer/execute` | `ContentImporterApi/Execute` |
| `/content-importer/progress/{session}` | `ContentImporterApi/GetProgress/{session}` |
| `/content-statistics/dashboard` | `ContentStatisticsApi/GetDashboard` |
| `/content-statistics/aggregation-start` | `ContentStatisticsApi/StartAggregationJob` |
| `/content-types` | `ContentTypeAuditApi/GetTypes` |
| `/content-types/{id}/properties` | `ContentTypeAuditApi/GetProperties/{id}` |
| `/content-types/{id}/content` | `ContentTypeAuditApi/GetContentOfType/{id}` |
| `/content/{id}/references` | `ContentTypeAuditApi/GetContentReferences/{id}` |
| `/content-types/inheritance-tree` | `ContentTypeAuditApi/GetInheritanceTree` |
| `/aggregation-status` | `ContentTypeAuditApi/GetAggregationStatus` |
| `/aggregation-start` | `ContentTypeAuditApi/StartAggregationJob` |
| `/recommendations/rules` | `ContentTypeRecommendationsApi/GetRules` |
| `/recommendations/rules` (POST) | `ContentTypeRecommendationsApi/SaveRule` |
| `/recommendations/rules/{id}` (DELETE) | `ContentTypeRecommendationsApi/DeleteRule/{id}` |
| `/recommendations/evaluate` | `ContentTypeRecommendationsApi/EvaluateRules` |
| `/recommendations/content-types` | `ContentTypeRecommendationsApi/GetContentTypes` |
| `/language-audit/overview` | `LanguageAuditApi/GetOverview` |
| `/language-audit/missing` | `LanguageAuditApi/GetMissingTranslations` |
| `/language-audit/coverage-tree` | `LanguageAuditApi/GetCoverageTree` |
| `/language-audit/stale` | `LanguageAuditApi/GetStaleTranslations` |
| `/language-audit/queue` | `LanguageAuditApi/GetTranslationQueue` |
| `/language-audit/export` | `LanguageAuditApi/ExportTranslationQueue` |
| `/language-audit/aggregation-start` | `LanguageAuditApi/StartAggregationJob` |
| `/link-checker/links` | `LinkCheckerApi/GetLinks` |
| `/link-checker/stats` | `LinkCheckerApi/GetStats` |
| `/link-checker/job-status` | `LinkCheckerApi/GetJobStatus` |
| `/link-checker/job-start` | `LinkCheckerApi/StartJob` |
| `/manage-children/{parentId}` | `ManageChildrenApi/GetChildren/{parentId}` |
| `/manage-children/parent/{id}` | `ManageChildrenApi/GetParent/{id}` |
| `/manage-children/delete` | `ManageChildrenApi/BulkDelete` |
| `/manage-children/delete-permanently` | `ManageChildrenApi/BulkDeletePermanently` |
| `/manage-children/publish` | `ManageChildrenApi/BulkPublish` |
| `/manage-children/unpublish` | `ManageChildrenApi/BulkUnpublish` |
| `/manage-children/move` | `ManageChildrenApi/BulkMove` |
| `/personalization/usages` | `PersonalizationAuditApi/GetUsages` |
| `/personalization/visitor-groups` | `PersonalizationAuditApi/GetVisitorGroups` |
| `/personalization/job-status` | `PersonalizationAuditApi/GetJobStatus` |
| `/personalization/job-start` | `PersonalizationAuditApi/StartJob` |
| `/jobs-gantt/jobs` | `ScheduledJobsGanttApi/GetJobs` |
| `/jobs-gantt/executions` | `ScheduledJobsGanttApi/GetExecutions` |
| `/jobs-gantt/gantt-data` | `ScheduledJobsGanttApi/GetGanttData` |
| `/security-audit/tree/children` | `SecurityAuditApi/GetChildren` |
| `/security-audit/tree/node/{id}` | `SecurityAuditApi/GetNodeDetail/{id}` |
| `/security-audit/tree/path/{id}` | `SecurityAuditApi/GetPathToContent/{id}` |
| `/security-audit/roles` | `SecurityAuditApi/GetRoles` |
| `/security-audit/roles/{name}/content` | `SecurityAuditApi/GetContentForRole/{name}` |
| `/security-audit/issues/summary` | `SecurityAuditApi/GetIssuesSummary` |
| `/security-audit/issues` | `SecurityAuditApi/GetIssues` |
| `/security-audit/status` | `SecurityAuditApi/GetStatus` |
| `/security-audit/aggregation-start` | `SecurityAuditApi/StartAggregationJob` |
| `/security-audit/export` | `SecurityAuditApi/Export` |
| `/visitor-group-tester/groups` | `VisitorGroupTesterApi/GetGroups` |
| `/components/content/{id}` | `ComponentsApi/GetContent/{id}` |
| `/components/content/{id}/children` | `ComponentsApi/GetChildren/{id}` |
| `/components/content/search` | `ComponentsApi/SearchContent` |
| `/components/content-types` | `ComponentsApi/GetContentTypes` |
| `/features` | `FeaturesApi/GetFeatures` |
| `/preferences/{tool}` | `PreferencesApi/Get/{tool}` |
| `/preferences/{tool}` (POST) | `PreferencesApi/Save/{tool}` |
| `/ui-strings` | `UiStringsController/Get` |
| SignalR `/editorpowertools/hubs/active-editors` | `{EPT_BASE_URL}hubs/active-editors` |

## Files Changed

**`Infrastructure/ApplicationBuilderExtensions.cs`** — add conventional route, fix hub path  
**`Views/Shared/_PowertoolsLayout.cshtml`** — remove `EPT_API_URL`, fix `EPT_HUB_URL`  
**21 API controllers** — remove class-level `[Route]`, simplify action-level attributes  
**~15 JS files** — replace `EPT_API_URL + '/...'` with `EPT_BASE_URL + 'Controller/Action'`

## What Does NOT Change

- `EPT_BASE_URL` (already correct)
- `EPT_CMS_URL`, `EPT_ADMIN_URL`, `EPT_VG_URL`
- `EditorPowertoolsController` page actions (no routes to change)
- All authorization attributes, `[RequireAjax]`, access checker logic
- All request/response shapes
