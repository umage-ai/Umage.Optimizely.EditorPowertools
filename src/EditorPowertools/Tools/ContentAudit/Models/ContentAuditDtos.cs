namespace EditorPowertools.Tools.ContentAudit.Models;

public class ContentAuditRequest
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 50;
    public string? SortBy { get; set; }
    public string SortDirection { get; set; } = "asc";
    public string? Search { get; set; }
    public List<ContentAuditFilter>? Filters { get; set; }
    public string? MainTypeFilter { get; set; }
    public string? QuickFilter { get; set; }
    public List<string>? Columns { get; set; }
}

public class ContentAuditFilter
{
    public string Column { get; set; } = "";
    public string Operator { get; set; } = "contains";
    public string Value { get; set; } = "";
}

public class ContentAuditResponse
{
    public List<ContentAuditRow> Items { get; set; } = [];
    public int? TotalCount { get; set; }    // null when provider cannot determine
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int? TotalPages { get; set; }    // null when TotalCount is null
}

public class ContentAuditRow
{
    public int ContentId { get; set; }
    public string Name { get; set; } = "";
    public string? Language { get; set; }
    public string? ContentType { get; set; }
    public string? MainType { get; set; }
    public string? Url { get; set; }
    public string? EditUrl { get; set; }
    public string? Breadcrumb { get; set; }
    public string? Status { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime? Created { get; set; }
    public string? ChangedBy { get; set; }
    public DateTime? Changed { get; set; }
    public DateTime? Published { get; set; }
    public DateTime? PublishedUntil { get; set; }
    public string? MasterLanguage { get; set; }
    public string? AllLanguages { get; set; }
    public int? ReferenceCount { get; set; }
    public int? VersionCount { get; set; }
    public bool? HasPersonalizations { get; set; }
}

public class ContentAuditExportRequest
{
    public string Format { get; set; } = "xlsx";
    public string? Search { get; set; }
    public List<ContentAuditFilter>? Filters { get; set; }
    public string? MainTypeFilter { get; set; }
    public string? QuickFilter { get; set; }
    public string? SortBy { get; set; }
    public string SortDirection { get; set; } = "asc";
    public List<string>? Columns { get; set; }
}

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
