namespace UmageAI.Optimizely.EditorPowerTools.Tools.BulkPropertyEditor.Models;

public record ContentTypeListItem(int Id, string Name, string BaseType);

public record LanguageInfo(string Code, string Name, bool IsDefault);

public record PropertyColumnInfo(string Name, string DisplayName, string TypeName, bool IsEditable);

public class ContentFilterRequest
{
    public int ContentTypeId { get; set; }
    public string Language { get; set; } = "en";
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 50;
    public string? SortBy { get; set; }
    public string SortDirection { get; set; } = "asc";
    public List<PropertyFilter>? Filters { get; set; }
    public List<string>? Columns { get; set; }
    public bool IncludeReferences { get; set; }
}

public class PropertyFilter
{
    public string PropertyName { get; set; } = "";
    public string Operator { get; set; } = "contains";
    public string Value { get; set; } = "";
}

public record ContentFilterResponse(
    List<ContentItemRow> Items,
    int TotalCount,
    int Page,
    int PageSize,
    int TotalPages);

public record ContentItemRow(
    int ContentId,
    string Name,
    string ContentTypeName,
    string Status,
    DateTime? LastEdited,
    string? EditedBy,
    string Language,
    bool CanEdit,
    bool CanPublish,
    string EditUrl,
    string? ParentName,
    string? ParentEditUrl,
    Dictionary<string, PropertyValue> Properties,
    List<ContentReferenceInfo>? References);

public record PropertyValue(string? DisplayValue, object? RawValue, bool IsEditable, string TypeName);

public record ContentReferenceInfo(int ContentId, string Name, string ContentTypeName, string? EditUrl);

public class InlineEditRequest
{
    public int ContentId { get; set; }
    public string Language { get; set; } = "en";
    public string PropertyName { get; set; } = "";
    public string? Value { get; set; }
}

public class BulkSaveRequest
{
    public string Action { get; set; } = "save";
    public List<ContentEditItem> Items { get; set; } = [];
}

public class ContentEditItem
{
    public int ContentId { get; set; }
    public string Language { get; set; } = "en";
    public Dictionary<string, string?> PropertyChanges { get; set; } = [];
}
