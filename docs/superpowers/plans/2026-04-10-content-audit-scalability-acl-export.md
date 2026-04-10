# Content Audit: Scalability, ACL & Export Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the in-memory full-scan approach with a pluggable data provider, enforce per-item ACL checks, and move bulk export to an Optimizely Scheduled Job that saves results as CMS media files.

**Architecture:** An `IContentAuditDataProvider` interface decouples data access from the service layer. The default provider walks `GetDescendents` lazily page-by-page (no TotalCount) with ACL filtering via `IContentAccessEvaluator`. Export moves out of the HTTP request into a `ScheduledJobBase` subclass that writes XLSX/CSV/JSON to Optimizely blob storage and creates a `ContentAuditReportMedia` content item in a configurable CMS folder.

**Tech Stack:** .NET 8, Optimizely CMS 12 (`EPiServer.*`), EPPlus (XLSX), `DynamicDataStore` for job request/status persistence, `IBlobFactory` for file storage, `IScheduledJobExecutor` for job triggering.

---

## File Map

| Action | Path | Responsibility |
|---|---|---|
| Create | `Configuration/ContentAuditOptions.cs` | Report folder name setting |
| Modify | `Configuration/EditorPowertoolsOptions.cs` | Add `ContentAudit` property |
| Modify | `Tools/ContentAudit/Models/ContentAuditDtos.cs` | Add `ContentAuditPageResult`, `ContentAuditExportJobRequest`, `ContentAuditExportJobStatus` |
| Create | `Tools/ContentAudit/IContentAuditDataProvider.cs` | Provider interface |
| Create | `Tools/ContentAudit/ContentAuditExportRenderer.cs` | Pure XLSX/CSV/JSON rendering (extracted from controller) |
| Create | `Tools/ContentAudit/GetDescendentsContentAuditProvider.cs` | Default provider — lazy scan with ACL |
| Modify | `Tools/ContentAudit/ContentAuditService.cs` | Thin coordinator delegating to provider |
| Create | `Tools/ContentAudit/ContentAuditReportMedia.cs` | CMS media content type for export files |
| Create | `Tools/ContentAudit/ContentAuditExportJob.cs` | Scheduled job — reads DDS, streams export, saves media |
| Modify | `Tools/ContentAudit/ContentAuditApiController.cs` | Add export-request / export-status endpoints; remove rendering |
| Modify | `Infrastructure/ServiceCollectionExtensions.cs` | Register new types |
| Modify | `modules/.../js/content-audit.js` | Prev/Next pagination, export trigger + poll |

All paths are relative to `src/EditorPowertools/`.

---

## Task 1: Add `ContentAuditOptions` and DTOs

**Files:**
- Create: `src/EditorPowertools/Configuration/ContentAuditOptions.cs`
- Modify: `src/EditorPowertools/Configuration/EditorPowertoolsOptions.cs`
- Modify: `src/EditorPowertools/Tools/ContentAudit/Models/ContentAuditDtos.cs`

- [ ] **Step 1: Create `ContentAuditOptions.cs`**

```csharp
namespace EditorPowertools.Configuration;

public class ContentAuditOptions
{
    /// <summary>
    /// Name of the CMS folder (under Global Assets) where export files are stored.
    /// The folder is created automatically on first export if it does not exist.
    /// </summary>
    public string ReportFolderName { get; set; } = "Internal Reports";
}
```

- [ ] **Step 2: Add `ContentAudit` property to `EditorPowertoolsOptions`**

Open `src/EditorPowertools/Configuration/EditorPowertoolsOptions.cs`.
Add after the `AuthorizedRoles` property:

```csharp
    /// <summary>
    /// Options specific to the Content Audit tool.
    /// </summary>
    public ContentAuditOptions ContentAudit { get; set; } = new();
```

- [ ] **Step 3: Add new DTOs to `ContentAuditDtos.cs`**

Add the following classes at the bottom of `src/EditorPowertools/Tools/ContentAudit/Models/ContentAuditDtos.cs` (above the last closing brace is fine — these are in the same namespace):

```csharp
/// <summary>
/// Returned by IContentAuditDataProvider.GetPage().
/// TotalCount is null when the provider cannot determine it without a full scan (e.g. default provider).
/// </summary>
public class ContentAuditPageResult
{
    public List<ContentAuditRow> Items { get; init; } = [];
    public int? TotalCount { get; init; }
}

/// <summary>
/// Persisted to DDS when a user triggers an export job.
/// </summary>
public class ContentAuditExportJobRequest
{
    public EPiServer.Data.Identity Id { get; set; } = EPiServer.Data.Identity.NewIdentity();
    public Guid RequestId { get; set; } = Guid.NewGuid();
    public string RequestedBy { get; set; } = "";
    public DateTime RequestedAt { get; set; } = DateTime.UtcNow;
    public string Format { get; set; } = "xlsx";
    public string? Columns { get; set; }       // comma-separated column keys
    public string? MainTypeFilter { get; set; }
    public string? QuickFilter { get; set; }
    public string? Search { get; set; }
    public string? FiltersJson { get; set; }   // JSON-serialized List<ContentAuditFilter>
    public string Status { get; set; } = "Pending"; // Pending | Running | Completed | Failed
    public string? ResultContentId { get; set; }    // ContentLink.ID of the generated media file
    public string? ErrorMessage { get; set; }
    public DateTime? CompletedAt { get; set; }
}
```

- [ ] **Step 4: Update `ContentAuditResponse` to support null `TotalPages`**

In `ContentAuditDtos.cs`, change `ContentAuditResponse`:

```csharp
public class ContentAuditResponse
{
    public List<ContentAuditRow> Items { get; set; } = [];
    public int? TotalCount { get; set; }    // null when provider cannot determine
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int? TotalPages { get; set; }    // null when TotalCount is null
}
```

- [ ] **Step 5: Commit**

```bash
git add src/EditorPowertools/Configuration/ContentAuditOptions.cs \
        src/EditorPowertools/Configuration/EditorPowertoolsOptions.cs \
        src/EditorPowertools/Tools/ContentAudit/Models/ContentAuditDtos.cs
git commit -m "feat(content-audit): add ContentAuditOptions, PageResult, and ExportJobRequest DTOs"
```

---

## Task 2: Define `IContentAuditDataProvider`

**Files:**
- Create: `src/EditorPowertools/Tools/ContentAudit/IContentAuditDataProvider.cs`

- [ ] **Step 1: Create the interface**

