using EPiServer.Data;
using EPiServer.Data.Dynamic;

namespace UmageAI.Optimizely.EditorPowerTools.Tools.PersonalizationAudit;

/// <summary>
/// DDS-persisted record holding a single personalization usage instance.
/// Updated by the scheduled job that scans content for visitor group usage.
/// </summary>
[EPiServerDataStore(AutomaticallyCreateStore = true, AutomaticallyRemapStore = true, StoreName = "EditorPowertools_PersonalizationUsage")]
public class PersonalizationUsageRecord : IDynamicData
{
    public Identity Id { get; set; } = Identity.NewIdentity();
    public int ContentId { get; set; }
    public string ContentName { get; set; } = string.Empty;
    public string? ContentTypeName { get; set; }
    public string? Language { get; set; }
    public string PropertyName { get; set; } = string.Empty;
    public string VisitorGroupId { get; set; } = string.Empty;
    public string VisitorGroupName { get; set; } = string.Empty;

    /// <summary>"AccessRight", "ContentArea", or "XhtmlString"</summary>
    public string UsageType { get; set; } = string.Empty;

    public string? Breadcrumb { get; set; }
    public string? EditUrl { get; set; }
    public int? ParentContentId { get; set; }
    public string? ParentContentName { get; set; }
    public DateTime LastUpdated { get; set; }
}

/// <summary>
/// Repository for reading/writing personalization usage records from DDS.
/// </summary>
public class PersonalizationUsageRepository
{
    public IEnumerable<PersonalizationUsageRecord> GetAll()
    {
        var store = GetStore();
        return store.Items<PersonalizationUsageRecord>().ToList();
    }

    public IEnumerable<PersonalizationUsageRecord> GetByVisitorGroup(string visitorGroupId)
    {
        var store = GetStore();
        return store.Items<PersonalizationUsageRecord>()
            .Where(r => r.VisitorGroupId == visitorGroupId)
            .ToList();
    }

    public void Clear()
    {
        var store = GetStore();
        store.DeleteAll();
    }

    public void Save(PersonalizationUsageRecord record)
    {
        var store = GetStore();
        store.Save(record);
    }

    private static DynamicDataStore GetStore()
    {
        return DynamicDataStoreFactory.Instance.CreateStore(typeof(PersonalizationUsageRecord));
    }
}
