using EPiServer.Data;
using EPiServer.Data.Dynamic;

namespace UmageAI.Optimizely.EditorPowerTools.Tools.SecurityAudit.Models;

/// <summary>
/// DDS-persisted record holding the ACL snapshot and issue flags for a single content item.
/// One record per content item. Populated by SecurityAuditAnalyzer during the unified job.
/// </summary>
[EPiServerDataStore(AutomaticallyCreateStore = true, AutomaticallyRemapStore = true, StoreName = "EditorPowertools_SecurityAudit")]
public class SecurityAuditRecord : IDynamicData
{
    public Identity Id { get; set; } = Identity.NewIdentity();

    // Content identification
    public int ContentId { get; set; }
    public string ContentName { get; set; } = string.Empty;
    public string? ContentTypeName { get; set; }
    public string? Breadcrumb { get; set; }
    public int ParentContentId { get; set; }
    public int TreeDepth { get; set; }
    public bool IsPage { get; set; }

    // Serialized ACL — compact JSON array of entries
    // Format: [{"Name":"Everyone","EntityType":"Role","Access":"Read"},...]
    public string AclEntriesJson { get; set; } = "[]";

    // Inheritance
    public bool IsInheriting { get; set; }
    public bool HasExplicitAcl { get; set; }

    // Pre-computed issue flags (set by analyzer during traversal)
    public bool HasNoRestrictions { get; set; }
    public bool EveryoneCanPublish { get; set; }
    public bool EveryoneCanEdit { get; set; }
    public bool ChildMorePermissive { get; set; }
    public int IssueCount { get; set; }

    /// <summary>
    /// Aggregate count of issues in the entire subtree below this node.
    /// Computed during Complete() phase.
    /// </summary>
    public int SubtreeIssueCount { get; set; }

    public DateTime LastUpdated { get; set; }
}