```csharp
using EditorPowertools.Tools.ContentAudit.Models;

namespace EditorPowertools.Tools.ContentAudit;

/// <summary>
/// Pluggable content data source for the Content Audit tool.
/// Register a custom implementation via DI to replace the default GetDescendents-based provider.
/// Search-backed providers (e.g. Optimizely Find) can implement full server-side
/// filtering, sorting, and accurate TotalCount.
/// </summary>
public interface IContentAuditDataProvider
{
    /// <summary>
    /// Returns one page of matching rows.
    /// <para><see cref="ContentAuditPageResult.TotalCount"/> is <c>null</c> when the provider
    /// cannot determine the total without a full tree scan (default provider).</para>
    /// </summary>
    ContentAuditPageResult GetPage(ContentAuditRequest request, CancellationToken ct = default);

    /// <summary>
    /// Streams all matching rows for the export job.
    /// Implementations should yield items one-by-one to avoid loading everything into memory.
    /// </summary>
    IEnumerable<ContentAuditRow> GetAllRows(ContentAuditExportRequest request, CancellationToken ct = default);
}
```

- [ ] **Step 2: Commit**

```bash
git add src/EditorPowertools/Tools/ContentAudit/IContentAuditDataProvider.cs
git commit -m "feat(content-audit): add IContentAuditDataProvider interface"
```

---

## Task 3: Extract `ContentAuditExportRenderer`

The controller currently contains `ExportXlsx`, `ExportCsv`, `ExportJson`, `GetCellValue`, `GetColumnLabel`, and CSV escape logic. Extract these into a standalone class so both the controller (in future) and the export job can use them.

**Files:**
- Create: `src/EditorPowertools/Tools/ContentAudit/ContentAuditExportRenderer.cs`
- Modify: `src/EditorPowertools/Tools/ContentAudit/ContentAuditApiController.cs`

- [ ] **Step 1: Create `ContentAuditExportRenderer.cs`**

```csharp
using System.Text;
using System.Text.Json;
using EditorPowertools.Tools.ContentAudit.Models;
using OfficeOpenXml;

namespace EditorPowertools.Tools.ContentAudit;

/// <summary>
/// Pure rendering — converts ContentAuditRow collections to XLSX, CSV, or JSON bytes.
/// No Optimizely dependencies; easily unit-tested.
/// </summary>
public class ContentAuditExportRenderer
{
    public byte[] RenderXlsx(IEnumerable<ContentAuditRow> rows, List<string> columns)
    {
        using var package = new ExcelPackage();
        var ws = package.Workbook.Worksheets.Add("Content Audit");

        for (int c = 0; c < columns.Count; c++)
        {
            ws.Cells[1, c + 1].Value = GetColumnLabel(columns[c]);
            ws.Cells[1, c + 1].Style.Font.Bold = true;
        }

        int r = 2;
        foreach (var row in rows)
        {
            for (int c = 0; c < columns.Count; c++)
                ws.Cells[r, c + 1].Value = GetCellValue(row, columns[c]);
            r++;
        }

        for (int c = 1; c <= columns.Count; c++)
        {
            ws.Column(c).AutoFit();
            if (ws.Column(c).Width > 50)
                ws.Column(c).Width = 50;
        }

        return package.GetAsByteArray();
    }

    public byte[] RenderCsv(IEnumerable<ContentAuditRow> rows, List<string> columns)
    {
        var sb = new StringBuilder();
        sb.AppendLine(string.Join(",", columns.Select(c => CsvEscape(GetColumnLabel(c)))));
        foreach (var row in rows)
            sb.AppendLine(string.Join(",", columns.Select(c => CsvEscape(GetCellValue(row, c)?.ToString() ?? ""))));

        return Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
    }

    public byte[] RenderJson(IEnumerable<ContentAuditRow> rows)
    {
        var json = JsonSerializer.Serialize(rows.ToList(), new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
        return Encoding.UTF8.GetBytes(json);
    }

    public string GetExtension(string format) => format.ToLowerInvariant() switch
    {
        "xlsx" => ".xlsx",
        "csv"  => ".csv",
        "json" => ".json",
        _      => ".bin"
    };

    public string GetContentType(string format) => format.ToLowerInvariant() switch
    {
        "xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        "csv"  => "text/csv",
        "json" => "application/json",
        _      => "application/octet-stream"
    };

    public static object? GetCellValue(ContentAuditRow row, string column) =>
        column.ToLowerInvariant() switch
        {
            "contentid"          => row.ContentId,
            "name"               => row.Name,
            "language"           => row.Language,
            "contenttype"        => row.ContentType,
            "maintype"           => row.MainType,
            "url"                => row.Url,
            "editurl"            => row.EditUrl,
            "breadcrumb"         => row.Breadcrumb,
            "status"             => row.Status,
            "createdby"          => row.CreatedBy,
            "created"            => row.Created?.ToString("yyyy-MM-dd HH:mm"),
            "changedby"          => row.ChangedBy,
            "changed"            => row.Changed?.ToString("yyyy-MM-dd HH:mm"),
            "published"          => row.Published?.ToString("yyyy-MM-dd HH:mm"),
            "publisheduntil"     => row.PublishedUntil?.ToString("yyyy-MM-dd HH:mm"),
            "masterlanguage"     => row.MasterLanguage,
            "alllanguages"       => row.AllLanguages,
            "referencecount"     => row.ReferenceCount,
            "versioncount"       => row.VersionCount,
            "haspersonalizations"=> row.HasPersonalizations == true ? "Yes" : "No",
            _                    => null
        };

    public static string GetColumnLabel(string column) =>
        column.ToLowerInvariant() switch
        {
            "contentid"          => "Content ID",
            "name"               => "Name",
            "language"           => "Language",
            "contenttype"        => "Content Type",
            "maintype"           => "Main Type",
            "url"                => "URL",
            "editurl"            => "Edit URL",
            "breadcrumb"         => "Breadcrumb",
            "status"             => "Status",
            "createdby"          => "Created By",
            "created"            => "Created",
            "changedby"          => "Changed By",
            "changed"            => "Changed",
            "published"          => "Published",
            "publisheduntil"     => "Published Until",
            "masterlanguage"     => "Master Language",
            "alllanguages"       => "All Languages",
            "referencecount"     => "Reference Count",
            "versioncount"       => "Version Count",
            "haspersonalizations"=> "Has Personalizations",
            _                    => column
        };

    private static string CsvEscape(string value)
    {
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r'))
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        return value;
    }
}
```

- [ ] **Step 2: Update the controller to use `ContentAuditExportRenderer`**

In `ContentAuditApiController.cs`:
1. Add a constructor parameter: `ContentAuditExportRenderer _renderer`
2. Replace the `ExportXlsx` / `ExportCsv` / `ExportJson` / `GetCellValue` / `GetColumnLabel` / `CsvEscape` private methods with calls to `_renderer`.

Updated constructor:
```csharp
    private readonly ContentAuditService _service;
    private readonly FeatureAccessChecker _accessChecker;
    private readonly ContentAuditExportRenderer _renderer;
    private readonly ILogger<ContentAuditApiController> _logger;

    public ContentAuditApiController(
        ContentAuditService service,
        FeatureAccessChecker accessChecker,
        ContentAuditExportRenderer renderer,
        ILogger<ContentAuditApiController> logger)
    {
        _service = service;
        _accessChecker = accessChecker;
        _renderer = renderer;
        _logger = logger;
    }
```

