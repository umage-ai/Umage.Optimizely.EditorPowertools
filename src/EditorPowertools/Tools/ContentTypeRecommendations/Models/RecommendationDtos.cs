namespace UmageAI.Optimizely.EditorPowerTools.Tools.ContentTypeRecommendations.Models;

public class RecommendationRuleDto
{
    public string Id { get; set; } = string.Empty;
    public int ParentContentType { get; set; }
    public string? ParentContentTypeName { get; set; }
    public int? ParentContentId { get; set; }
    public string? ParentContentName { get; set; }
    public bool IncludeDescendants { get; set; }
    public bool ForThisContentFolder { get; set; }
    public List<SuggestedTypeDto> SuggestedTypes { get; set; } = new();
}

public class SuggestedTypeDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

public class SaveRuleRequest
{
    public string? Id { get; set; }
    public int ParentContentType { get; set; } = -1;
    public int? ParentContentId { get; set; }
    public bool IncludeDescendants { get; set; }
    public bool ForThisContentFolder { get; set; }
    public List<int> ContentTypesToSuggest { get; set; } = new();
}
