using EPiServer.Core;
using EPiServer.Data;
using EPiServer.Data.Dynamic;

namespace EditorPowertools.Tools.ContentTypeRecommendations;

[EPiServerDataStore(AutomaticallyCreateStore = true, AutomaticallyRemapStore = true, StoreName = "EditorPowertools_ContentTypeRecommendations")]
public class ContentTypeRecommendationRule : IDynamicData
{
    public Identity Id { get; set; } = Identity.NewIdentity();

    /// <summary>Content type ID of the parent. -1 means any.</summary>
    public int ParentContentType { get; set; } = -1;

    /// <summary>If true, rule applies to descendants of the parent content too.</summary>
    public bool IncludeDescendants { get; set; }

    /// <summary>Optional specific content item (folder/page) this rule applies to.</summary>
    public ContentReference? ParentContent { get; set; }

    /// <summary>If true, rule only applies to this exact content folder, not its children.</summary>
    public bool ForThisContentFolder { get; set; }

    /// <summary>List of content type IDs to suggest when this rule matches.</summary>
    public List<int> ContentTypesToSuggest { get; set; } = new();
}

public class ContentTypeRecommendationRepository
{
    public IEnumerable<ContentTypeRecommendationRule> GetAll()
    {
        var store = GetStore();
        return store.Items<ContentTypeRecommendationRule>().ToList();
    }

    public ContentTypeRecommendationRule? GetById(Identity id)
    {
        var store = GetStore();
        return store.Load<ContentTypeRecommendationRule>(id);
    }

    public void Save(ContentTypeRecommendationRule rule)
    {
        var store = GetStore();
        store.Save(rule);
    }

    public void Delete(Identity id)
    {
        var store = GetStore();
        store.Delete(id);
    }

    private static DynamicDataStore GetStore()
    {
        return DynamicDataStoreFactory.Instance.CreateStore(typeof(ContentTypeRecommendationRule));
    }
}