Replace the existing export format switch in the `Export` action:

```csharp
            var allRows = _service.GetAllMatchingRows(request, ct).ToList();

            return format.ToLowerInvariant() switch
            {
                "xlsx" => File(
                    _renderer.RenderXlsx(allRows, parsedColumns),
                    _renderer.GetContentType("xlsx"),
                    $"content-audit-{DateTime.UtcNow:yyyyMMdd-HHmmss}.xlsx"),
                "csv" => File(
                    _renderer.RenderCsv(allRows, parsedColumns),
                    _renderer.GetContentType("csv"),
                    $"content-audit-{DateTime.UtcNow:yyyyMMdd-HHmmss}.csv"),
                "json" => File(
                    _renderer.RenderJson(allRows),
                    _renderer.GetContentType("json"),
                    $"content-audit-{DateTime.UtcNow:yyyyMMdd-HHmmss}.json"),
                _ => BadRequest(new { success = false, message = $"Unsupported format: {format}" })
            };
```

Delete the private methods `ExportXlsx`, `ExportCsv`, `ExportJson`, `GetCellValue`, `GetColumnLabel`, `CsvEscape` — they now live in `ContentAuditExportRenderer`.

- [ ] **Step 3: Verify build**

```bash
dotnet build src/EditorPowertools/EditorPowertools.csproj
```

Expected: 0 errors.

- [ ] **Step 4: Commit**

```bash
git add src/EditorPowertools/Tools/ContentAudit/ContentAuditExportRenderer.cs \
        src/EditorPowertools/Tools/ContentAudit/ContentAuditApiController.cs
git commit -m "refactor(content-audit): extract ContentAuditExportRenderer from controller"
```

---

## Task 4: Implement `GetDescendentsContentAuditProvider` (Default Provider + ACL)

This class contains all logic currently in `ContentAuditService` — `BuildRow`, filtering, sorting — plus the new ACL check. It replaces the service's internal scan.

**Files:**
- Create: `src/EditorPowertools/Tools/ContentAudit/GetDescendentsContentAuditProvider.cs`

- [ ] **Step 1: Create the provider**

