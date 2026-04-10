namespace UmageAI.Optimizely.EditorPowerTools.Tools.ContentStatistics.Models;

/// <summary>
/// Top-level response containing all dashboard data in one call.
/// </summary>
public class ContentStatisticsDashboardDto
{
    public SummaryStatsDto Summary { get; set; } = new();
    public IReadOnlyList<ContentTypeDistributionDto> TypeDistribution { get; set; } = [];
    public IReadOnlyList<ContentCreationMonthDto> CreationOverTime { get; set; } = [];
    public IReadOnlyList<StaleContentDto> StaleContent { get; set; } = [];
    public IReadOnlyList<EditorActivityDto> TopEditors { get; set; } = [];
}

public class SummaryStatsDto
{
    public int TotalContent { get; set; }
    public int TotalPages { get; set; }
    public int TotalBlocks { get; set; }
    public int TotalMedia { get; set; }
    public double AverageVersionsPerItem { get; set; }
    public DateTime? LastAnalyzed { get; set; }
}

public class ContentTypeDistributionDto
{
    public string Category { get; set; } = string.Empty;
    public int Count { get; set; }
}

public class ContentCreationMonthDto
{
    public string Month { get; set; } = string.Empty;
    public int Count { get; set; }
}

public class StaleContentDto
{
    public int ContentId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string ContentTypeName { get; set; } = string.Empty;
    public DateTime LastModified { get; set; }
    public int DaysSinceModified { get; set; }
    public string? EditUrl { get; set; }
}

public class EditorActivityDto
{
    public string Username { get; set; } = string.Empty;
    public int EditCount { get; set; }
    public int PublishCount { get; set; }
    public DateTime? LastActive { get; set; }
}
