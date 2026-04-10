namespace UmageAI.Optimizely.EditorPowerTools.Tools.ActivityTimeline.Models;

public class ActivityDto
{
    public int ContentId { get; set; }
    public int VersionId { get; set; }
    public string ContentName { get; set; } = string.Empty;
    public string ContentTypeName { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string User { get; set; } = string.Empty;
    public DateTime TimestampUtc { get; set; }
    public string? Language { get; set; }
    public string? EditUrl { get; set; }
    public bool HasPreviousVersion { get; set; }
    /// <summary>For comment/message entries, the message text.</summary>
    public string? Message { get; set; }
}

public class ActivityFilterRequest
{
    public int Skip { get; set; } = 0;
    public int Take { get; set; } = 50;
    public string? User { get; set; }
    public string? ContentTypeName { get; set; }
    public string? Action { get; set; }
    public DateTime? FromUtc { get; set; }
    public DateTime? ToUtc { get; set; }
    /// <summary>When set, filters to a single content item's history.</summary>
    public int? ContentId { get; set; }
}

public class ActivityTimelineResponse
{
    public List<ActivityDto> Activities { get; set; } = new();
    public int TotalCount { get; set; }
    public bool HasMore { get; set; }
    /// <summary>When filtering by a single content item, the name of that item.</summary>
    public string? ContentName { get; set; }
}

public class ActivityStatsDto
{
    public int TotalToday { get; set; }
    public int ActiveEditorsToday { get; set; }
    public int PublishesToday { get; set; }
    public int DraftsToday { get; set; }
}

public class VersionComparisonDto
{
    public bool HasPrevious { get; set; }
    public string ContentName { get; set; } = string.Empty;
    public int CurrentVersion { get; set; }
    public int PreviousVersion { get; set; }
    public List<PropertyChangeDto> Changes { get; set; } = new();
}

public class PropertyChangeDto
{
    public string PropertyName { get; set; } = string.Empty;
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
    /// <summary>True if the property contains HTML (XhtmlString). UI should render in an iframe/sandbox.</summary>
    public bool IsHtml { get; set; }
}