```csharp
using System.Globalization;
using EditorPowertools.Tools.ContentAudit.Models;
using EPiServer;
using EPiServer.Core;
using EPiServer.DataAbstraction;
using EPiServer.Editor;
using EPiServer.Security;
using EPiServer.Web;
using EPiServer.Web.Routing;
using Microsoft.Extensions.Logging;

namespace EditorPowertools.Tools.ContentAudit;

/// <summary>
/// Default IContentAuditDataProvider: walks GetDescendents lazily, one item at a time.
/// Does NOT return TotalCount — the caller should show Prev/Next instead of page numbers.
/// Applies IContentAccessEvaluator.HasAccess to skip items the current user cannot read.
/// Sorting is not supported; SortBy is ignored.
/// </summary>
public class GetDescendentsContentAuditProvider : IContentAuditDataProvider
{
    private readonly IContentRepository _contentRepository;
    private readonly IContentTypeRepository _contentTypeRepository;
    private readonly IContentVersionRepository _contentVersionRepository;
    private readonly ILanguageBranchRepository _languageBranchRepository;
    private readonly IUrlResolver _urlResolver;
    private readonly IContentAccessEvaluator _accessEvaluator;
    private readonly ILogger<GetDescendentsContentAuditProvider> _logger;

    public GetDescendentsContentAuditProvider(
        IContentRepository contentRepository,
        IContentTypeRepository contentTypeRepository,
        IContentVersionRepository contentVersionRepository,
        ILanguageBranchRepository languageBranchRepository,
        IUrlResolver urlResolver,
        IContentAccessEvaluator accessEvaluator,
        ILogger<GetDescendentsContentAuditProvider> logger)
    {
        _contentRepository = contentRepository;
        _contentTypeRepository = contentTypeRepository;
        _contentVersionRepository = contentVersionRepository;
        _languageBranchRepository = languageBranchRepository;
        _urlResolver = urlResolver;
        _accessEvaluator = accessEvaluator;
        _logger = logger;
    }

    public ContentAuditPageResult GetPage(ContentAuditRequest request, CancellationToken ct = default)
    {
        var columns = request.Columns ?? GetDefaultColumns();
        bool needsRefCount   = columns.Contains("referenceCount");
        bool needsVersions   = columns.Contains("versionCount");
        bool needsPersonaliz = columns.Contains("hasPersonalizations");

        // Enumerate refs lazily; skip to the offset needed for this page.
        // Note: this is O(offset) in the tree size — acceptable for a preview tool.
        var allRefs = _contentRepository.GetDescendents(ContentReference.RootPage);
        var principal = PrincipalInfo.CurrentPrincipal;

        var items = new List<ContentAuditRow>();
        int matchesFound = 0;
        int targetSkip  = (request.Page - 1) * request.PageSize;
        bool hasMore    = false;

        foreach (var contentRef in allRefs)
        {
            ct.ThrowIfCancellationRequested();
            if (ContentReference.IsNullOrEmpty(contentRef)) continue;

            IContent? content = TryLoad(contentRef);
            if (content == null) continue;

            // ACL check
            if (!_accessEvaluator.HasAccess(content, principal, AccessLevel.Read))
            {
                _logger.LogDebug("Content {ContentRef} skipped — no read access for {User}", contentRef, principal?.Identity?.Name);
                continue;
            }

            var row = BuildRow(content, columns, needsRefCount, needsVersions, needsPersonaliz);

            if (!MatchesRequest(row, request)) continue;

            matchesFound++;

            if (matchesFound <= targetSkip) continue;        // still in the skip zone

            if (items.Count < request.PageSize)
            {
                items.Add(row);
            }
            else
            {
                hasMore = true;
                break;   // got one extra — we know there's a next page
            }
        }

        return new ContentAuditPageResult
        {
            Items = items,
            TotalCount = null    // default provider never knows the total
        };
    }

    public IEnumerable<ContentAuditRow> GetAllRows(ContentAuditExportRequest request, CancellationToken ct = default)
    {
        var columns = request.Columns ?? GetDefaultColumns();
        bool needsRefCount   = columns.Contains("referenceCount");
        bool needsVersions   = columns.Contains("versionCount");
        bool needsPersonaliz = columns.Contains("hasPersonalizations");
        var principal = PrincipalInfo.CurrentPrincipal;

        foreach (var contentRef in _contentRepository.GetDescendents(ContentReference.RootPage))
        {
            ct.ThrowIfCancellationRequested();
            if (ContentReference.IsNullOrEmpty(contentRef)) continue;

            IContent? content = TryLoad(contentRef);
            if (content == null) continue;

            if (!_accessEvaluator.HasAccess(content, principal, AccessLevel.Read)) continue;

            var row = BuildRow(content, columns, needsRefCount, needsVersions, needsPersonaliz);
            if (!MatchesExportRequest(row, request)) continue;

            yield return row;
        }
    }

    // ---- Helpers ----

    private IContent? TryLoad(ContentReference contentRef)
    {
        try { return _contentRepository.Get<IContent>(contentRef); }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Could not load content {ContentRef}", contentRef);
            return null;
        }
    }

    private bool MatchesRequest(ContentAuditRow row, ContentAuditRequest request)
    {
        if (!string.IsNullOrEmpty(request.MainTypeFilter) &&
            !string.Equals(row.MainType, request.MainTypeFilter, StringComparison.OrdinalIgnoreCase))
            return false;

        if (!string.IsNullOrEmpty(request.QuickFilter) && !MatchesQuickFilter(row, request.QuickFilter))
            return false;

        if (!string.IsNullOrEmpty(request.Search) &&
            (row.Name == null || !row.Name.Contains(request.Search, StringComparison.OrdinalIgnoreCase)))
            return false;

        if (request.Filters is { Count: > 0 })
        {
            foreach (var f in request.Filters)
                if (!MatchesFilter(row, f)) return false;
        }

        return true;
    }

    private bool MatchesExportRequest(ContentAuditRow row, ContentAuditExportRequest request)
    {
        if (!string.IsNullOrEmpty(request.MainTypeFilter) &&
            !string.Equals(row.MainType, request.MainTypeFilter, StringComparison.OrdinalIgnoreCase))
            return false;

        if (!string.IsNullOrEmpty(request.QuickFilter) && !MatchesQuickFilter(row, request.QuickFilter))
            return false;

        if (!string.IsNullOrEmpty(request.Search) &&
            (row.Name == null || !row.Name.Contains(request.Search, StringComparison.OrdinalIgnoreCase)))
            return false;

        if (request.Filters is { Count: > 0 })
        {
            foreach (var f in request.Filters)
                if (!MatchesFilter(row, f)) return false;
        }

        return true;
    }

    private ContentAuditRow BuildRow(IContent content, List<string> columns,
        bool needsRefCount, bool needsVersionCount, bool needsPersonalizations)
    {
        var trackable    = content as IChangeTrackable;
        var versionable  = content as IVersionable;
        var localizable  = content as ILocalizable;
        var contentType  = _contentTypeRepository.Load(content.ContentTypeID);
        string mainType  = GetMainType(contentType?.ModelType);

        var row = new ContentAuditRow
        {
            ContentId = content.ContentLink.ID,
            Name      = content.Name,
            MainType  = mainType    // always set — used by filters
        };

        if (columns.Contains("language"))      row.Language      = localizable?.Language?.Name;
        if (columns.Contains("contentType"))   row.ContentType   = contentType?.LocalizedName ?? contentType?.Name;
        if (columns.Contains("mainType"))      row.MainType      = mainType;

        if (columns.Contains("url"))
        {
            try { if (content is PageData) row.Url = _urlResolver.GetUrl(content.ContentLink); }
            catch { /* URL resolution can fail for special pages */ }
        }

        if (columns.Contains("editUrl"))       row.EditUrl       = EPiServer.Editor.PageEditing.GetEditUrl(content.ContentLink);
        if (columns.Contains("breadcrumb"))    row.Breadcrumb    = BuildBreadcrumb(content);
        if (columns.Contains("status"))        row.Status        = versionable != null ? GetStatus(versionable) : "Unknown";
        if (columns.Contains("createdBy"))     row.CreatedBy     = trackable?.CreatedBy;
        if (columns.Contains("created"))       row.Created       = trackable?.Created;
        if (columns.Contains("changedBy"))     row.ChangedBy     = trackable?.ChangedBy;
        if (columns.Contains("changed"))       row.Changed       = trackable?.Changed;
        if (columns.Contains("published"))     row.Published     = versionable?.StartPublish;
        if (columns.Contains("publishedUntil"))row.PublishedUntil= versionable?.StopPublish;
        if (columns.Contains("masterLanguage"))row.MasterLanguage= localizable?.MasterLanguage?.Name;
        if (columns.Contains("allLanguages") && localizable?.ExistingLanguages != null)
            row.AllLanguages = string.Join(", ", localizable.ExistingLanguages.Select(l => l.Name));

        if (needsRefCount || columns.Contains("referenceCount"))
        {
            try { row.ReferenceCount = _contentRepository.GetReferencesToContent(content.ContentLink, false).Count(); }
            catch { row.ReferenceCount = 0; }
        }

        if (needsVersionCount || columns.Contains("versionCount"))
        {
            try { row.VersionCount = _contentVersionRepository.List(content.ContentLink).Count(); }
            catch { row.VersionCount = 0; }
        }

        if (needsPersonalizations || columns.Contains("hasPersonalizations"))
            row.HasPersonalizations = CheckHasPersonalizations(content);

        return row;
    }

    private string BuildBreadcrumb(IContent content)
    {
        var parts = new List<string>();
        try
        {
            var parentRef = content.ParentLink;
            int depth = 0;
            while (!ContentReference.IsNullOrEmpty(parentRef) && depth < 10)
            {
                var parent = _contentRepository.Get<IContent>(parentRef);
                if (parent == null) break;
                parts.Insert(0, parent.Name);
                parentRef = parent.ParentLink;
                depth++;
            }
        }
        catch { /* best-effort */ }
        return string.Join(" > ", parts);
    }

    private static string GetMainType(Type? modelType)
    {
        if (modelType == null) return "Other";
        if (typeof(PageData).IsAssignableFrom(modelType))  return "Page";
        if (typeof(MediaData).IsAssignableFrom(modelType)) return "Media";
        if (typeof(BlockData).IsAssignableFrom(modelType)) return "Block";
        return "Other";
    }

    private static string GetStatus(IVersionable versionable) =>
        versionable.Status switch
        {
            VersionStatus.Published          => "Published",
            VersionStatus.CheckedOut         => "Draft",
            VersionStatus.CheckedIn          => "Ready to Publish",
            VersionStatus.PreviouslyPublished=> "Previously Published",
            VersionStatus.DelayedPublish     => "Scheduled",
            VersionStatus.Rejected           => "Rejected",
            _                                => versionable.Status.ToString()
        };

    private static bool CheckHasPersonalizations(IContent content)
    {
        try
        {
            foreach (PropertyData prop in content.Property)
            {
                if (prop.Value is ContentArea ca &&
                    ca.FilteredItems != null &&
                    ca.FilteredItems.Any(i => i.AllowedRoles != null && i.AllowedRoles.Any()))
                    return true;
            }
        }
        catch { /* best-effort */ }
        return false;
    }

    private static bool MatchesQuickFilter(ContentAuditRow row, string quickFilter) =>
        quickFilter.ToLowerInvariant() switch
        {
            "pages"       => string.Equals(row.MainType, "Page",  StringComparison.OrdinalIgnoreCase),
            "blocks"      => string.Equals(row.MainType, "Block", StringComparison.OrdinalIgnoreCase),
            "media"       => string.Equals(row.MainType, "Media", StringComparison.OrdinalIgnoreCase),
            "unpublished" => !string.Equals(row.Status,  "Published", StringComparison.OrdinalIgnoreCase),
            "unused"      => row.ReferenceCount == 0,
            _             => true
        };

    private static bool MatchesFilter(ContentAuditRow row, ContentAuditFilter filter)
    {
        string? value = GetColumnValue(row, filter.Column);
        return filter.Operator.ToLowerInvariant() switch
        {
            "contains"    => value?.Contains(filter.Value, StringComparison.OrdinalIgnoreCase) == true,
            "equals"      => string.Equals(value, filter.Value, StringComparison.OrdinalIgnoreCase),
            "startswith"  => value?.StartsWith(filter.Value, StringComparison.OrdinalIgnoreCase) == true,
            "isempty"     => string.IsNullOrEmpty(value),
            "isnotempty"  => !string.IsNullOrEmpty(value),
            _             => true
        };
    }

    private static string? GetColumnValue(ContentAuditRow row, string column) =>
        column.ToLowerInvariant() switch
        {
            "contentid"          => row.ContentId.ToString(),
            "name"               => row.Name,
            "language"           => row.Language,
            "contenttype"        => row.ContentType,
            "maintype"           => row.MainType,
            "url"                => row.Url,
            "editurl"            => row.EditUrl,
            "breadcrumb"         => row.Breadcrumb,
            "status"             => row.Status,
            "createdby"          => row.CreatedBy,
            "created"            => row.Created?.ToString("o"),
            "changedby"          => row.ChangedBy,
            "changed"            => row.Changed?.ToString("o"),
            "published"          => row.Published?.ToString("o"),
            "publisheduntil"     => row.PublishedUntil?.ToString("o"),
            "masterlanguage"     => row.MasterLanguage,
            "alllanguages"       => row.AllLanguages,
            "referencecount"     => row.ReferenceCount?.ToString(),
            "versioncount"       => row.VersionCount?.ToString(),
            "haspersonalizations"=> row.HasPersonalizations?.ToString(),
            _                    => null
        };

    private static List<string> GetDefaultColumns() =>
    [
        "contentId", "name", "language", "contentType", "mainType",
        "status", "changedBy", "changed", "published"
    ];
}
```

