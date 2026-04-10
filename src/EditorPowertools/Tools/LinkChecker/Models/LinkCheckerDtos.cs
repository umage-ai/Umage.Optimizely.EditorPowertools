namespace UmageAI.Optimizely.EditorPowerTools.Tools.LinkChecker.Models;

public class LinkCheckDto
{
    public int ContentId { get; set; }
    public string ContentName { get; set; } = string.Empty;
    public string? ContentTypeName { get; set; }
    public string PropertyName { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string? FriendlyUrl { get; set; }
    public int? TargetContentId { get; set; }
    public string LinkType { get; set; } = string.Empty;
    public int StatusCode { get; set; }
    public string StatusText { get; set; } = string.Empty;
    public bool IsValid { get; set; }
    public string? Breadcrumb { get; set; }
    public string? EditUrl { get; set; }
    /// <summary>For blocks: where this block is used (page names).</summary>
    public string? UsedOn { get; set; }
    /// <summary>For blocks: structured usage info (name|friendlyUrl|editUrl separated by ;;).</summary>
    public string? UsedOnEditUrls { get; set; }
    public DateTime LastChecked { get; set; }
}

public class LinkCheckerStatsDto
{
    public int TotalLinks { get; set; }
    public int BrokenLinks { get; set; }
    public int ValidLinks { get; set; }
    public int InternalLinks { get; set; }
    public int ExternalLinks { get; set; }
}
