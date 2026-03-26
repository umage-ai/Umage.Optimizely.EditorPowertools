namespace EditorPowertools.Tools.AudienceManager.Models;

public class VisitorGroupDetailDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string CleanName { get; set; } = string.Empty;
    public string? Category { get; set; }
    public string? Notes { get; set; }
    public int CriteriaCount { get; set; }
    public bool StatisticsEnabled { get; set; }
    public string CriteriaOperator { get; set; } = string.Empty;
    public List<CriterionDto> Criteria { get; set; } = new();
    public string EditUrl { get; set; } = string.Empty;
    public int? UsageCount { get; set; }
}

public class CriterionDto
{
    public string TypeName { get; set; } = string.Empty;
    public string? Description { get; set; }
}

public class VisitorGroupUsageDto
{
    public int ContentId { get; set; }
    public string ContentName { get; set; } = string.Empty;
    public string? PropertyName { get; set; }
    public string? UsageType { get; set; }
    public string? EditUrl { get; set; }
}
