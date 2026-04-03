using System.Text.Json;
using EPiServer.Core;
using EPiServer.DataAbstraction;
using EPiServer.Framework.Localization;
using EPiServer.Security;
using EditorPowertools.Helpers;
using EditorPowertools.Tools.SecurityAudit;
using EditorPowertools.Tools.SecurityAudit.Models;
using Microsoft.Extensions.Logging;

namespace EditorPowertools.Services.Analyzers;

/// <summary>
/// Analyzer that reads ACLs from IContentSecurityRepository for every content item,
/// detects security issues, and saves SecurityAuditRecord to DDS.
/// </summary>
public class SecurityAuditAnalyzer : IContentAnalyzer
{
    private readonly IContentSecurityRepository _contentSecurityRepository;
    private readonly IContentTypeRepository _contentTypeRepository;
    private readonly SecurityAuditRepository _repository;
    private readonly ILogger<SecurityAuditAnalyzer> _logger;

    /// <summary>
    /// Cache of parent ACLs: content ID -> set of "entityName:accessLevel" strings.
    /// Used for ChildMorePermissive detection.
    /// </summary>
    private readonly Dictionary<int, HashSet<string>> _parentAclCache = new();

    /// <summary>
    /// Cache of parent HasNoRestrictions flag for inheritance chain detection.
    /// </summary>
    private readonly Dictionary<int, bool> _parentNoRestrictionsCache = new();

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly LocalizationService _localizationService;

    public string Name => _localizationService.GetString("/editorpowertools/analyzers/securityaudit");

    public SecurityAuditAnalyzer(
        IContentSecurityRepository contentSecurityRepository,
        IContentTypeRepository contentTypeRepository,
        SecurityAuditRepository repository,
        LocalizationService localizationService,
        ILogger<SecurityAuditAnalyzer> logger)
    {
        _contentSecurityRepository = contentSecurityRepository;
        _contentTypeRepository = contentTypeRepository;
        _repository = repository;
        _localizationService = localizationService;
        _logger = logger;
    }

    public void Initialize()
    {
        _parentAclCache.Clear();
        _parentNoRestrictionsCache.Clear();
        _repository.Clear();
    }

    public void Analyze(IContent content, ContentReference contentRef)
    {
        try
        {
            var contentType = _contentTypeRepository.Load(content.ContentTypeID);
            var contentTypeName = contentType?.DisplayName ?? contentType?.Name;
            var breadcrumb = content.GetBreadcrumb();
            var parentId = content.ParentLink?.ID ?? 0;
            var isPage = content is PageData;

            // Compute tree depth from breadcrumb (count separators)
            var treeDepth = breadcrumb.Split(" / ").Length - 1;

            // Read ACL
            var descriptor = _contentSecurityRepository.Get(contentRef);

            var entries = new List<AclEntryDto>();
            var aclSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            bool isInheriting = true;
            bool hasExplicitAcl = false;
            bool hasNoRestrictions = false;
            bool everyoneCanPublish = false;
            bool everyoneCanEdit = false;
            bool childMorePermissive = false;

            if (descriptor != null)
            {
                isInheriting = descriptor.IsInherited;
                hasExplicitAcl = !descriptor.IsInherited || descriptor.Entries.Any();

                foreach (var entry in descriptor.Entries)
                {
                    var entityType = entry.EntityType switch
                    {
                        SecurityEntityType.Role => "Role",
                        SecurityEntityType.User => "User",
                        SecurityEntityType.VisitorGroup => "VisitorGroup",
                        _ => entry.EntityType.ToString()
                    };

                    var accessStr = entry.Access.ToString();

                    entries.Add(new AclEntryDto
                    {
                        Name = entry.Name,
                        EntityType = entityType,
                        Access = accessStr
                    });

                    aclSet.Add($"{entry.Name}:{accessStr}");

                    // Check "Everyone" role permissions
                    if (string.Equals(entry.Name, "Everyone", StringComparison.OrdinalIgnoreCase))
                    {
                        if (entry.Access.HasFlag(AccessLevel.Publish) ||
                            entry.Access.HasFlag(AccessLevel.FullAccess) ||
                            entry.Access.HasFlag(AccessLevel.Administer))
                        {
                            everyoneCanPublish = true;
                        }

                        if (entry.Access.HasFlag(AccessLevel.Edit) ||
                            entry.Access.HasFlag(AccessLevel.Publish) ||
                            entry.Access.HasFlag(AccessLevel.FullAccess) ||
                            entry.Access.HasFlag(AccessLevel.Administer))
                        {
                            everyoneCanEdit = true;
                        }
                    }
                }

                // HasNoRestrictions: no explicit ACL or ACL grants Read to Everyone
                // and parent also has no restrictions (or this is inheriting from unrestricted parent)
                if (!hasExplicitAcl)
                {
                    hasNoRestrictions = !_parentNoRestrictionsCache.ContainsKey(parentId)
                                        || _parentNoRestrictionsCache.GetValueOrDefault(parentId, true);
                }
                else
                {
                    // Check if the only entry is Everyone:Read (effectively open)
                    var everyoneEntries = descriptor.Entries
                        .Where(e => string.Equals(e.Name, "Everyone", StringComparison.OrdinalIgnoreCase))
                        .ToList();
                    if (everyoneEntries.Any(e => e.Access.HasFlag(AccessLevel.Read)) &&
                        descriptor.Entries.Count() == everyoneEntries.Count)
                    {
                        hasNoRestrictions = true;
                    }
                }

                // ChildMorePermissive: compare against parent's cached ACL
                if (_parentAclCache.TryGetValue(parentId, out var parentAcl))
                {
                    childMorePermissive = IsMorePermissive(aclSet, parentAcl);
                }
            }
            else
            {
                // No security descriptor — effectively unrestricted
                hasNoRestrictions = true;
            }

            // Count issues on this node
            int issueCount = 0;
            if (everyoneCanPublish) issueCount++;
            if (everyoneCanEdit) issueCount++;
            if (childMorePermissive) issueCount++;
            if (hasNoRestrictions && isPage) issueCount++;

            var aclJson = JsonSerializer.Serialize(entries, JsonOptions);

            var record = new SecurityAuditRecord
            {
                ContentId = contentRef.ID,
                ContentName = content.Name,
                ContentTypeName = contentTypeName,
                Breadcrumb = breadcrumb,
                ParentContentId = parentId,
                TreeDepth = treeDepth,
                IsPage = isPage,
                AclEntriesJson = aclJson,
                IsInheriting = isInheriting,
                HasExplicitAcl = hasExplicitAcl,
                HasNoRestrictions = hasNoRestrictions,
                EveryoneCanPublish = everyoneCanPublish,
                EveryoneCanEdit = everyoneCanEdit,
                ChildMorePermissive = childMorePermissive,
                IssueCount = issueCount,
                SubtreeIssueCount = 0, // computed in Complete()
                LastUpdated = DateTime.UtcNow
            };

            _repository.Save(record);

            // Cache this node's ACL and restriction status for child comparison
            _parentAclCache[contentRef.ID] = aclSet;
            _parentNoRestrictionsCache[contentRef.ID] = hasNoRestrictions;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error analyzing security for content {ContentRef}", contentRef);
        }
    }