- [ ] **Step 2: Verify build**

```bash
dotnet build src/EditorPowertools/EditorPowertools.csproj
```

Expected: 0 errors.

- [ ] **Step 3: Commit**

```bash
git add src/EditorPowertools/Tools/ContentAudit/GetDescendentsContentAuditProvider.cs
git commit -m "feat(content-audit): implement GetDescendentsContentAuditProvider with ACL check"
```

---

## Task 5: Slim Down `ContentAuditService`

Replace the existing implementation with a thin coordinator that delegates to `IContentAuditDataProvider`.

**Files:**
- Modify: `src/EditorPowertools/Tools/ContentAudit/ContentAuditService.cs`

- [ ] **Step 1: Replace `ContentAuditService.cs` entirely**

```csharp
using System.Text.Json;
using EditorPowertools.Tools.ContentAudit.Models;
using Microsoft.Extensions.Logging;

namespace EditorPowertools.Tools.ContentAudit;

public class ContentAuditService
{
    private readonly IContentAuditDataProvider _provider;
    private readonly ILogger<ContentAuditService> _logger;

    public ContentAuditService(IContentAuditDataProvider provider, ILogger<ContentAuditService> logger)
    {
        _provider = provider;
        _logger = logger;
    }

    public ContentAuditResponse GetContent(ContentAuditRequest request, CancellationToken ct = default)
    {
        var result = _provider.GetPage(request, ct);

        int? totalPages = result.TotalCount.HasValue
            ? (int)Math.Ceiling((double)result.TotalCount.Value / request.PageSize)
            : null;

        return new ContentAuditResponse
        {
            Items      = result.Items,
            TotalCount = result.TotalCount,
            Page       = request.Page,
            PageSize   = request.PageSize,
            TotalPages = totalPages
        };
    }

    public IEnumerable<ContentAuditRow> GetAllMatchingRows(ContentAuditExportRequest request, CancellationToken ct = default)
        => _provider.GetAllRows(request, ct);
}
```

- [ ] **Step 2: Verify build**

```bash
dotnet build src/EditorPowertools/EditorPowertools.csproj
```

Expected: 0 errors.

- [ ] **Step 3: Commit**

```bash
git add src/EditorPowertools/Tools/ContentAudit/ContentAuditService.cs
git commit -m "refactor(content-audit): slim ContentAuditService to delegate to IContentAuditDataProvider"
```

---

## Task 6: Define `ContentAuditReportMedia` CMS Content Type

The export job needs a concrete `MediaData` subclass registered with Optimizely so it can create typed content items.

**Files:**
- Create: `src/EditorPowertools/Tools/ContentAudit/ContentAuditReportMedia.cs`

- [ ] **Step 1: Create the media type**

```csharp
using EPiServer.Core;
using EPiServer.DataAnnotations;
using EPiServer.Framework.DataAnnotations;

namespace EditorPowertools.Tools.ContentAudit;

/// <summary>
/// CMS media content type used to store Content Audit export files.
/// Registered automatically by Optimizely's content type scanner.
/// Accepts XLSX, CSV, and JSON file extensions.
/// </summary>
[ContentType(
    DisplayName = "Content Audit Report",
    GUID        = "a3f1e2d4-5b6c-4e7a-8f90-1b2c3d4e5f60",
    Description = "Generated content audit export files. Do not create manually.")]
[MediaDescriptor(ExtensionString = "xlsx,csv,json")]
public class ContentAuditReportMedia : MediaData
{
}
```

- [ ] **Step 2: Verify build**

```bash
dotnet build src/EditorPowertools/EditorPowertools.csproj
```

Expected: 0 errors.

- [ ] **Step 3: Commit**

```bash
git add src/EditorPowertools/Tools/ContentAudit/ContentAuditReportMedia.cs
git commit -m "feat(content-audit): add ContentAuditReportMedia CMS content type"
```

---

## Task 7: Implement `ContentAuditExportJob`

**Files:**
- Create: `src/EditorPowertools/Tools/ContentAudit/ContentAuditExportJob.cs`

- [ ] **Step 1: Create the job**

