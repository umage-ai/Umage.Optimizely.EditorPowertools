namespace UmageAI.Optimizely.EditorPowerTools.Tools.SecurityAudit.Models;

/// <summary>
/// Deserialized from AclEntriesJson for API responses.
/// </summary>
public class AclEntryDto
{
    public string Name { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty; // "Role", "User", "VisitorGroup"
    public string Access { get; set; } = string.Empty;     // "Read", "Edit", "Publish", "FullAccess", etc.
}

/// <summary>
/// Returned by the tree endpoints. Matches the ept-tree UI component pattern.
/// </summary>
public class ContentPermissionNodeDto
{
    public int ContentId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? ContentTypeName { get; set; }
    public string? Breadcrumb { get; set; }
    public bool IsPage { get; set; }
    public bool HasChildren { get; set; }

    // Permissions summary
    public List<AclEntryDto> Entries { get; set; } = new();
    public bool IsInheriting { get; set; }
    public bool HasExplicitAcl { get; set; }

    // Issues
    public bool HasNoRestrictions { get; set; }
    public bool EveryoneCanPublish { get; set; }
    public bool EveryoneCanEdit { get; set; }
    public bool ChildMorePermissive { get; set; }
    public int IssueCount { get; set; }
    public int SubtreeIssueCount { get; set; }

    // Children (populated on expand)
    public List<ContentPermissionNodeDto>? Children { get; set; }
}

/// <summary>
/// For the Role/User Explorer view — summary per role/user.
/// </summary>
public class RoleAccessSummaryDto
{
    public string RoleOrUser { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty; // "Role" or "User"

    // Content grouped by access level
    public int FullAccessCount { get; set; }
    public int PublishCount { get; set; }
    public int EditCount { get; set; }
    public int ReadOnlyCount { get; set; }
    public int TotalContentCount { get; set; }
}

/// <summary>
/// Paginated result for the Role Explorer detail view.
/// </summary>
public class RoleExplorerResultDto
{
    public string RoleOrUser { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public List<RoleExplorerItemDto> Items { get; set; } = new();
}

/// <summary>
/// Single item in the Role Explorer detail list.
/// </summary>
public class RoleExplorerItemDto
{
    public int ContentId { get; set; }
    public string ContentName { get; set; } = string.Empty;
    public string? Breadcrumb { get; set; }
    public string? ContentTypeName { get; set; }
    public string Access { get; set; } = string.Empty;
    public bool IsInheriting { get; set; }
    public string EditUrl { get; set; } = string.Empty;
}

/// <summary>
/// For the Issues Dashboard.
/// </summary>
public class SecurityIssueDto
{
    public string IssueType { get; set; } = string.Empty;    // "EveryonePublish", "NoRestrictions", etc.
    public string Severity { get; set; } = string.Empty;     // "Critical", "Warning", "Info"
    public string Description { get; set; } = string.Empty;
    public int ContentId { get; set; }
    public string ContentName { get; set; } = string.Empty;
    public string? Breadcrumb { get; set; }
    public string EditUrl { get; set; } = string.Empty;
}

/// <summary>
/// Paginated issues result.
/// </summary>
public class SecurityIssuesResultDto
{
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public List<SecurityIssueDto> Items { get; set; } = new();
}

/// <summary>
/// Summary counts for the issues dashboard header stats.
/// </summary>
public class SecurityIssuesSummaryDto
{
    public int TotalIssues { get; set; }
    public int CriticalCount { get; set; }
    public int WarningCount { get; set; }
    public int InfoCount { get; set; }

    public int EveryonePublishCount { get; set; }
    public int EveryoneEditCount { get; set; }
    public int ChildMorePermissiveCount { get; set; }
    public int NoRestrictionsCount { get; set; }
}

/// <summary>
/// Flat row for CSV/Excel export.
/// </summary>
public class SecurityExportRow
{
    public int ContentId { get; set; }
    public string ContentName { get; set; } = string.Empty;
    public string? Breadcrumb { get; set; }
    public string? ContentTypeName { get; set; }
    public bool IsPage { get; set; }
    public string AclEntries { get; set; } = string.Empty; // semicolon-separated
    public bool IsInheriting { get; set; }
    public string Issues { get; set; } = string.Empty;     // semicolon-separated
}
