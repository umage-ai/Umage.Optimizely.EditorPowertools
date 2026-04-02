using System.Text.Json;
using EPiServer.Shell;
using EditorPowertools.Tools.SecurityAudit.Models;
using Microsoft.Extensions.Logging;

namespace EditorPowertools.Tools.SecurityAudit;

/// <summary>
/// Business logic layer for Security Audit. Reads from DDS via repository
/// and provides the query interface for the API controller.
/// </summary>
public class SecurityAuditService
{
    private readonly SecurityAuditRepository _repository;
    private readonly ILogger<SecurityAuditService> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public SecurityAuditService(
        SecurityAuditRepository repository,
        ILogger<SecurityAuditService> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    // --- Content Tree View ---

    /// <summary>
    /// Returns top-level children of a node for lazy tree loading.
    /// parentContentId = 0 returns root-level content.
    /// </summary>
    public List<ContentPermissionNodeDto> GetChildren(int parentContentId)
    {
        // parentContentId=0 means "root" — in Optimizely, RootPage is typically ID 1
        // Try the requested ID first, if empty and ID is 0, try common root IDs
        var children = _repository.GetByParent(parentContentId).ToList();
        if (children.Count == 0 && parentContentId == 0)
        {
            children = _repository.GetByParent(1).ToList(); // ContentReference.RootPage
        }
        var allRecords = _repository.GetAll().ToList();
        var childLookup = allRecords.GroupBy(r => r.ParentContentId)
            .ToDictionary(g => g.Key, g => true);

        return children.Select(r => MapToNodeDto(r, childLookup.ContainsKey(r.ContentId))).ToList();
    }

    /// <summary>
    /// Returns the full ACL detail for a single content item.
    /// </summary>
    public ContentPermissionNodeDto? GetNodeDetail(int contentId)
    {
        var record = _repository.GetByContentId(contentId);
        if (record == null) return null;

        var allRecords = _repository.GetAll().ToList();
        var hasChildren = allRecords.Any(r => r.ParentContentId == contentId);

        return MapToNodeDto(record, hasChildren);
    }

    /// <summary>
    /// Returns the ancestor chain from root to the specified content (for "reveal in tree").
    /// </summary>
    public List<int> GetPathToContent(int contentId)
    {
        var path = new List<int>();
        var record = _repository.GetByContentId(contentId);

        while (record != null && record.ContentId != 0)
        {
            path.Insert(0, record.ContentId);
            if (record.ParentContentId == 0) break;
            record = _repository.GetByContentId(record.ParentContentId);
        }

        return path;
    }

    // --- Role/User Explorer ---

    /// <summary>
    /// Lists all distinct roles and users found across all ACLs with summary counts.
    /// </summary>
    public List<RoleAccessSummaryDto> GetAllRolesAndUsers()
    {
        var allRecords = _repository.GetAll().ToList();
        var summaries = new Dictionary<string, RoleAccessSummaryDto>(StringComparer.OrdinalIgnoreCase);

        foreach (var record in allRecords)
        {
            var entries = DeserializeEntries(record.AclEntriesJson);
            foreach (var entry in entries)
            {
                var key = $"{entry.EntityType}:{entry.Name}";
                if (!summaries.TryGetValue(key, out var summary))
                {
                    summary = new RoleAccessSummaryDto
                    {
                        RoleOrUser = entry.Name,
                        EntityType = entry.EntityType
                    };
                    summaries[key] = summary;
                }

                summary.TotalContentCount++;
                CategorizeAccess(entry.Access, summary);
            }
        }

        return summaries.Values
            .OrderByDescending(s => s.TotalContentCount)
            .ToList();
    }

    /// <summary>
    /// Returns all content accessible by a given role or user, optionally filtered by access level.
    /// </summary>
    public RoleExplorerResultDto GetContentForRoleOrUser(
        string name, string entityType,
        string? accessLevelFilter = null,
        int page = 1, int pageSize = 50)
    {
        var records = _repository.GetByRoleOrUser(name).ToList();
        var items = new List<RoleExplorerItemDto>();

        foreach (var record in records)
        {
            var entries = DeserializeEntries(record.AclEntriesJson);
            var matchingEntry = entries.FirstOrDefault(e =>
                string.Equals(e.Name, name, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(e.EntityType, entityType, StringComparison.OrdinalIgnoreCase));

            if (matchingEntry == null) continue;

            if (!string.IsNullOrEmpty(accessLevelFilter) &&
                !string.Equals(matchingEntry.Access, accessLevelFilter, StringComparison.OrdinalIgnoreCase))
                continue;

            var editUrl = $"{Paths.ToResource("CMS", "")}#/content/{record.ContentId}";

            items.Add(new RoleExplorerItemDto
            {
                ContentId = record.ContentId,
                ContentName = record.ContentName,
                Breadcrumb = record.Breadcrumb,
                ContentTypeName = record.ContentTypeName,
                Access = matchingEntry.Access,
                IsInheriting = record.IsInheriting,
                EditUrl = editUrl
            });
        }

        var totalCount = items.Count;
        var paged = items.Skip((page - 1) * pageSize).Take(pageSize).ToList();

        return new RoleExplorerResultDto
        {
            RoleOrUser = name,
            EntityType = entityType,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize,
            Items = paged
        };
    }

    // --- Issues Dashboard ---

    /// <summary>
    /// Returns all detected security issues, filterable by type and severity.
    /// </summary>
    public SecurityIssuesResultDto GetIssues(
        string? issueTypeFilter = null,
        string? severityFilter = null,
        int page = 1, int pageSize = 50)
    {
        var records = _repository.GetWithIssues().ToList();
        var issues = new List<SecurityIssueDto>();

        foreach (var record in records)
        {
            var editUrl = $"{Paths.ToResource("CMS", "")}#/content/{record.ContentId}";
            AddIssuesForRecord(record, editUrl, issues);
        }

        // Apply filters
        if (!string.IsNullOrEmpty(issueTypeFilter))
            issues = issues.Where(i => string.Equals(i.IssueType, issueTypeFilter, StringComparison.OrdinalIgnoreCase)).ToList();
        if (!string.IsNullOrEmpty(severityFilter))
            issues = issues.Where(i => string.Equals(i.Severity, severityFilter, StringComparison.OrdinalIgnoreCase)).ToList();

        // Sort: Critical first, then Warning, then Info
        issues = issues.OrderBy(i => i.Severity switch
        {
            "Critical" => 0,
            "Warning" => 1,
            "Info" => 2,
            _ => 3
        }).ThenBy(i => i.ContentName).ToList();

        var totalCount = issues.Count;
        var paged = issues.Skip((page - 1) * pageSize).Take(pageSize).ToList();

        return new SecurityIssuesResultDto
        {
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize,
            Items = paged
        };
    }

    /// <summary>
    /// Returns summary counts for the issues dashboard header stats.
    /// </summary>
    public SecurityIssuesSummaryDto GetIssuesSummary()
    {
        var records = _repository.GetWithIssues().ToList();

        return new SecurityIssuesSummaryDto
        {
            TotalIssues = records.Sum(r => r.IssueCount),
            CriticalCount = records.Count(r => r.EveryoneCanPublish || r.EveryoneCanEdit),
            WarningCount = records.Count(r => r.ChildMorePermissive),
            InfoCount = records.Count(r => r.HasNoRestrictions && r.IsPage),
            EveryonePublishCount = records.Count(r => r.EveryoneCanPublish),
            EveryoneEditCount = records.Count(r => r.EveryoneCanEdit),
            ChildMorePermissiveCount = records.Count(r => r.ChildMorePermissive),
            NoRestrictionsCount = records.Count(r => r.HasNoRestrictions && r.IsPage)
        };
    }

    // --- Export ---

    /// <summary>
    /// Returns all permission data for CSV/Excel export.
    /// </summary>
    public IEnumerable<SecurityExportRow> ExportAll()
    {
        var allRecords = _repository.GetAll();

        foreach (var record in allRecords)
        {
            var entries = DeserializeEntries(record.AclEntriesJson);
            var aclSummary = string.Join("; ", entries.Select(e => $"{e.Name}:{e.Access}"));

            var issuesList = new List<string>();
            if (record.EveryoneCanPublish) issuesList.Add("EveryonePublish");
            if (record.EveryoneCanEdit) issuesList.Add("EveryoneEdit");
            if (record.ChildMorePermissive) issuesList.Add("ChildMorePermissive");
            if (record.HasNoRestrictions && record.IsPage) issuesList.Add("NoRestrictions");

            yield return new SecurityExportRow
            {
                ContentId = record.ContentId,
                ContentName = record.ContentName,
                Breadcrumb = record.Breadcrumb,
                ContentTypeName = record.ContentTypeName,
                IsPage = record.IsPage,
                AclEntries = aclSummary,
                IsInheriting = record.IsInheriting,
                Issues = string.Join("; ", issuesList)
            };
        }
    }

    // --- Job Status ---

    /// <summary>
    /// Returns when the analyzer last ran (from DDS timestamp).
    /// </summary>
    public DateTime? GetLastAnalysisTime()
    {
        try
        {
            var record = _repository.GetAll().FirstOrDefault();
            return record?.LastUpdated;
        }
        catch
        {
            return null;
        }
    }

    // --- Private Helpers ---

    private static ContentPermissionNodeDto MapToNodeDto(SecurityAuditRecord record, bool hasChildren)
    {
        var entries = DeserializeEntries(record.AclEntriesJson);

        return new ContentPermissionNodeDto
        {
            ContentId = record.ContentId,
            Name = record.ContentName,
            ContentTypeName = record.ContentTypeName,
            Breadcrumb = record.Breadcrumb,
            IsPage = record.IsPage,
            HasChildren = hasChildren,
            Entries = entries,
            IsInheriting = record.IsInheriting,
            HasExplicitAcl = record.HasExplicitAcl,
            HasNoRestrictions = record.HasNoRestrictions,
            EveryoneCanPublish = record.EveryoneCanPublish,
            EveryoneCanEdit = record.EveryoneCanEdit,
            ChildMorePermissive = record.ChildMorePermissive,
            IssueCount = record.IssueCount,
            SubtreeIssueCount = record.SubtreeIssueCount
        };
    }

    private static List<AclEntryDto> DeserializeEntries(string json)
    {
        if (string.IsNullOrWhiteSpace(json) || json == "[]")
            return new List<AclEntryDto>();

        try
        {
            return JsonSerializer.Deserialize<List<AclEntryDto>>(json, JsonOptions) ?? new List<AclEntryDto>();
        }
        catch
        {
            return new List<AclEntryDto>();
        }
    }

    private static void CategorizeAccess(string access, RoleAccessSummaryDto summary)
    {
        var upper = access.ToUpperInvariant();
        if (upper.Contains("FULLACESS") || upper.Contains("FULLACCESS") || upper.Contains("ADMINISTER"))
            summary.FullAccessCount++;
        else if (upper.Contains("PUBLISH"))
            summary.PublishCount++;
        else if (upper.Contains("EDIT") || upper.Contains("CREATE") || upper.Contains("DELETE"))
            summary.EditCount++;
        else
            summary.ReadOnlyCount++;
    }

    private static void AddIssuesForRecord(SecurityAuditRecord record, string editUrl, List<SecurityIssueDto> issues)
    {
        if (record.EveryoneCanPublish)
        {
            issues.Add(new SecurityIssueDto
            {
                IssueType = "EveryonePublish",
                Severity = "Critical",
                Description = "\"Everyone\" role has Publish or higher access",
                ContentId = record.ContentId,
                ContentName = record.ContentName,
                Breadcrumb = record.Breadcrumb,
                EditUrl = editUrl
            });
        }

        if (record.EveryoneCanEdit)
        {
            issues.Add(new SecurityIssueDto
            {
                IssueType = "EveryoneEdit",
                Severity = "Critical",
                Description = "\"Everyone\" role has Edit or higher access",
                ContentId = record.ContentId,
                ContentName = record.ContentName,
                Breadcrumb = record.Breadcrumb,
                EditUrl = editUrl
            });
        }

        if (record.ChildMorePermissive)
        {
            issues.Add(new SecurityIssueDto
            {
                IssueType = "ChildMorePermissive",
                Severity = "Warning",
                Description = "This node grants broader access than its parent",
                ContentId = record.ContentId,
                ContentName = record.ContentName,
                Breadcrumb = record.Breadcrumb,
                EditUrl = editUrl
            });
        }

        if (record.HasNoRestrictions && record.IsPage)
        {
            issues.Add(new SecurityIssueDto
            {
                IssueType = "NoRestrictions",
                Severity = "Info",
                Description = "Page has no access restrictions set",
                ContentId = record.ContentId,
                ContentName = record.ContentName,
                Breadcrumb = record.Breadcrumb,
                EditUrl = editUrl
            });
        }
    }
}
