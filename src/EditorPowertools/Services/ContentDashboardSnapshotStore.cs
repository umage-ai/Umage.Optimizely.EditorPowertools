using EPiServer.Data;
using EPiServer.Data.Dynamic;

namespace UmageAI.Optimizely.EditorPowerTools.Services;

/// <summary>
/// DDS-persisted snapshot holding the live-scan parts of the Content Statistics dashboard
/// (creation-over-time, stale content, editor activity, average versions). Produced by
/// <see cref="Analyzers.ContentDashboardAnalyzer"/> during the unified content analysis job
/// and read by ContentStatisticsService.GetDashboard. Lists are JSON-serialised to keep DDS
/// schema flat — there is at most one row of this record at any time.
/// </summary>
[EPiServerDataStore(AutomaticallyCreateStore = true, AutomaticallyRemapStore = true, StoreName = "EditorPowertools_ContentDashboardSnapshot")]
public class ContentDashboardSnapshotRecord : IDynamicData
{
    public Identity Id { get; set; } = Identity.NewIdentity();
    public DateTime LastUpdated { get; set; }
    public double AverageVersionsPerItem { get; set; }
    /// <summary>JSON-serialised <c>Dictionary&lt;string,int&gt;</c> — month "yyyy-MM" → count.</summary>
    public string? CreationByMonthJson { get; set; }
    /// <summary>JSON-serialised list of <see cref="StaleContentSnapshotItem"/> ordered oldest-first, capped to 20.</summary>
    public string? StaleContentJson { get; set; }
    /// <summary>JSON-serialised list of <see cref="EditorActivitySnapshotItem"/> ordered by edit count desc, capped to 10.</summary>
    public string? TopEditorsJson { get; set; }
}

public class StaleContentSnapshotItem
{
    public int ContentId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string ContentTypeName { get; set; } = string.Empty;
    public DateTime LastModified { get; set; }
}

public class EditorActivitySnapshotItem
{
    public string Username { get; set; } = string.Empty;
    public int EditCount { get; set; }
    public int PublishCount { get; set; }
    public DateTime? LastActive { get; set; }
}

public class ContentDashboardSnapshotRepository
{
    public virtual ContentDashboardSnapshotRecord? GetCurrent()
    {
        var store = GetStore();
        return store.Items<ContentDashboardSnapshotRecord>().FirstOrDefault();
    }

    public virtual void Save(ContentDashboardSnapshotRecord record)
    {
        var store = GetStore();
        // Only one row is ever kept — overwrite if present, insert otherwise.
        var existing = store.Items<ContentDashboardSnapshotRecord>().FirstOrDefault();
        if (existing != null)
        {
            existing.LastUpdated = record.LastUpdated;
            existing.AverageVersionsPerItem = record.AverageVersionsPerItem;
            existing.CreationByMonthJson = record.CreationByMonthJson;
            existing.StaleContentJson = record.StaleContentJson;
            existing.TopEditorsJson = record.TopEditorsJson;
            store.Save(existing);
        }
        else
        {
            store.Save(record);
        }
    }

    public virtual void Clear()
    {
        var store = GetStore();
        store.DeleteAll();
    }

    private static DynamicDataStore GetStore() =>
        DynamicDataStoreFactory.Instance.CreateStore(typeof(ContentDashboardSnapshotRecord));
}
