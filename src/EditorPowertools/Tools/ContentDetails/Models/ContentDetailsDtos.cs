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
    public List<PropertySummaryDto> Properties { get; set; } = new();
    public List<ContentReferenceDto> ReferencedBy { get; set; } = new();
    public List<VersionSummaryDto> Versions { get; set; } = new();
}

public class PropertySummaryDto
{
    public string Name { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string TypeName { get; set; } = string.Empty;
    public string? Value { get; set; }
    public bool IsContentArea { get; set; }
    public int ItemCount { get; set; }
}

public class ContentReferenceDto
{
    public int ContentId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string ContentTypeName { get; set; } = string.Empty;
    public string? PropertyName { get; set; }
}

public class VersionSummaryDto
{
    public int VersionId { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime Saved { get; set; }
    public string? SavedBy { get; set; }
}
