using UmageAI.Optimizely.EditorPowerTools.Tools.ContentAudit.Models;
using EPiServer;
using EPiServer.Core;
using EPiServer.DataAbstraction;
using EPiServer.Editor;
using EPiServer.Security;
using EPiServer.Web.Routing;
using Microsoft.Extensions.Logging;

namespace UmageAI.Optimizely.EditorPowerTools.Tools.ContentAudit;

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

            if (matchesFound <= targetSkip) continue;  // still in the skip zone

            if (items.Count < request.PageSize)
            {
                items.Add(row);
            }
            else
            {
                break;  // got a full page — stop scanning
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

        if (columns.Contains("editUrl"))       row.EditUrl       = PageEditing.GetEditUrl(content.ContentLink);
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
