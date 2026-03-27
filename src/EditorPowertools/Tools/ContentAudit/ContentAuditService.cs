using System.Globalization;
using EditorPowertools.Tools.ContentAudit.Models;
using EPiServer;
using EPiServer.Core;
using EPiServer.DataAbstraction;
using EPiServer.Editor;
using EPiServer.Web;
using EPiServer.Web.Routing;
using Microsoft.Extensions.Logging;

namespace EditorPowertools.Tools.ContentAudit;

public class ContentAuditService
{
    private readonly IContentRepository _contentRepository;
    private readonly IContentTypeRepository _contentTypeRepository;
    private readonly IContentVersionRepository _contentVersionRepository;
    private readonly ILanguageBranchRepository _languageBranchRepository;
    private readonly IUrlResolver _urlResolver;
    private readonly ILogger<ContentAuditService> _logger;

    public ContentAuditService(
        IContentRepository contentRepository,
        IContentTypeRepository contentTypeRepository,
        IContentVersionRepository contentVersionRepository,
        ILanguageBranchRepository languageBranchRepository,
        IUrlResolver urlResolver,
        ILogger<ContentAuditService> logger)
    {
        _contentRepository = contentRepository;
        _contentTypeRepository = contentTypeRepository;
        _contentVersionRepository = contentVersionRepository;
        _languageBranchRepository = languageBranchRepository;
        _urlResolver = urlResolver;
        _logger = logger;
    }

    public ContentAuditResponse GetContent(ContentAuditRequest request, CancellationToken ct = default)
    {
        var allDescendents = _contentRepository.GetDescendents(ContentReference.RootPage);
        var requestedColumns = request.Columns ?? GetDefaultColumns();

        bool needsRefCount = requestedColumns.Contains("referenceCount");
        bool needsVersionCount = requestedColumns.Contains("versionCount");
        bool needsPersonalizations = requestedColumns.Contains("hasPersonalizations");
        bool isUnusedFilter = string.Equals(request.QuickFilter, "unused", StringComparison.OrdinalIgnoreCase);

        var rows = new List<ContentAuditRow>();
        int skipped = 0;

        foreach (ContentReference contentRef in allDescendents)
        {
            ct.ThrowIfCancellationRequested();

            if (ContentReference.IsNullOrEmpty(contentRef))
                continue;

            IContent? content;
            try
            {
                content = _contentRepository.Get<IContent>(contentRef);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Could not load content {ContentRef}", contentRef);
                continue;
            }

            if (content == null)
                continue;

            ContentAuditRow row = BuildRow(content, requestedColumns, needsRefCount, needsVersionCount, needsPersonalizations);

            // Apply main type filter
            if (!string.IsNullOrEmpty(request.MainTypeFilter))
            {
                if (!string.Equals(row.MainType, request.MainTypeFilter, StringComparison.OrdinalIgnoreCase))
                    continue;
            }

            // Apply quick filter
            if (!string.IsNullOrEmpty(request.QuickFilter))
            {
                if (!MatchesQuickFilter(row, request.QuickFilter))
                    continue;
            }

            // Apply search
            if (!string.IsNullOrEmpty(request.Search))
            {
                if (row.Name == null || !row.Name.Contains(request.Search, StringComparison.OrdinalIgnoreCase))
                    continue;
            }

            // Apply column filters
            if (request.Filters is { Count: > 0 })
            {
                bool matchesAll = true;
                foreach (var filter in request.Filters)
                {
                    if (!MatchesFilter(row, filter))
                    {
                        matchesAll = false;
                        break;
                    }
                }
                if (!matchesAll) continue;
            }

            rows.Add(row);
        }

        int totalCount = rows.Count;

        // Sort
        if (!string.IsNullOrEmpty(request.SortBy))
        {
            rows = ApplySorting(rows, request.SortBy, request.SortDirection);
        }

        // Page
        int totalPages = (int)Math.Ceiling((double)totalCount / request.PageSize);
        var pagedRows = rows
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToList();

        return new ContentAuditResponse
        {
            Items = pagedRows,
            TotalCount = totalCount,
            Page = request.Page,
            PageSize = request.PageSize,
            TotalPages = totalPages
        };
    }

