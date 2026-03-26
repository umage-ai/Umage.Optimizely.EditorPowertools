namespace EditorPowertools.Tools.PersonalizationAudit.Models;

public class PersonalizationUsageDto
{
    public int ContentId { get; set; }
    public string ContentName { get; set; } = string.Empty;
    public string? ContentTypeName { get; set; }
    public string? Language { get; set; }
    public string PropertyName { get; set; } = string.Empty;
    public string VisitorGroupId { get; set; } = string.Empty;
    public string VisitorGroupName { get; set; } = string.Empty;
    public string UsageType { get; set; } = string.Empty;
    public string? Breadcrumb { get; set; }
    public string? EditUrl { get; set; }
    public int? ParentContentId { get; set; }
    public string? ParentContentName { get; set; }
}

public class VisitorGroupDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int CriteriaCount { get; set; }
    public bool StatisticsEnabled { get; set; }
}
