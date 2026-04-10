using EPiServer.Data;
using EPiServer.Data.Dynamic;

namespace UmageAI.Optimizely.EditorPowerTools.Tools.LanguageAudit;

/// <summary>
/// DDS-persisted record holding language version information for a single content item.
/// Updated by the LanguageAuditAnalyzer during the unified scheduled job traversal.
/// </summary>
[EPiServerDataStore(AutomaticallyCreateStore = true, AutomaticallyRemapStore = true, StoreName = "UmageAI.Optimizely.EditorPowerTools_LanguageAudit")]
public class LanguageAuditRecord : IDynamicData
{
    public Identity Id { get; set; } = Identity.NewIdentity();
    public int ContentId { get; set; }
    public string ContentName { get; set; } = string.Empty;
    public string? ContentTypeName { get; set; }
    public string? Breadcrumb { get; set; }
    public int ParentContentId { get; set; }
    public string MasterLanguage { get; set; } = string.Empty;

    /// <summary>Comma-separated list of available language codes.</summary>
    public string AvailableLanguages { get; set; } = string.Empty;

    /// <summary>
    /// JSON array of language details:
    /// [{"lang":"en","status":"Published","lastModified":"2024-01-15T10:30:00Z"},...]
    /// </summary>
    public string LanguageDetailsJson { get; set; } = "[]";

    public bool IsMissingTranslations { get; set; }
    public int StalestTranslationDays { get; set; }
    public string? EditUrl { get; set; }
}

/// <summary>
/// Repository for reading/writing language audit records from DDS.
/// </summary>
public class LanguageAuditRepository
{
    public IEnumerable<LanguageAuditRecord> GetAll()
    {
        var store = GetStore();
        return store.Items<LanguageAuditRecord>().ToList();
    }

    public IEnumerable<LanguageAuditRecord> GetByParent(int parentContentId)
    {
        var store = GetStore();
        return store.Items<LanguageAuditRecord>()
            .Where(r => r.ParentContentId == parentContentId)
            .ToList();
    }

    public void Clear()
    {
        var store = GetStore();
        store.DeleteAll();
    }

    public void Save(LanguageAuditRecord record)
    {
        var store = GetStore();
        store.Save(record);
    }

    private static DynamicDataStore GetStore()
    {
        return DynamicDataStoreFactory.Instance.CreateStore(typeof(LanguageAuditRecord));
    }
}