    /// <summary>
    /// Yields all matching rows one by one for streaming export.
    /// </summary>
    public IEnumerable<ContentAuditRow> GetAllMatchingRows(ContentAuditExportRequest request, CancellationToken ct = default)
    {
        var allDescendents = _contentRepository.GetDescendents(ContentReference.RootPage);
        var requestedColumns = request.Columns ?? GetDefaultColumns();

        bool needsRefCount = requestedColumns.Contains("referenceCount");
        bool needsVersionCount = requestedColumns.Contains("versionCount");
        bool needsPersonalizations = requestedColumns.Contains("hasPersonalizations");

        foreach (ContentReference contentRef in allDescendents)
        {
            ct.ThrowIfCancellationRequested();

            if (ContentReference.IsNullOrEmpty(contentRef))
                continue;

            IContent? content;
            try
            {
                content = _contentRepository.Get<IContent>(contentRef);
            }
            catch
            {
                continue;
            }

            if (content == null)
                continue;

            ContentAuditRow row = BuildRow(content, requestedColumns, needsRefCount, needsVersionCount, needsPersonalizations);

            // Apply main type filter
            if (!string.IsNullOrEmpty(request.MainTypeFilter))
            {
                if (!string.Equals(row.MainType, request.MainTypeFilter, StringComparison.OrdinalIgnoreCase))
                    continue;
            }

            // Apply quick filter
            if (!string.IsNullOrEmpty(request.QuickFilter))
            {
                if (!MatchesQuickFilter(row, request.QuickFilter))
                    continue;
            }

            // Apply search
            if (!string.IsNullOrEmpty(request.Search))
            {
                if (row.Name == null || !row.Name.Contains(request.Search, StringComparison.OrdinalIgnoreCase))
                    continue;
            }

            // Apply column filters
            if (request.Filters is { Count: > 0 })
            {
                bool matchesAll = true;
                foreach (var filter in request.Filters)
                {
                    if (!MatchesFilter(row, filter))
                    {
                        matchesAll = false;
                        break;
                    }
                }
                if (!matchesAll) continue;
            }

            yield return row;
        }
    }

    private ContentAuditRow BuildRow(IContent content, List<string> columns, bool needsRefCount, bool needsVersionCount, bool needsPersonalizations)
    {
        var trackable = content as IChangeTrackable;
        var versionable = content as IVersionable;
        var localizable = content as ILocalizable;
        var contentType = _contentTypeRepository.Load(content.ContentTypeID);

        var row = new ContentAuditRow
        {
            ContentId = content.ContentLink.ID,
            Name = content.Name
        };

        // Always populate mainType since it's used for filtering
        string mainType = GetMainType(contentType?.ModelType);
        row.MainType = mainType;

        if (columns.Contains("language"))
            row.Language = localizable?.Language?.Name;

        if (columns.Contains("contentType"))
            row.ContentType = contentType?.LocalizedName ?? contentType?.Name;

        if (columns.Contains("mainType"))
            row.MainType = mainType;

        if (columns.Contains("url"))
        {
            try
            {
                if (content is PageData)
                {
                    row.Url = _urlResolver.GetUrl(content.ContentLink);
                }
            }
            catch
            {
                // URL resolution can fail for special pages
            }
        }

        if (columns.Contains("editUrl"))
        {
            row.EditUrl = PageEditing.GetEditUrl(content.ContentLink);
        }

        if (columns.Contains("breadcrumb"))
        {
            row.Breadcrumb = BuildBreadcrumb(content);
        }

        if (columns.Contains("status"))
        {
            row.Status = versionable != null ? GetStatus(versionable) : "Unknown";
        }

        if (columns.Contains("createdBy"))
            row.CreatedBy = trackable?.CreatedBy;

        if (columns.Contains("created"))
            row.Created = trackable?.Created;

        if (columns.Contains("changedBy"))
            row.ChangedBy = trackable?.ChangedBy;

        if (columns.Contains("changed"))
            row.Changed = trackable?.Changed;

        if (columns.Contains("published"))
            row.Published = versionable?.StartPublish;

        if (columns.Contains("publishedUntil"))
            row.PublishedUntil = versionable?.StopPublish;

        if (columns.Contains("masterLanguage"))
            row.MasterLanguage = localizable?.MasterLanguage?.Name;

        if (columns.Contains("allLanguages"))
        {
            if (localizable?.ExistingLanguages != null)
            {
                row.AllLanguages = string.Join(", ",
                    localizable.ExistingLanguages.Select(l => l.Name));
            }
        }

        if (needsRefCount || columns.Contains("referenceCount"))
        {
            try
            {
                var refs = _contentRepository.GetReferencesToContent(content.ContentLink, false);
                row.ReferenceCount = refs.Count();
            }
            catch
            {
                row.ReferenceCount = 0;
            }
        }

        if (needsVersionCount || columns.Contains("versionCount"))
        {
            try
            {
                var versions = _contentVersionRepository.List(content.ContentLink);
                row.VersionCount = versions.Count();
            }
            catch
            {
                row.VersionCount = 0;
            }
        }

        if (needsPersonalizations || columns.Contains("hasPersonalizations"))
        {
            row.HasPersonalizations = CheckHasPersonalizations(content);
        }

        return row;
    }

