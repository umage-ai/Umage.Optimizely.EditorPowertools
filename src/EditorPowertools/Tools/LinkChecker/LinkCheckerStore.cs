using EPiServer.Data;
using EPiServer.Data.Dynamic;

namespace EditorPowertools.Tools.LinkChecker;

/// <summary>
/// DDS-persisted record holding a single link check result.
/// Updated by the scheduled job that scans content for links.
/// </summary>
[EPiServerDataStore(AutomaticallyCreateStore = true, AutomaticallyRemapStore = true, StoreName = "EditorPowertools_LinkChecker")]
public class LinkCheckRecord : IDynamicData
{
    public Identity Id { get; set; } = Identity.NewIdentity();
    public int ContentId { get; set; }
    public string ContentName { get; set; } = string.Empty;
    public string? ContentTypeName { get; set; }
    public string PropertyName { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    /// <summary>Resolved friendly URL for display (e.g. "/en/about-us/" instead of "/link/abc.aspx").</summary>
    public string? FriendlyUrl { get; set; }
    /// <summary>For internal links, the target content ID (for linking to edit mode).</summary>
    public int? TargetContentId { get; set; }
    public string LinkType { get; set; } = string.Empty; // "Internal", "External"
    public int StatusCode { get; set; }
    public string StatusText { get; set; } = string.Empty; // "OK", "Not Found", "Timeout", etc.
    public bool IsValid { get; set; }
    public string? Breadcrumb { get; set; }
    public string? EditUrl { get; set; }
    /// <summary>For blocks: comma-separated list of page names where this block is used.</summary>
    public string? UsedOn { get; set; }
    /// <summary>For blocks: comma-separated list of page edit URLs.</summary>
    public string? UsedOnEditUrls { get; set; }
    public DateTime LastChecked { get; set; }
}

/// <summary>
/// Repository for reading/writing link check records from DDS.
/// </summary>
public class LinkCheckerRepository
{
    public virtual IEnumerable<LinkCheckRecord> GetAll()
    {
        var store = GetStore();
        return store.Items<LinkCheckRecord>().ToList();
    }

    public virtual IEnumerable<LinkCheckRecord> GetByStatus(bool isValid)
    {
        var store = GetStore();
        return store.Items<LinkCheckRecord>()
            .Where(r => r.IsValid == isValid)
            .ToList();
    }

    public virtual void Clear()
    {
        var store = GetStore();
        store.DeleteAll();
    }

    public virtual void Save(LinkCheckRecord record)
    {
        var store = GetStore();
        store.Save(record);
    }

    public virtual int GetBrokenCount()
    {
        var store = GetStore();
        return store.Items<LinkCheckRecord>().Count(r => !r.IsValid);
    }

    public virtual int GetTotalCount()
    {
        var store = GetStore();
        return store.Items<LinkCheckRecord>().Count();
    }

    private static DynamicDataStore GetStore()
    {
        return DynamicDataStoreFactory.Instance.CreateStore(typeof(LinkCheckRecord));
    }
}
