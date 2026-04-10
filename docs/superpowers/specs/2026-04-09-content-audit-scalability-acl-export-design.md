# Content Audit: Scalability, ACL, and Export Redesign

**Date:** 2026-04-09  
**Status:** Approved

## Problem Statement

Three issues in the current `ContentAuditService`:

1. **Memory/scalability**: `GetDescendents(RootPage)` causes all matching `ContentAuditRow` objects to accumulate in memory before sort+page. Risks OOM at 75k+ items.
2. **ACL violation**: Content items are loaded and returned without verifying the current user's `AccessLevel.Read`. Items the user has no access to appear in audit results and exports.
3. **Export scalability**: Export runs in-request, blocking until all content is processed. Cannot handle large sites.

---

## Architecture

### Pluggable Data Provider

A new `IContentAuditDataProvider` interface replaces the direct `GetDescendents` call. `ContentAuditService` becomes a thin coordinator.

```csharp
public interface IContentAuditDataProvider
{
    /// TotalCount is null when the provider cannot determine it cheaply (e.g. default provider).
    ContentAuditPageResult GetPage(ContentAuditRequest request, CancellationToken ct);

    /// Streams all matching rows for export jobs. May be slow on the default provider.
    IEnumerable<ContentAuditRow> GetAllRows(ContentAuditExportRequest request, CancellationToken ct);
}

public class ContentAuditPageResult
{
    public List<ContentAuditRow> Items { get; init; } = [];
    public int? TotalCount { get; init; }  // null = unknown
}
```

The registered implementation is resolved via DI. Sites using Optimizely Find can register a search-backed provider that returns exact totals with server-side filter/sort. The default provider is registered automatically by `AddEditorPowertools()`.

### Default Provider — `GetDescendentsContentAuditProvider`

- Walks `GetDescendents(RootPage)` lazily using skip/take to reach the requested page offset.
- Loads each `IContent` item one at a time.
- Checks `IContentAccessEvaluator.HasAccess(content, PrincipalInfo.CurrentPrincipal, AccessLevel.Read)` — skips denied items.
- Applies all filters (mainType, quickFilter, search, column filters) in-memory as it scans.
- Returns `TotalCount = null` because counting requires scanning the entire tree.
- Sorting is not supported; `SortBy` is ignored with a logged warning.

**Known limitation**: Deep page offsets are slow because the IEnumerable must be enumerated from the start. This is acceptable because: (a) the UI is a sample/preview, not a data grid, and (b) sites needing full sort/count/search should register a search provider.

### ACL Fix

`IContentAccessEvaluator` is injected into both the service and the default provider. Every `IContent` item is checked before `BuildRow` is called. The check uses `PrincipalInfo.CurrentPrincipal` (Optimizely's ambient principal — correct for web requests). Denied items are silently skipped; the skip is logged at Debug level.

This fix covers both the paged query endpoint and the export job.

### UI Pagination

When `TotalCount` is null, the `ContentAuditResponse` sets `TotalPages = null`. The JS switches from numbered pagination to Prev/Next navigation and shows a banner:

> "Showing a preview of content. Run a full export for complete results."

When `TotalCount` is populated (search provider), numbered pagination works as before.

---

## Export via Optimizely Scheduled Job

### Flow

1. User configures export (format, columns, filters) in the Content Audit UI.
2. `POST /editorpowertools/api/content-audit/export-request` saves the export config to DDS, keyed by a new GUID. Returns `{ jobRequestId }`.
3. The endpoint triggers `IScheduledJobExecutor.StartAsync(contentAuditExportJobId)`.
4. JS polls `GET /editorpowertools/api/content-audit/export-status?requestId={jobRequestId}` every 3 seconds.
5. On completion, the status response includes the CMS content link / download URL of the generated file.
6. UI shows a download button.

### Job Implementation — `ContentAuditExportJob`

- Decorated with `[ScheduledPlugIn(DisplayName = "[EditorPowertools] Content Audit Export")]`.
- On execute: reads the latest export config from DDS.
- Iterates `IContentAuditDataProvider.GetAllRows()` — ACL check applies here too.
- Streams the output into a `IBlobFactory`-backed stream.
- Creates a `GenericMedia` content item in the configured report folder.
- Saves `ContentReference` of the created media file back to DDS (keyed by jobRequestId).
- Updates DDS status to `Completed` / `Failed`.

### Report Folder

The export job creates the output as a CMS media file. On first run, if the folder does not exist it is created under `ContentReference.GlobalAssetsRoot`. Folder name is configurable:

```csharp
public class ContentAuditOptions
{
    public string ReportFolderName { get; set; } = "Internal Reports";
}
```

`EditorPowertoolsOptions` gets a new `ContentAudit` property of type `ContentAuditOptions`.

The media file is named `content-audit-{timestamp}.{ext}` and is visible in the CMS Assets panel under the configured folder.

---

## DDS Records

Two new DDS-stored types:

**`ContentAuditExportRequest`** (persisted when user triggers export)
- `Id` (Guid)
- `RequestedAt` (DateTime)
- `RequestedBy` (string — username)
- `Format`, `Columns`, `Filters`, `MainTypeFilter`, `QuickFilter`, `SortBy`, `SortDirection`

**`ContentAuditExportStatus`** (updated by job)
- `RequestId` (Guid)
- `Status`: `Pending | Running | Completed | Failed`
- `ResultContentLink` (string, nullable) — CMS ContentReference of completed file
- `ErrorMessage` (string, nullable)
- `CompletedAt` (DateTime, nullable)

Old completed/failed records should be cleaned up — the export job deletes records older than 7 days on each run.

---

## Options Summary

```csharp
// EditorPowertoolsOptions
public ContentAuditOptions ContentAudit { get; set; } = new();

public class ContentAuditOptions
{
    /// CMS folder name (under Global Assets) where export files are stored.
    public string ReportFolderName { get; set; } = "Internal Reports";
}
```

No option for max items — the pluggable provider pattern makes a hard cap unnecessary. Sites with large content trees should register a search provider.

---

## What Stays the Same

- `ContentAuditController` (Razor view), all JS column/filter/sort definitions.
- `ContentAuditRow` DTO shape.
- `BuildRow`, `GetColumnValue`, `GetCellValue`, `GetColumnLabel`, sorting logic — these move into the default provider unchanged.
- XLSX / CSV / JSON rendering code — moves from the controller into a new `ContentAuditExportRenderer` helper called by the job.
- All localization keys.

---

## Files Affected

| File | Change |
|---|---|
| `Tools/ContentAudit/IContentAuditDataProvider.cs` | New — interface |
| `Tools/ContentAudit/GetDescendentsContentAuditProvider.cs` | New — default provider (replaces service internals) |
| `Tools/ContentAudit/ContentAuditService.cs` | Simplified — delegates to provider, removed BuildRow/filtering logic |
| `Tools/ContentAudit/ContentAuditController.cs` | Updated — export trigger + status polling endpoints |
| `Tools/ContentAudit/ContentAuditExportJob.cs` | New — scheduled job |
| `Tools/ContentAudit/ContentAuditExportRenderer.cs` | New — XLSX/CSV/JSON rendering (extracted from controller) |
| `Tools/ContentAudit/Models/ContentAuditDtos.cs` | Add `ContentAuditPageResult`, `ContentAuditExportRequest`, `ContentAuditExportStatus` |
| `Configuration/EditorPowertoolsOptions.cs` | Add `ContentAudit` property |
| `Configuration/ContentAuditOptions.cs` | New |
| `ServiceCollectionExtensions.cs` | Register new types |
| JS `content-audit.js` | Prev/Next pagination, export trigger/poll UI |
