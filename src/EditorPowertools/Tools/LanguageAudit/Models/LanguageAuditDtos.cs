namespace UmageAI.Optimizely.EditorPowerTools.Tools.LanguageAudit.Models;

public class LanguageOverviewDto
{
    public int TotalContent { get; set; }
    public List<string> EnabledLanguages { get; set; } = new();
    public List<LanguageStatDto> LanguageStats { get; set; } = new();
    public int MissingTranslationsCount { get; set; }
    public int StaleTranslationsCount { get; set; }
}

public class LanguageStatDto
{
    public string Language { get; set; } = string.Empty;
    public int TotalContent { get; set; }
    public int PublishedCount { get; set; }
    public double CoveragePercent { get; set; }
}

public class MissingTranslationDto
{
    public int ContentId { get; set; }
    public string ContentName { get; set; } = string.Empty;
    public string? ContentTypeName { get; set; }
    public string? Breadcrumb { get; set; }
    public string MasterLanguage { get; set; } = string.Empty;
    public List<string> AvailableLanguages { get; set; } = new();
    public string? EditUrl { get; set; }
}

public class LanguageCoverageNodeDto
{
    public int ContentId { get; set; }
    public string ContentName { get; set; } = string.Empty;
    public bool HasLanguage { get; set; }
    public int TotalChildren { get; set; }
    public int ChildrenWithLanguage { get; set; }
    public double CoveragePercent { get; set; }
    public List<LanguageCoverageNodeDto>? Children { get; set; }
}

public class StaleTranslationDto
{
    public int ContentId { get; set; }
    public string ContentName { get; set; } = string.Empty;
    public string? ContentTypeName { get; set; }
    public string? Breadcrumb { get; set; }
    public string MasterLanguage { get; set; } = string.Empty;
    public DateTime MasterLastModified { get; set; }
    public string OtherLanguage { get; set; } = string.Empty;
    public DateTime OtherLastModified { get; set; }
    public int DaysBehind { get; set; }
    public string? EditUrl { get; set; }
}

public class TranslationQueueItemDto
{
    public int ContentId { get; set; }
    public string ContentName { get; set; } = string.Empty;
    public string? ContentTypeName { get; set; }
    public string? Breadcrumb { get; set; }
    public string MasterLanguage { get; set; } = string.Empty;
    public DateTime MasterLastModified { get; set; }
    public List<string> AvailableLanguages { get; set; } = new();
    public string? EditUrl { get; set; }
}

public class TranslationQueueResultDto
{
    public List<TranslationQueueItemDto> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages { get; set; }
}
