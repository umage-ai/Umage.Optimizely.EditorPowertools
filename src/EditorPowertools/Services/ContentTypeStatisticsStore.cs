using EPiServer.Data;
using EPiServer.Data.Dynamic;

namespace UmageAI.Optimizely.EditorPowerTools.Services;

/// <summary>
/// DDS-persisted record holding aggregated statistics for a content type.
/// Updated by the scheduled job that traverses all content.
/// </summary>
[EPiServerDataStore(AutomaticallyCreateStore = true, AutomaticallyRemapStore = true, StoreName = "EditorPowertools_ContentTypeStatistics")]
public class ContentTypeStatisticsRecord : IDynamicData
{
    public Identity Id { get; set; } = Identity.NewIdentity();

    /// <summary>Content type ID from EPiServer.</summary>
    public int ContentTypeId { get; set; }

    /// <summary>Total number of content items of this type (across all languages).</summary>
    public int ContentCount { get; set; }

    /// <summary>Number of content items that are published.</summary>
    public int PublishedCount { get; set; }

    /// <summary>Number of content items referenced by other content (via soft links).</summary>
    public int ReferencedCount { get; set; }

    /// <summary>Number of content items NOT referenced by any other content.</summary>
    public int UnreferencedCount { get; set; }

    /// <summary>When these statistics were last computed.</summary>
    public DateTime LastUpdated { get; set; }
}

/// <summary>
/// Repository for reading/writing content type statistics from DDS.
/// </summary>
public class ContentTypeStatisticsRepository
{
    public virtual IEnumerable<ContentTypeStatisticsRecord> GetAll()
    {
        var store = GetStore();
        return store.Items<ContentTypeStatisticsRecord>().ToList();
    }

    public ContentTypeStatisticsRecord? GetByContentTypeId(int contentTypeId)
    {
        var store = GetStore();
        return store.Items<ContentTypeStatisticsRecord>()
            .FirstOrDefault(r => r.ContentTypeId == contentTypeId);
    }

    public void SaveOrUpdate(ContentTypeStatisticsRecord record)
    {
        var store = GetStore();
        var existing = store.Items<ContentTypeStatisticsRecord>()
            .FirstOrDefault(r => r.ContentTypeId == record.ContentTypeId);

        if (existing != null)
        {
            existing.ContentCount = record.ContentCount;
            existing.PublishedCount = record.PublishedCount;
            existing.ReferencedCount = record.ReferencedCount;
            existing.UnreferencedCount = record.UnreferencedCount;
            existing.LastUpdated = record.LastUpdated;
            store.Save(existing);
        }
        else
        {
            store.Save(record);
        }
    }

    public void Clear()
    {
        var store = GetStore();
        store.DeleteAll();
    }

    private static DynamicDataStore GetStore()
    {
        return DynamicDataStoreFactory.Instance.CreateStore(typeof(ContentTypeStatisticsRecord));
    }
}
