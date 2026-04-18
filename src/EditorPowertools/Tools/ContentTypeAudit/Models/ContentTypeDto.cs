using UmageAI.Optimizely.EditorPowerTools.Abstractions;

namespace UmageAI.Optimizely.EditorPowerTools.Tools.ContentTypeAudit.Models;

public class ContentTypeDto
{
    public int Id { get; set; }
    public Guid Guid { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
    public string? Description { get; set; }
    public string? GroupName { get; set; }
    public string Base { get; set; } = string.Empty;
    public string? ModelType { get; set; }
    public string? ParentTypeName { get; set; }
    public string? DefaultController { get; set; }
    public string? EditUrl { get; set; }
    public int PropertyCount { get; set; }
    public bool IsSystemType { get; set; }
    public bool IsCodeless { get; set; }
    public string? IconUrl { get; set; }
    public DateTime? Created { get; set; }
    public DateTime? Saved { get; set; }
    public string? SavedBy { get; set; }

    // From DDS statistics (null if job hasn't run)
    public int? ContentCount { get; set; }
    public int? PublishedCount { get; set; }
    public int? ReferencedCount { get; set; }
    public int? UnreferencedCount { get; set; }
    public DateTime? StatisticsUpdated { get; set; }

    // CMS 13 metadata — null on CMS 12, populated on CMS 13
    public bool? IsContract { get; set; }
    public string[]? CompositionBehaviors { get; set; }
    public ContractRef[]? Contracts { get; set; }
}

public class PropertyDefinitionDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string TypeName { get; set; } = string.Empty;
    public string? TabName { get; set; }
    public int SortOrder { get; set; }
    public bool Required { get; set; }
    public bool Searchable { get; set; }
    public bool LanguageSpecific { get; set; }
    public bool ExistsOnModel { get; set; }

    /// <summary>Code-defined, inherited from parent, or code-less (only in DB).</summary>
    public PropertyOrigin Origin { get; set; }
}

public enum PropertyOrigin
{
    /// <summary>Defined on this type's model class.</summary>
    Defined,

    /// <summary>Inherited from a parent type's model class.</summary>
    Inherited,

    /// <summary>Exists in database but not on the model (code-less).</summary>
    Codeless
}

public class ContentUsageDto
{
    public int ContentId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Language { get; set; } = string.Empty;
    public string? Breadcrumb { get; set; }
    public string? EditUrl { get; set; }
    public bool IsPublished { get; set; }
    public int ReferenceCount { get; set; }
}

public class SoftLinkDto
{
    public int OwnerContentId { get; set; }
    public string OwnerName { get; set; } = string.Empty;
    public string? OwnerTypeName { get; set; }
    public string? Language { get; set; }
    public string? PropertyName { get; set; }
    public string? EditUrl { get; set; }
}

public class ContentTypeTreeNodeDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
    public int? ContentCount { get; set; }
    public bool IsCodeless { get; set; }
    public List<ContentTypeTreeNodeDto> Children { get; set; } = new();
}
