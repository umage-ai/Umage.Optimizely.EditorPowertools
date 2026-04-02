using EditorPowertools.Tools.SecurityAudit.Models;
using EPiServer.Data.Dynamic;

namespace EditorPowertools.Tools.SecurityAudit;

/// <summary>
/// Thin DDS wrapper for SecurityAuditRecord CRUD operations.
/// </summary>
public class SecurityAuditRepository
{
    public virtual void Clear()
    {
        var store = GetStore();
        store.DeleteAll();
    }

    public virtual void Save(SecurityAuditRecord record)
    {
        var store = GetStore();
        store.Save(record);
    }

    public virtual void SaveOrUpdate(SecurityAuditRecord record)
    {
        var store = GetStore();
        var existing = store.Items<SecurityAuditRecord>()
            .FirstOrDefault(r => r.ContentId == record.ContentId);

        if (existing != null)
        {
            existing.ContentName = record.ContentName;
            existing.ContentTypeName = record.ContentTypeName;
            existing.Breadcrumb = record.Breadcrumb;
            existing.ParentContentId = record.ParentContentId;
            existing.TreeDepth = record.TreeDepth;
            existing.IsPage = record.IsPage;
            existing.AclEntriesJson = record.AclEntriesJson;
            existing.IsInheriting = record.IsInheriting;
            existing.HasExplicitAcl = record.HasExplicitAcl;
            existing.HasNoRestrictions = record.HasNoRestrictions;
            existing.EveryoneCanPublish = record.EveryoneCanPublish;
            existing.EveryoneCanEdit = record.EveryoneCanEdit;
            existing.ChildMorePermissive = record.ChildMorePermissive;
            existing.IssueCount = record.IssueCount;
            existing.SubtreeIssueCount = record.SubtreeIssueCount;
            existing.LastUpdated = record.LastUpdated;
            store.Save(existing);
        }
        else
        {
            store.Save(record);
        }
    }

    public virtual IEnumerable<SecurityAuditRecord> GetAll()
    {
        var store = GetStore();
        return store.Items<SecurityAuditRecord>().ToList();
    }

    public virtual IEnumerable<SecurityAuditRecord> GetByParent(int parentContentId)
    {
        var store = GetStore();
        return store.Items<SecurityAuditRecord>()
            .Where(r => r.ParentContentId == parentContentId)
            .ToList();
    }

    public virtual SecurityAuditRecord? GetByContentId(int contentId)
    {
        var store = GetStore();
        return store.Items<SecurityAuditRecord>()
            .FirstOrDefault(r => r.ContentId == contentId);
    }

    public virtual IEnumerable<SecurityAuditRecord> GetByRoleOrUser(string name)
    {
        var store = GetStore();
        // AclEntriesJson contains the name as a JSON string value, so simple contains check
        return store.Items<SecurityAuditRecord>()
            .Where(r => r.AclEntriesJson.Contains(name))
            .ToList();
    }

    public virtual IEnumerable<SecurityAuditRecord> GetWithIssues()
    {
        var store = GetStore();
        return store.Items<SecurityAuditRecord>()
            .Where(r => r.IssueCount > 0)
            .ToList();
    }

    private static DynamicDataStore GetStore()
    {
        return DynamicDataStoreFactory.Instance.CreateStore(typeof(SecurityAuditRecord));
    }
}