```csharp
using System.Text.Json;
using EditorPowertools.Configuration;
using EditorPowertools.Tools.ContentAudit.Models;
using EPiServer;
using EPiServer.Core;
using EPiServer.Data.Dynamic;
using EPiServer.Framework.Blobs;
using EPiServer.PlugIn;
using EPiServer.Scheduler;
using EPiServer.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EditorPowertools.Tools.ContentAudit;

[ScheduledPlugIn(
    DisplayName    = "[EditorPowertools] Content Audit Export",
    Description    = "Generates a Content Audit export file and saves it to the CMS media library.",
    LanguagePath   = "/editorpowertools/jobs/contentauditexport",
    SortIndex      = 10001)]
public class ContentAuditExportJob : ScheduledJobBase
{
    private readonly IContentAuditDataProvider _provider;
    private readonly ContentAuditExportRenderer _renderer;
    private readonly IContentRepository _contentRepository;
    private readonly IBlobFactory _blobFactory;
    private readonly DynamicDataStoreFactory _storeFactory;
    private readonly EditorPowertoolsOptions _options;
    private readonly ILogger<ContentAuditExportJob> _logger;
    private bool _stopSignaled;

    public ContentAuditExportJob(
        IContentAuditDataProvider provider,
        ContentAuditExportRenderer renderer,
        IContentRepository contentRepository,
        IBlobFactory blobFactory,
        DynamicDataStoreFactory storeFactory,
        IOptions<EditorPowertoolsOptions> options,
        ILogger<ContentAuditExportJob> logger)
    {
        _provider        = provider;
        _renderer        = renderer;
        _contentRepository = contentRepository;
        _blobFactory     = blobFactory;
        _storeFactory    = storeFactory;
        _options         = options.Value;
        _logger          = logger;
        IsStoppable      = true;
    }

    public override void Stop() { _stopSignaled = true; base.Stop(); }

    public override string Execute()
    {
        _stopSignaled = false;
        var store = GetStore();

        // Clean up old records first (>7 days)
        var cutoff = DateTime.UtcNow.AddDays(-7);
        var old = store.Items<ContentAuditExportJobRequest>()
            .Where(r => r.CompletedAt.HasValue && r.CompletedAt < cutoff)
            .ToList();
        foreach (var o in old) store.Delete(o.Id);

        // Get all pending requests
        var pending = store.Items<ContentAuditExportJobRequest>()
            .Where(r => r.Status == "Pending")
            .OrderBy(r => r.RequestedAt)
            .ToList();

        if (pending.Count == 0)
            return "No pending export requests.";

        var folderRef = EnsureReportFolder();
        int processed = 0;

        foreach (var jobRequest in pending)
        {
            if (_stopSignaled) break;

            jobRequest.Status = "Running";
            store.Save(jobRequest);

            try
            {
                ProcessRequest(jobRequest, folderRef, store);
                processed++;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Content audit export failed for request {RequestId}", jobRequest.RequestId);
                jobRequest.Status       = "Failed";
                jobRequest.ErrorMessage = "Export failed. Check server logs for details.";
                jobRequest.CompletedAt  = DateTime.UtcNow;
                store.Save(jobRequest);
            }
        }

        return $"Processed {processed} export request(s).";
    }

    private void ProcessRequest(ContentAuditExportJobRequest jobRequest, ContentReference folderRef, DynamicDataStore store)
    {
        var exportRequest = BuildExportRequest(jobRequest);
        var ct = _stopSignaled ? new CancellationToken(true) : CancellationToken.None;
        var rows = _provider.GetAllRows(exportRequest, ct);

        string ext    = _renderer.GetExtension(jobRequest.Format);
        string mime   = _renderer.GetContentType(jobRequest.Format);
        string name   = $"content-audit-{DateTime.UtcNow:yyyyMMdd-HHmmss}{ext}";

        // Create the CMS media item
        var media = _contentRepository.GetDefault<ContentAuditReportMedia>(folderRef);
        media.Name = name;

        // Write blob
        var blob = _blobFactory.CreateBlob(media.ContentLink, ext);
        using (var stream = blob.OpenWrite())
        {
            byte[] bytes = jobRequest.Format.ToLowerInvariant() switch
            {
                "xlsx" => _renderer.RenderXlsx(rows, exportRequest.Columns ?? GetAllColumns()),
                "csv"  => _renderer.RenderCsv(rows,  exportRequest.Columns ?? GetAllColumns()),
                "json" => _renderer.RenderJson(rows),
                _      => _renderer.RenderXlsx(rows, exportRequest.Columns ?? GetAllColumns())
            };
            stream.Write(bytes, 0, bytes.Length);
        }

        media.BinaryData = blob;
        var savedRef = _contentRepository.Save(media, EPiServer.DataAbstraction.SaveAction.Publish, AccessLevel.NoAccess);

        jobRequest.Status          = "Completed";
        jobRequest.ResultContentId = savedRef.ID.ToString();
        jobRequest.CompletedAt     = DateTime.UtcNow;
        store.Save(jobRequest);
    }

    private ContentAuditExportRequest BuildExportRequest(ContentAuditExportJobRequest jobRequest)
    {
        List<ContentAuditFilter>? filters = null;
        if (!string.IsNullOrEmpty(jobRequest.FiltersJson))
        {
            try { filters = JsonSerializer.Deserialize<List<ContentAuditFilter>>(jobRequest.FiltersJson); }
            catch { /* ignore malformed filters */ }
        }

        List<string>? columns = string.IsNullOrEmpty(jobRequest.Columns)
            ? null
            : jobRequest.Columns.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();

        return new ContentAuditExportRequest
        {
            Format         = jobRequest.Format,
            Columns        = columns,
            MainTypeFilter = jobRequest.MainTypeFilter,
            QuickFilter    = jobRequest.QuickFilter,
            Search         = jobRequest.Search,
            Filters        = filters
        };
    }

    private ContentReference EnsureReportFolder()
    {
        var folderName = _options.ContentAudit.ReportFolderName;
        var globalAssets = ContentReference.GlobalAssetsRoot;

        var existing = _contentRepository
            .GetChildren<ContentFolder>(globalAssets)
            .FirstOrDefault(f => string.Equals(f.Name, folderName, StringComparison.OrdinalIgnoreCase));

        if (existing != null)
            return existing.ContentLink;

        var newFolder = _contentRepository.GetDefault<ContentFolder>(globalAssets);
        newFolder.Name = folderName;
        return _contentRepository.Save(newFolder, EPiServer.DataAbstraction.SaveAction.Publish, AccessLevel.NoAccess);
    }

    private DynamicDataStore GetStore() =>
        _storeFactory.GetStore(typeof(ContentAuditExportJobRequest))
        ?? _storeFactory.CreateStore(typeof(ContentAuditExportJobRequest));

    private static List<string> GetAllColumns() =>
    [
        "contentId", "name", "language", "contentType", "mainType",
        "url", "editUrl", "breadcrumb", "status",
        "createdBy", "created", "changedBy", "changed",
        "published", "publishedUntil",
        "masterLanguage", "allLanguages",
        "referenceCount", "versionCount", "hasPersonalizations"
    ];
}
```

