namespace EditorPowertools.Tools.ManageChildren.Models;

public class ChildItemDto
{
    public int ContentId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string ContentTypeName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? Language { get; set; }
    public DateTime? Changed { get; set; }
    public string? ChangedBy { get; set; }
    public int SortIndex { get; set; }
    public bool HasChildren { get; set; }
    public string EditUrl { get; set; } = string.Empty;
}

public class BulkActionRequest
{
    public int ParentContentId { get; set; }
    public List<int> ContentIds { get; set; } = new();
}

public class BulkMoveRequest : BulkActionRequest
{
    public int TargetParentId { get; set; }
}

public class BulkActionResult
{
    public int Succeeded { get; set; }
    public int Failed { get; set; }
    public List<string> Errors { get; set; } = new();
}
