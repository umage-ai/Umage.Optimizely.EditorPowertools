namespace UmageAI.Optimizely.EditorPowerTools.Tools.ContentImporter.Models;

public class FileUploadResponse
{
    public Guid SessionId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string FileType { get; set; } = string.Empty;
    public List<ColumnInfo> Columns { get; set; } = new();
    public List<Dictionary<string, string>> SampleRows { get; set; } = new();
    public int TotalRowCount { get; set; }
}

public class ColumnInfo
{
    public string Name { get; set; } = string.Empty;
    public List<string> SampleValues { get; set; } = new();
}

public class ImportContentTypeInfo
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? GroupName { get; set; }
    /// <summary>"Page", "Block", "Media"</summary>
    public string BaseType { get; set; } = string.Empty;
    public List<ImportPropertyInfo> Properties { get; set; } = new();
}

public class ImportPropertyInfo
{
    public string Name { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string TypeName { get; set; } = string.Empty;
    public bool IsRequired { get; set; }
    public bool IsContentArea { get; set; }
    public bool IsXhtmlString { get; set; }
    public bool IsContentReference { get; set; }
    public bool IsBoolean { get; set; }
    public bool IsBuiltIn { get; set; }
}

public class ImportMappingRequest
{
    public Guid SessionId { get; set; }
    public int TargetContentTypeId { get; set; }
    public int ParentContentId { get; set; }
    public string Language { get; set; } = "en";
    public bool PublishAfterImport { get; set; }
    /// <summary>Property name to use for the content Name (required).</summary>
    public string? NameSourceColumn { get; set; }
    public List<PropertyMapping> Mappings { get; set; } = new();
}

public class PropertyMapping
{
    public string TargetProperty { get; set; } = string.Empty;
    /// <summary>"column", "hardcoded", "inline-block", "skip"</summary>
    public string MappingType { get; set; } = "skip";
    public string? SourceColumn { get; set; }
    /// <summary>Supports {ColumnName} template placeholders.</summary>
    public string? HardcodedValue { get; set; }
    /// <summary>Single inline block (legacy).</summary>
    public InlineBlockMapping? InlineBlock { get; set; }
    /// <summary>Multiple inline blocks for a ContentArea.</summary>
    public List<InlineBlockMapping>? InlineBlocks { get; set; }
}

public class InlineBlockMapping
{
    public int BlockTypeId { get; set; }
    public List<PropertyMapping> Mappings { get; set; } = new();
}

public class DryRunResponse
{
    public Guid SessionId { get; set; }
    public List<PreviewItem> PreviewItems { get; set; } = new();
    public int TotalCount { get; set; }
    public List<string> Errors { get; set; } = new();
}

public class PreviewItem
{
    public int RowIndex { get; set; }
    public string Name { get; set; } = string.Empty;
    public Dictionary<string, string> Properties { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
}

public class ImportProgress
{
    public Guid SessionId { get; set; }
    /// <summary>"running", "completed", "failed"</summary>
    public string Status { get; set; } = "running";
    public int Processed { get; set; }
    public int Total { get; set; }
    public List<ImportError> Errors { get; set; } = new();
    public List<int> CreatedContentIds { get; set; } = new();
}

public class ImportError
{
    public int RowIndex { get; set; }
    public string Message { get; set; } = string.Empty;
}