- [ ] **Step 2: Verify build**

```bash
dotnet build src/EditorPowertools/EditorPowertools.csproj
```

Expected: 0 errors.

- [ ] **Step 3: Commit**

```bash
git add src/EditorPowertools/Tools/ContentAudit/ContentAuditExportJob.cs
git commit -m "feat(content-audit): add ContentAuditExportJob scheduled job with blob storage output"
```

---

## Task 8: Add Export Endpoints to `ContentAuditApiController`

Add `POST /export-request` (triggers job) and `GET /export-status` (polls DDS for completion).

**Files:**
- Modify: `src/EditorPowertools/Tools/ContentAudit/ContentAuditApiController.cs`

- [ ] **Step 1: Add new dependencies to constructor**

Add `IScheduledJobRepository`, `IScheduledJobExecutor`, and `DynamicDataStoreFactory` to the constructor. Also add `using` directives for `EPiServer.DataAbstraction`, `EPiServer.Scheduler`, `EPiServer.Data.Dynamic`.

Updated constructor fields and signature:
```csharp
    private readonly ContentAuditService _service;
    private readonly FeatureAccessChecker _accessChecker;
    private readonly ContentAuditExportRenderer _renderer;
    private readonly IScheduledJobRepository _jobRepository;
    private readonly IScheduledJobExecutor _jobExecutor;
    private readonly DynamicDataStoreFactory _storeFactory;
    private readonly ILogger<ContentAuditApiController> _logger;

    public ContentAuditApiController(
        ContentAuditService service,
        FeatureAccessChecker accessChecker,
        ContentAuditExportRenderer renderer,
        IScheduledJobRepository jobRepository,
        IScheduledJobExecutor jobExecutor,
        DynamicDataStoreFactory storeFactory,
        ILogger<ContentAuditApiController> logger)
    {
        _service       = service;
        _accessChecker = accessChecker;
        _renderer      = renderer;
        _jobRepository = jobRepository;
        _jobExecutor   = jobExecutor;
        _storeFactory  = storeFactory;
        _logger        = logger;
    }
```

- [ ] **Step 2: Add `POST /export-request` action**

Add after the existing `Export` action:

```csharp
    /// <summary>
    /// Saves an export request to DDS and triggers the ContentAuditExportJob.
    /// Returns a requestId the client polls with /export-status.
    /// </summary>
    [HttpPost("export-request")]
    [RequireAjax]
    public async Task<IActionResult> RequestExport(
        [FromQuery] string format = "xlsx",
        [FromQuery] string? columns = null,
        [FromQuery] string? mainTypeFilter = null,
        [FromQuery] string? quickFilter = null,
        [FromQuery] string? search = null,
        [FromQuery] string? filters = null)
    {
        if (!_accessChecker.HasAccess(HttpContext,
                nameof(Configuration.FeatureToggles.ContentAudit),
                EditorPowertoolsPermissions.ContentAudit))
            return Forbid();

        try
        {
            var requestId = Guid.NewGuid();
            var record = new ContentAuditExportJobRequest
            {
                RequestId      = requestId,
                RequestedBy    = User.Identity?.Name ?? "unknown",
                RequestedAt    = DateTime.UtcNow,
                Format         = format,
                Columns        = columns,
                MainTypeFilter = mainTypeFilter,
                QuickFilter    = quickFilter,
                Search         = search,
                FiltersJson    = filters,
                Status         = "Pending"
            };

            var store = GetStore();
            store.Save(record);

            // Trigger the job (best-effort — job may already be running)
            var job = _jobRepository.List()
                .FirstOrDefault(j => j.TypeName?.Contains("ContentAuditExportJob", StringComparison.OrdinalIgnoreCase) == true);

            if (job != null && !job.IsRunning)
                await _jobExecutor.StartAsync(job);

            return Ok(new { success = true, requestId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to queue content audit export");
            return StatusCode(500, new { success = false, message = "Failed to queue export." });
        }
    }

    /// <summary>
    /// Returns the status of an export request. When Status == "Completed",
    /// includes the ContentLink ID so the client can construct a download URL.
    /// </summary>
    [HttpGet("export-status")]
    public IActionResult GetExportStatus([FromQuery] Guid requestId)
    {
        if (!_accessChecker.HasAccess(HttpContext,
                nameof(Configuration.FeatureToggles.ContentAudit),
                EditorPowertoolsPermissions.ContentAudit))
            return Forbid();

        try
        {
            var store  = GetStore();
            var record = store.Items<ContentAuditExportJobRequest>()
                .FirstOrDefault(r => r.RequestId == requestId);

            if (record == null)
                return NotFound(new { success = false, message = "Export request not found." });

            return Ok(new
            {
                success         = true,
                status          = record.Status,
                resultContentId = record.ResultContentId,
                errorMessage    = record.ErrorMessage,
                completedAt     = record.CompletedAt
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get export status for {RequestId}", requestId);
            return StatusCode(500, new { success = false, message = "Failed to get export status." });
        }
    }

    private DynamicDataStore GetStore() =>
        _storeFactory.GetStore(typeof(ContentAuditExportJobRequest))
        ?? _storeFactory.CreateStore(typeof(ContentAuditExportJobRequest));
```

- [ ] **Step 3: Verify build**

```bash
dotnet build src/EditorPowertools/EditorPowertools.csproj
```

Expected: 0 errors.

- [ ] **Step 4: Commit**

```bash
git add src/EditorPowertools/Tools/ContentAudit/ContentAuditApiController.cs
git commit -m "feat(content-audit): add export-request and export-status API endpoints"
```

---

## Task 9: Register New Types in DI

**Files:**
- Modify: `src/EditorPowertools/Infrastructure/ServiceCollectionExtensions.cs`

- [ ] **Step 1: Add registrations**

Locate the `ContentAuditService` registration (currently line 128) and add the new types after it:

```csharp
        services.AddTransient<ContentAuditService>();
        services.AddTransient<IContentAuditDataProvider, GetDescendentsContentAuditProvider>();
        services.AddTransient<ContentAuditExportRenderer>();
```

- [ ] **Step 2: Verify build**

```bash
dotnet build src/EditorPowertools/EditorPowertools.csproj
```

Expected: 0 errors.

- [ ] **Step 3: Commit**

```bash
git add src/EditorPowertools/Infrastructure/ServiceCollectionExtensions.cs
git commit -m "feat(content-audit): register IContentAuditDataProvider and ContentAuditExportRenderer in DI"
```

---

## Task 10: Update `content-audit.js` for Pagination and Async Export

**Files:**
- Modify: `src/EditorPowertools/modules/_protected/EditorPowertools/ClientResources/js/content-audit.js`

Read the full file before editing — it is large and the pagination and export sections must be located precisely.

- [ ] **Step 1: Locate and update the `renderPagination` function**

