namespace EditorPowertools.Tools.ContentDetails.Models;

public class ContentDetailsDto
{
    public int ContentId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string ContentTypeName { get; set; } = string.Empty;
    public string? ContentGuid { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? CreatedBy { get; set; }
    public DateTime? Created { get; set; }
    public string? ChangedBy { get; set; }
    public DateTime? Changed { get; set; }
    public DateTime? Published { get; set; }
    public string? Language { get; set; }
    public string? ParentName { get; set; }
    public int VersionCount { get; set; }

    /// <summary>Content items that this content links to or embeds.</summary>
    public List<ContentUsageDto> Uses { get; set; } = new();

    /// <summary>Content items that reference this content.</summary>
    public List<ContentReferenceDto> UsedBy { get; set; } = new();

    /// <summary>Recursive content structure (content areas, blocks within blocks).</summary>
    public ContentTreeNodeDto? ContentTree { get; set; }

    public List<VersionSummaryDto> Versions { get; set; } = new();

    /// <summary>Personalizations (visitor groups) used on this content and sub-content.</summary>
    public List<PersonalizationInfoDto> Personalizations { get; set; } = new();

    /// <summary>Language version sync status — which languages are behind the master.</summary>
    public List<LanguageSyncDto> LanguageSync { get; set; } = new();
}

public class ContentUsageDto
{
    public int ContentId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string ContentTypeName { get; set; } = string.Empty;
    /// <summary>The property on THIS content that holds the reference.</summary>
    public string? PropertyName { get; set; }
    /// <summary>e.g. "ContentArea", "ContentReference", "Url"</summary>
    public string ReferenceType { get; set; } = string.Empty;
}

public class ContentReferenceDto
{
    public int ContentId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string ContentTypeName { get; set; } = string.Empty;
    public string? PropertyName { get; set; }
}

public class ContentTreeNodeDto
{
    public int ContentId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string ContentTypeName { get; set; } = string.Empty;
    /// <summary>The property name this content was found in (null for root).</summary>
    public string? PropertyName { get; set; }
    /// <summary>"ContentArea", "ContentReference", "Page" (child page)</summary>
    public string? NodeType { get; set; }
    public List<ContentTreeNodeDto> Children { get; set; } = new();
}

public class VersionSummaryDto
{
    public int VersionId { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime Saved { get; set; }
    public string? SavedBy { get; set; }
    public string? Language { get; set; }
    public bool IsCommonDraft { get; set; }
    public bool IsMasterLanguageBranch { get; set; }
    /// <summary>Properties that changed compared to the previous version.</summary>
    public List<PropertyChangeDto>? ChangedProperties { get; set; }
    /// <summary>CMS compare URL to diff this version against its predecessor.</summary>
    public string? CompareUrl { get; set; }
}

public class PropertyChangeDto
{
    public string PropertyName { get; set; } = string.Empty;
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
}

public class PersonalizationInfoDto
{
    public string VisitorGroupName { get; set; } = string.Empty;
    /// <summary>Where the personalization is used (property name on which content).</summary>
    public string ContentName { get; set; } = string.Empty;
    public int ContentId { get; set; }
    public string PropertyName { get; set; } = string.Empty;
}

public class LanguageSyncDto
{
    public string Language { get; set; } = string.Empty;
    public bool IsMaster { get; set; }
    public DateTime? LastChanged { get; set; }
    public string? LastChangedBy { get; set; }
    public string Status { get; set; } = string.Empty;
    /// <summary>True if this language version was changed before the master was last changed.</summary>
    public bool IsBehindMaster { get; set; }
}