    public void Complete()
    {
        try
        {
            // Compute SubtreeIssueCount aggregates by walking records bottom-up
            var allRecords = _repository.GetAll().ToList();
            var byId = allRecords.ToDictionary(r => r.ContentId);
            var childrenByParent = allRecords.GroupBy(r => r.ParentContentId)
                .ToDictionary(g => g.Key, g => g.ToList());

            // Compute subtree issue counts via recursive aggregation
            var subtreeCounts = new Dictionary<int, int>();

            int ComputeSubtreeIssues(int contentId)
            {
                if (subtreeCounts.TryGetValue(contentId, out var cached))
                    return cached;

                int count = 0;
                if (childrenByParent.TryGetValue(contentId, out var children))
                {
                    foreach (var child in children)
                    {
                        count += child.IssueCount + ComputeSubtreeIssues(child.ContentId);
                    }
                }

                subtreeCounts[contentId] = count;
                return count;
            }

            foreach (var record in allRecords)
            {
                var subtreeCount = ComputeSubtreeIssues(record.ContentId);
                if (subtreeCount > 0)
                {
                    record.SubtreeIssueCount = subtreeCount;
                    _repository.SaveOrUpdate(record);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error computing subtree issue counts for Security Audit");
        }
    }

    /// <summary>
    /// Determines if the child ACL set is more permissive than the parent.
    /// A child is more permissive if it grants access to an entity not in the parent,
    /// or grants a higher access level to the same entity.
    /// </summary>
    private static bool IsMorePermissive(HashSet<string> childAcl, HashSet<string> parentAcl)
    {
        foreach (var entry in childAcl)
        {
            if (!parentAcl.Contains(entry))
            {
                // Child has an entry not in parent — could be more permissive
                var parts = entry.Split(':', 2);
                if (parts.Length != 2) continue;

                var entityName = parts[0];
                // Check if parent has same entity with different (possibly lower) access
                var parentEntry = parentAcl.FirstOrDefault(p =>
                    p.StartsWith(entityName + ":", StringComparison.OrdinalIgnoreCase));

                if (parentEntry == null)
                {
                    // Entity not in parent at all — child is more permissive
                    return true;
                }

                // Both have the entity, compare access levels
                var childAccess = ParseAccessLevel(parts[1]);
                var parentAccess = ParseAccessLevel(parentEntry.Split(':', 2)[1]);

                if (childAccess > parentAccess)
                    return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Converts an access level string to a numeric value for comparison.
    /// Higher values mean more permissions.
    /// </summary>
    private static int ParseAccessLevel(string access)
    {
        // AccessLevel is a flags enum; parse and compute a rough "permissiveness" score
        if (Enum.TryParse<AccessLevel>(access, true, out var level))
        {
            if (level.HasFlag(AccessLevel.Administer) || level.HasFlag(AccessLevel.FullAccess))
                return 4;
            if (level.HasFlag(AccessLevel.Publish))
                return 3;
            if (level.HasFlag(AccessLevel.Edit) || level.HasFlag(AccessLevel.Create) || level.HasFlag(AccessLevel.Delete))
                return 2;
            if (level.HasFlag(AccessLevel.Read))
                return 1;
        }
        return 0;
    }
}