Find the function that renders pagination controls (look for `totalPages`, `page`, and pagination button elements). Replace or modify it so that when `totalPages` is null/absent, it renders Prev/Next only with a note:

```javascript
    function renderPagination(data) {
        var pager = document.getElementById('ept-audit-pager');
        if (!pager) return;

        var hasTotalPages = data.totalPages != null;
        var canPrev = data.page > 1;
        var canNext = hasTotalPages ? (data.page < data.totalPages) : (data.items && data.items.length === state.pageSize);

        var html = '';
        if (hasTotalPages) {
            // Numbered pagination
            html += '<span class="ept-pager-info">' + EPT.s('contentaudit.page_info', 'Page {0} of {1}')
                .replace('{0}', data.page).replace('{1}', data.totalPages) + '</span>';
        } else {
            // Prev/Next only — provider doesn't know total
            html += '<span class="ept-pager-info ept-pager-info--preview">'
                + EPT.s('contentaudit.page_preview', 'Preview — page {0}').replace('{0}', data.page) + '</span>';
        }

        html += '<button class="ept-btn ept-btn--sm" id="ept-prev-btn" ' + (canPrev ? '' : 'disabled') + '>'
            + EPT.s('contentaudit.btn_prev', 'Previous') + '</button>';
        html += '<button class="ept-btn ept-btn--sm" id="ept-next-btn" ' + (canNext ? '' : 'disabled') + '>'
            + EPT.s('contentaudit.btn_next', 'Next') + '</button>';

        pager.innerHTML = html;

        var prevBtn = document.getElementById('ept-prev-btn');
        var nextBtn = document.getElementById('ept-next-btn');
        if (prevBtn) prevBtn.addEventListener('click', function() { state.page--; loadData(); });
        if (nextBtn) nextBtn.addEventListener('click', function() { state.page++; loadData(); });
    }
```

- [ ] **Step 2: Add a preview-mode banner**

After the table renders (in the function that populates the results), add a banner when `totalPages` is null:

```javascript
    function renderPreviewBanner(data) {
        var existing = document.getElementById('ept-preview-banner');
        if (data.totalPages != null) {
            if (existing) existing.remove();
            return;
        }
        if (!existing) {
            existing = document.createElement('div');
            existing.id = 'ept-preview-banner';
            existing.className = 'ept-alert ept-alert--info';
            var table = document.getElementById('ept-audit-table');
            if (table) table.parentNode.insertBefore(existing, table);
        }
        existing.textContent = EPT.s('contentaudit.preview_notice',
            'Showing a preview of content. Run a full export for complete results.');
    }
```

Call `renderPreviewBanner(data)` wherever you call `renderPagination(data)`.

- [ ] **Step 3: Replace the inline export handler with async job flow**

Find the export button click handler (look for `export`, `ExportXlsx`, or `/content-audit/export`). Replace with:

```javascript
    function bindExportButton() {
        var btn = document.getElementById('ept-export-btn');
        if (!btn) return;
        btn.addEventListener('click', async function() {
            var format = document.getElementById('ept-export-format')?.value || 'xlsx';
            var params = buildQueryParams(state);
            params.set('format', format);

            btn.disabled = true;
            btn.textContent = EPT.s('contentaudit.export_queuing', 'Queuing export...');

            try {
                var result = await EPT.postJson(API + '/export-request?' + params.toString());
                if (!result.success) throw new Error(result.message || 'Failed');

                var requestId = result.requestId;
                btn.textContent = EPT.s('contentaudit.export_running', 'Export running...');

                // Poll every 3 seconds
                var pollInterval = setInterval(async function() {
                    try {
                        var status = await EPT.fetchJson(API + '/export-status?requestId=' + requestId);
                        if (status.status === 'Completed') {
                            clearInterval(pollInterval);
                            btn.disabled = false;
                            btn.textContent = EPT.s('contentaudit.export_btn', 'Export');
                            showExportDownloadLink(status.resultContentId);
                        } else if (status.status === 'Failed') {
                            clearInterval(pollInterval);
                            btn.disabled = false;
                            btn.textContent = EPT.s('contentaudit.export_btn', 'Export');
                            showError(EPT.s('contentaudit.export_failed', 'Export failed. Check server logs.'));
                        }
                    } catch (e) {
                        clearInterval(pollInterval);
                        btn.disabled = false;
                        btn.textContent = EPT.s('contentaudit.export_btn', 'Export');
                    }
                }, 3000);
            } catch (err) {
                btn.disabled = false;
                btn.textContent = EPT.s('contentaudit.export_btn', 'Export');
                showError(EPT.s('contentaudit.export_queue_failed', 'Could not queue export. Try again.'));
            }
        });
    }

    function showExportDownloadLink(contentId) {
        var el = document.getElementById('ept-export-result');
        if (!el) {
            el = document.createElement('div');
            el.id = 'ept-export-result';
            el.className = 'ept-alert ept-alert--success';
            var exportArea = document.getElementById('ept-export-area');
            if (exportArea) exportArea.appendChild(el);
        }
        // Link into CMS assets view — admin can find the file under "Internal Reports"
        el.innerHTML = '✅ <strong>' + EPT.s('contentaudit.export_ready', 'Export complete.') + '</strong> '
            + EPT.s('contentaudit.export_find_in_assets', 'Find your file in the CMS Assets panel under "Internal Reports".');
    }
```

- [ ] **Step 4: Verify JS loads without errors on the sample site**

Run the sample site:
```bash
dotnet run --project src/EditorPowertools.SampleSite
```

Navigate to the Content Audit page. Open browser DevTools → Console. Expected: no JavaScript errors. Pagination shows "Preview — page 1" with Prev/Next buttons. Export button queues a job and shows a success message when done.

- [ ] **Step 5: Commit**

```bash
git add "src/EditorPowertools/modules/_protected/EditorPowertools/ClientResources/js/content-audit.js"
git commit -m "feat(content-audit): Prev/Next pagination and async job-based export in JS"
```

---

## Task 11: Smoke Test on Sample Site

- [ ] **Step 1: Run the sample site**

```bash
dotnet run --project src/EditorPowertools.SampleSite
```

- [ ] **Step 2: Verify ACL filtering**

Log in as a user WITHOUT admin rights but with EPiServer access. Open Content Audit. Verify items that user cannot read do not appear in results.

- [ ] **Step 3: Verify pagination**

Page 1 loads correctly. "Preview — page 1" banner is visible. Next button loads page 2. Content changes with each page.

- [ ] **Step 4: Verify export job**

Click Export → XLSX. Poll completes. Navigate to CMS Assets → "Internal Reports" folder. An XLSX file is present. Download and verify it has content rows.

- [ ] **Step 5: Verify the export job appears in Scheduled Jobs**

Navigate to CMS Admin → Scheduled Jobs. Confirm "[EditorPowertools] Content Audit Export" is listed.