    private string BuildBreadcrumb(IContent content)
    {
        var parts = new List<string>();
        try
        {
            ContentReference? parentRef = content.ParentLink;
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
        catch
        {
            // Breadcrumb is best-effort
        }

        return string.Join(" > ", parts);
    }

    private static string GetMainType(Type? modelType)
    {
        if (modelType == null) return "Other";
        if (typeof(PageData).IsAssignableFrom(modelType)) return "Page";
        if (typeof(MediaData).IsAssignableFrom(modelType)) return "Media";
        if (typeof(BlockData).IsAssignableFrom(modelType)) return "Block";
        return "Other";
    }

    private static string GetStatus(IVersionable versionable)
    {
        return versionable.Status switch
        {
            VersionStatus.Published => "Published",
            VersionStatus.CheckedOut => "Draft",
            VersionStatus.CheckedIn => "Ready to Publish",
            VersionStatus.PreviouslyPublished => "Previously Published",
            VersionStatus.DelayedPublish => "Scheduled",
            VersionStatus.Rejected => "Rejected",
            _ => versionable.Status.ToString()
        };
    }

    private bool CheckHasPersonalizations(IContent content)
    {
        try
        {
            foreach (PropertyData prop in content.Property)
            {
                if (prop.Value is ContentArea contentArea)
                {
                    if (contentArea.FilteredItems != null &&
                        contentArea.FilteredItems.Any(item =>
                            item.AllowedRoles != null && item.AllowedRoles.Any()))
                    {
                        return true;
                    }
                }
            }
        }
        catch
        {
            // Best effort
        }
        return false;
    }

    private static bool MatchesQuickFilter(ContentAuditRow row, string quickFilter)
    {
        return quickFilter.ToLowerInvariant() switch
        {
            "pages" => string.Equals(row.MainType, "Page", StringComparison.OrdinalIgnoreCase),
            "blocks" => string.Equals(row.MainType, "Block", StringComparison.OrdinalIgnoreCase),
            "media" => string.Equals(row.MainType, "Media", StringComparison.OrdinalIgnoreCase),
            "unpublished" => !string.Equals(row.Status, "Published", StringComparison.OrdinalIgnoreCase),
            "unused" => row.ReferenceCount == 0,
            _ => true
        };
    }

    private static bool MatchesFilter(ContentAuditRow row, ContentAuditFilter filter)
    {
        string? value = GetColumnValue(row, filter.Column);

        return filter.Operator.ToLowerInvariant() switch
        {
            "contains" => value?.Contains(filter.Value, StringComparison.OrdinalIgnoreCase) == true,
            "equals" => string.Equals(value, filter.Value, StringComparison.OrdinalIgnoreCase),
            "startswith" => value?.StartsWith(filter.Value, StringComparison.OrdinalIgnoreCase) == true,
            "isempty" => string.IsNullOrEmpty(value),
            "isnotempty" => !string.IsNullOrEmpty(value),
            _ => true
        };
    }

    private static string? GetColumnValue(ContentAuditRow row, string column)
    {
        return column.ToLowerInvariant() switch
        {
            "contentid" => row.ContentId.ToString(),
            "name" => row.Name,
            "language" => row.Language,
            "contenttype" => row.ContentType,
            "maintype" => row.MainType,
            "url" => row.Url,
            "editurl" => row.EditUrl,
            "breadcrumb" => row.Breadcrumb,
            "status" => row.Status,
            "createdby" => row.CreatedBy,
            "created" => row.Created?.ToString("o"),
            "changedby" => row.ChangedBy,
            "changed" => row.Changed?.ToString("o"),
            "published" => row.Published?.ToString("o"),
            "publisheduntil" => row.PublishedUntil?.ToString("o"),
            "masterlanguage" => row.MasterLanguage,
            "alllanguages" => row.AllLanguages,
            "referencecount" => row.ReferenceCount?.ToString(),
            "versioncount" => row.VersionCount?.ToString(),
            "haspersonalizations" => row.HasPersonalizations?.ToString(),
            _ => null
        };
    }

    private static List<ContentAuditRow> ApplySorting(List<ContentAuditRow> rows, string sortBy, string direction)
    {
        bool desc = string.Equals(direction, "desc", StringComparison.OrdinalIgnoreCase);

        // Numeric columns
        if (sortBy is "contentId" or "referenceCount" or "versionCount")
        {
            int GetNum(ContentAuditRow r) => sortBy switch
            {
                "contentId" => r.ContentId,
                "referenceCount" => r.ReferenceCount ?? 0,
                "versionCount" => r.VersionCount ?? 0,
                _ => 0
            };

            return desc
                ? rows.OrderByDescending(GetNum).ToList()
                : rows.OrderBy(GetNum).ToList();
        }

        // Date columns
        if (sortBy is "created" or "changed" or "published" or "publishedUntil")
        {
            DateTime? GetDate(ContentAuditRow r) => sortBy switch
            {
                "created" => r.Created,
                "changed" => r.Changed,
                "published" => r.Published,
                "publishedUntil" => r.PublishedUntil,
                _ => null
            };

            return desc
                ? rows.OrderByDescending(GetDate).ToList()
                : rows.OrderBy(GetDate).ToList();
        }

        // Bool columns
        if (sortBy == "hasPersonalizations")
        {
            return desc
                ? rows.OrderByDescending(r => r.HasPersonalizations == true).ToList()
                : rows.OrderBy(r => r.HasPersonalizations == true).ToList();
        }

        // String columns (default)
        string GetStr(ContentAuditRow r) => GetColumnValue(r, sortBy) ?? "";

        return desc
            ? rows.OrderByDescending(GetStr, StringComparer.OrdinalIgnoreCase).ToList()
            : rows.OrderBy(GetStr, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static List<string> GetDefaultColumns()
    {
        return
        [
            "contentId", "name", "language", "contentType", "mainType",
            "status", "changedBy", "changed", "published"
        ];
    }
}
