using EPiServer;
using EPiServer.Core;
using EPiServer.DataAbstraction;
using EPiServer.Personalization.VisitorGroups;
using EPiServer.Shell;
using EditorPowertools.Tools.ContentDetails.Models;
using Microsoft.Extensions.Logging;

namespace EditorPowertools.Tools.ContentDetails;

public class ContentDetailsService
{
    private readonly IContentLoader _contentLoader;
    private readonly IContentVersionRepository _versionRepository;
    private readonly IContentTypeRepository _contentTypeRepository;
    private readonly IContentSoftLinkRepository _softLinkRepository;
    private readonly IVisitorGroupRepository _visitorGroupRepository;
    private readonly ILanguageBranchRepository _languageBranchRepository;
    private readonly ILogger<ContentDetailsService> _logger;

    private const int MaxTreeDepth = 5;
    private const int MaxReferences = 30;

    public ContentDetailsService(
        IContentLoader contentLoader,
        IContentVersionRepository versionRepository,
        IContentTypeRepository contentTypeRepository,
        IContentSoftLinkRepository softLinkRepository,
        IVisitorGroupRepository visitorGroupRepository,
        ILanguageBranchRepository languageBranchRepository,
        ILogger<ContentDetailsService> logger)
    {
        _contentLoader = contentLoader;
        _versionRepository = versionRepository;
        _contentTypeRepository = contentTypeRepository;
        _softLinkRepository = softLinkRepository;
        _visitorGroupRepository = visitorGroupRepository;
        _languageBranchRepository = languageBranchRepository;
        _logger = logger;
    }

    public ContentDetailsDto? GetDetails(int contentId)
    {
        var contentRef = new ContentReference(contentId);

        if (!_contentLoader.TryGet<IContent>(contentRef, out var content))
        {
            _logger.LogWarning("Content with ID {ContentId} not found", contentId);
            return null;
        }

        var contentType = _contentTypeRepository.Load(content.ContentTypeID);

        var dto = new ContentDetailsDto
        {
            ContentId = content.ContentLink.ID,
            Name = content.Name,
            ContentTypeName = contentType?.DisplayName ?? contentType?.Name ?? "Unknown",
            ContentGuid = content.ContentGuid.ToString()
        };

        if (content is IChangeTrackable trackable)
        {
            dto.CreatedBy = trackable.CreatedBy;
            dto.Created = trackable.Created;
            dto.ChangedBy = trackable.ChangedBy;
            dto.Changed = trackable.Changed;
        }

        if (content is IVersionable versionable)
        {
            dto.Status = versionable.Status.ToString();
            dto.Published = versionable.StartPublish;
        }

        if (content is ILocalizable localizable)
        {
            dto.Language = localizable.Language?.Name;
        }

        if (!ContentReference.IsNullOrEmpty(content.ParentLink))
        {
            try
            {
                if (_contentLoader.TryGet<IContent>(content.ParentLink, out var parent))
                    dto.ParentName = parent.Name;
            }
            catch { }
        }

        dto.Uses = GetOutgoingReferences(content);
        dto.UsedBy = GetIncomingReferences(contentRef);
        dto.ContentTree = BuildContentTree(content, 0);

        var versions = GetVersions(contentRef);
        dto.Versions = versions;
        dto.VersionCount = versions.Count;

        dto.Personalizations = GetPersonalizations(content, dto.ContentTree);
        dto.LanguageSync = GetLanguageSync(content);

        return dto;
    }

    /// <summary>
    /// Scans content properties for outgoing references (content areas, content references, URLs).
    /// </summary>
    private List<ContentUsageDto> GetOutgoingReferences(IContent content)
    {
        var uses = new List<ContentUsageDto>();
        var seen = new HashSet<int>();

        foreach (var prop in content.Property)
        {
            if (prop?.Value == null || string.IsNullOrEmpty(prop.Name))
                continue;

            if (prop.Value is ContentArea contentArea)
            {
                foreach (var item in contentArea.FilteredItems ?? Enumerable.Empty<ContentAreaItem>())
                {
                    if (ContentReference.IsNullOrEmpty(item.ContentLink) || !seen.Add(item.ContentLink.ID))
                        continue;

                    var dto = CreateUsageDto(item.ContentLink, prop.Name, "ContentArea");
                    if (dto != null) uses.Add(dto);
                }
            }
            else if (prop.Value is ContentReference refValue)
            {
                if (!ContentReference.IsNullOrEmpty(refValue) && seen.Add(refValue.ID))
                {
                    var dto = CreateUsageDto(refValue, prop.Name, "ContentReference");
                    if (dto != null) uses.Add(dto);
                }
            }
            else if (prop.Value is Url urlValue)
            {
                // Try to resolve URL to a content reference
                try
                {
                    var path = urlValue.ToString();
                    if (!string.IsNullOrEmpty(path) && path.Contains("episerverapi", StringComparison.OrdinalIgnoreCase) == false)
                    {
                        // Soft links will capture URL-based references, skip here
                    }
                }
                catch { }
            }
        }

        // Also check soft links for outgoing references we might have missed
        try
        {
            var softLinks = _softLinkRepository.Load(content.ContentLink, false);
            if (softLinks != null)
            {
                foreach (var link in softLinks.Where(sl =>
                    !ContentReference.IsNullOrEmpty(sl.ReferencedContentLink) &&
                    sl.OwnerContentLink.CompareToIgnoreWorkID(content.ContentLink)))
                {
                    if (!seen.Add(link.ReferencedContentLink.ID))
                        continue;

                    var dto = CreateUsageDto(link.ReferencedContentLink,
                        link.OwnerPropertyDefinition?.Name, "Link");
                    if (dto != null) uses.Add(dto);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load outgoing soft links for {ContentId}", content.ContentLink.ID);
        }

        return uses;
    }

    private ContentUsageDto? CreateUsageDto(ContentReference contentRef, string? propertyName, string referenceType)
    {
        try
        {
            if (!_contentLoader.TryGet<IContent>(contentRef, out var target))
                return new ContentUsageDto
                {
                    ContentId = contentRef.ID,
                    Name = $"[Content {contentRef.ID}]",
                    PropertyName = propertyName,
                    ReferenceType = referenceType
                };

            var type = _contentTypeRepository.Load(target.ContentTypeID);
            return new ContentUsageDto
            {
                ContentId = target.ContentLink.ID,
                Name = target.Name,
                ContentTypeName = type?.DisplayName ?? type?.Name ?? "Unknown",
                PropertyName = propertyName,
                ReferenceType = referenceType
            };
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Gets content items that reference this content (incoming references).
    /// </summary>
    private List<ContentReferenceDto> GetIncomingReferences(ContentReference contentRef)
    {
        var references = new List<ContentReferenceDto>();

        try
        {
            var softLinks = _softLinkRepository.Load(contentRef, true);
            if (softLinks == null)
                return references;

            foreach (var link in softLinks
                .Where(sl => !sl.OwnerContentLink.CompareToIgnoreWorkID(contentRef))
                .Take(MaxReferences))
            {
                var refDto = new ContentReferenceDto
                {
                    ContentId = link.OwnerContentLink.ID,
                    PropertyName = link.OwnerPropertyDefinition?.Name
                };

                try
                {
                    if (_contentLoader.TryGet<IContent>(link.OwnerContentLink, out var owner))
                    {
                        refDto.Name = owner.Name;
                        var ownerType = _contentTypeRepository.Load(owner.ContentTypeID);
                        refDto.ContentTypeName = ownerType?.DisplayName ?? ownerType?.Name ?? "Unknown";
                    }
                    else
                    {
                        refDto.Name = $"[Content {link.OwnerContentLink.ID}]";
                    }
                }
                catch
                {
                    refDto.Name = $"[Missing content {link.OwnerContentLink.ID}]";
                }

                references.Add(refDto);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load references for content {ContentId}", contentRef.ID);
        }

        return references;
    }

    /// <summary>
    /// Builds a recursive tree of content structure:
    /// content areas → blocks → nested content areas → nested blocks, etc.
    /// </summary>
    private ContentTreeNodeDto BuildContentTree(IContent content, int depth, HashSet<int>? visited = null)
    {
        visited ??= new HashSet<int>();
        visited.Add(content.ContentLink.ID);

        var contentType = _contentTypeRepository.Load(content.ContentTypeID);
        var node = new ContentTreeNodeDto
        {
            ContentId = content.ContentLink.ID,
            Name = content.Name,
            ContentTypeName = contentType?.DisplayName ?? contentType?.Name ?? "Unknown"
        };

        if (depth >= MaxTreeDepth)
            return node;

        // Scan properties for content areas and content references, grouped by property
        foreach (var prop in content.Property)
        {
            if (prop?.Value == null || string.IsNullOrEmpty(prop.Name))
                continue;

            if (prop.Value is ContentArea contentArea)
            {
                var items = (contentArea.FilteredItems ?? Enumerable.Empty<ContentAreaItem>()).ToList();
                if (items.Count == 0) continue;

                var propNode = new TreePropertyNodeDto
                {
                    PropertyName = prop.TranslateDisplayName() ?? prop.Name,
                    PropertyType = "ContentArea"
                };

                foreach (var item in items)
                {
                    if (ContentReference.IsNullOrEmpty(item.ContentLink) || visited.Contains(item.ContentLink.ID))
                        continue;
                    try
                    {
                        if (_contentLoader.TryGet<IContent>(item.ContentLink, out var child))
                        {
                            propNode.Children.Add(BuildContentTree(child, depth + 1, visited));
                        }
                    }
                    catch { }
                }

                if (propNode.Children.Count > 0)
                    node.Properties.Add(propNode);
            }
            else if (prop.Value is ContentReference refValue &&
                     !ContentReference.IsNullOrEmpty(refValue) &&
                     !visited.Contains(refValue.ID))
            {
                try
                {
                    if (_contentLoader.TryGet<IContent>(refValue, out var child))
                    {
                        var propNode = new TreePropertyNodeDto
                        {
                            PropertyName = prop.TranslateDisplayName() ?? prop.Name,
                            PropertyType = "ContentReference"
                        };
                        // Don't recurse into parent link — just show as leaf
                        var isLeafOnly = prop.Name.Equals("PageParentLink", StringComparison.OrdinalIgnoreCase);
                        if (isLeafOnly)
                        {
                            var childType = _contentTypeRepository.Load(child.ContentTypeID);
                            propNode.Children.Add(new ContentTreeNodeDto
                            {
                                ContentId = child.ContentLink.ID,
                                Name = child.Name,
                                ContentTypeName = childType?.DisplayName ?? childType?.Name ?? "Unknown"
                            });
                        }
                        else
                        {
                            propNode.Children.Add(BuildContentTree(child, depth + 1, visited));
                        }
                        node.Properties.Add(propNode);
                    }
                }
                catch { }
            }
        }

        return node;
    }

    /// <summary>
    /// Collects visitor groups used in personalized content areas on this content
    /// and recursively on sub-content (from the content tree).
    /// </summary>
    private List<PersonalizationInfoDto> GetPersonalizations(IContent content, ContentTreeNodeDto? tree)
    {
        var result = new List<PersonalizationInfoDto>();
        var allGroups = new Dictionary<Guid, VisitorGroup>();

        try
        {
            foreach (var vg in _visitorGroupRepository.List())
                allGroups[vg.Id] = vg;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load visitor groups");
            return result;
        }

        CollectPersonalizations(content, allGroups, result);

        // Also scan sub-content from the tree
        if (tree?.Properties != null)
        {
            foreach (var prop in tree.Properties)
                ScanTreeForPersonalizations(prop.Children, allGroups, result);
        }

        return result;
    }

    private void CollectPersonalizations(IContent content, Dictionary<Guid, VisitorGroup> allGroups, List<PersonalizationInfoDto> result)
    {
        foreach (var prop in content.Property)
        {
            if (prop?.Value is not ContentArea contentArea)
                continue;

            foreach (var item in contentArea.Items ?? Enumerable.Empty<ContentAreaItem>())
            {
                var allowedRoles = item.AllowedRoles;
                if (allowedRoles == null) continue;

                foreach (var role in allowedRoles)
                {
                    if (Guid.TryParse(role, out var vgId) && allGroups.TryGetValue(vgId, out var vg))
                    {
                        // Avoid duplicates for same group+content+property
                        if (!result.Any(r => r.VisitorGroupName == vg.Name && r.ContentId == content.ContentLink.ID && r.PropertyName == prop.Name))
                        {
                            result.Add(new PersonalizationInfoDto
                            {
                                VisitorGroupName = vg.Name,
                                ContentName = content.Name,
                                ContentId = content.ContentLink.ID,
                                PropertyName = prop.Name
                            });
                        }
                    }
                }
            }
        }
    }

    private void ScanTreeForPersonalizations(List<ContentTreeNodeDto> nodes, Dictionary<Guid, VisitorGroup> allGroups, List<PersonalizationInfoDto> result)
    {
        foreach (var node in nodes)
        {
            try
            {
                if (_contentLoader.TryGet<IContent>(new ContentReference(node.ContentId), out var child))
                {
                    CollectPersonalizations(child, allGroups, result);
                }
            }
            catch { }

            if (node.Properties?.Count > 0)
            {
                foreach (var prop in node.Properties)
                    ScanTreeForPersonalizations(prop.Children, allGroups, result);
            }
        }
    }

    /// <summary>
    /// Checks language versions to see which ones are behind the master language.
    /// </summary>
    private List<LanguageSyncDto> GetLanguageSync(IContent content)
    {
        var result = new List<LanguageSyncDto>();
        if (content is not ILocalizable localizable) return result;

        try
        {
            var masterLang = localizable.MasterLanguage?.Name;
            DateTime? masterChanged = null;

            // Get master version's change date
            if (content is IChangeTrackable masterTrackable && localizable.Language?.Name == masterLang)
            {
                masterChanged = masterTrackable.Changed;
            }

            var enabledLanguages = _languageBranchRepository.ListEnabled();

            foreach (var lang in enabledLanguages)
            {
                try
                {
                    var langRef = new ContentReference(content.ContentLink.ID);
                    var loaderOptions = new LoaderOptions { LanguageLoaderOption.Specific(lang.Culture) };
                    if (!_contentLoader.TryGet<IContent>(langRef, loaderOptions, out var langContent))
                        continue;

                    var isMaster = lang.LanguageID == masterLang;
                    DateTime? changed = null;
                    string? changedBy = null;
                    string status = "";

                    if (langContent is IChangeTrackable trackable)
                    {
                        changed = trackable.Changed;
                        changedBy = trackable.ChangedBy;
                    }
                    if (langContent is IVersionable versionable)
                    {
                        status = versionable.Status.ToString();
                    }

                    if (isMaster) masterChanged = changed;

                    result.Add(new LanguageSyncDto
                    {
                        Language = lang.LanguageID,
                        IsMaster = isMaster,
                        LastChanged = changed,
                        LastChangedBy = changedBy,
                        Status = status
                    });
                }
                catch { }
            }

            // Mark languages that are behind master
            if (masterChanged.HasValue)
            {
                foreach (var ls in result.Where(l => !l.IsMaster))
                {
                    ls.IsBehindMaster = !ls.LastChanged.HasValue || ls.LastChanged < masterChanged;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to check language sync for {ContentId}", content.ContentLink.ID);
        }

        return result;
    }

    private List<VersionSummaryDto> GetVersions(ContentReference contentRef)
    {
        var versions = new List<VersionSummaryDto>();

        try
        {
            var allVersions = _versionRepository.List(contentRef)
                .OrderByDescending(v => v.Saved)
                .Take(20)
                .ToList();

            // Load actual content for each version so we can diff properties
            var versionContents = new List<IContent?>();
            foreach (var ver in allVersions)
            {
                try
                {
                    _contentLoader.TryGet<IContent>(ver.ContentLink, out var vContent);
                    versionContents.Add(vContent);
                }
                catch
                {
                    versionContents.Add(null);
                }
            }

            for (var i = 0; i < allVersions.Count; i++)
            {
                var ver = allVersions[i];
                var dto = new VersionSummaryDto
                {
                    VersionId = ver.ContentLink.WorkID,
                    Status = ver.Status.ToString(),
                    Saved = ver.Saved,
                    SavedBy = ver.SavedBy,
                    Language = ver.LanguageBranch,
                    IsCommonDraft = ver.IsCommonDraft,
                    IsMasterLanguageBranch = ver.IsMasterLanguageBranch
                };

                // Diff against previous version (next in list since sorted descending)
                if (i + 1 < allVersions.Count && versionContents[i] != null && versionContents[i + 1] != null)
                {
                    dto.ChangedProperties = DiffProperties(versionContents[i + 1]!, versionContents[i]!);
                    dto.CompareUrl = $"{Paths.ToResource("CMS", "")}#context=epi.cms.contentdata:///{contentRef.ID}" +
                        $"&viewsetting=epi.cms.contentediting///compare/{allVersions[i + 1].ContentLink.WorkID}/{ver.ContentLink.WorkID}";
                }

                versions.Add(dto);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load versions for content {ContentId}", contentRef.ID);
        }

        return versions;
    }

    private static readonly HashSet<string> SystemPropertyNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "PageName", "PageLink", "PageParentLink", "PageGuid",
        "PageTypeID", "PageTypeName", "PageCreated", "PageChanged",
        "PageSaved", "PageLanguageBranch", "PageMasterLanguageBranch",
        "PageWorkStatus", "PageStartPublish", "PageStopPublish",
        "PageDeleted", "PageCreatedBy", "PageChangedBy",
        "PageDeletedBy", "PageDeletedDate", "PageShortcutType",
        "PageShortcutLink", "PageTargetFrame", "PageURLSegment",
        "PageExternalURL", "PagePendingPublish", "PageChangedOnPublish",
        "PageCategory", "PageArchiveLink", "PageFolderID",
        "PagePeerOrder", "PageChildOrderRule", "PageVisibleInMenu",
        "icontent_providerdefinitionid"
    };

    private List<PropertyChangeDto> DiffProperties(IContent older, IContent newer)
    {
        var changes = new List<PropertyChangeDto>();

        foreach (var newProp in newer.Property)
        {
            if (newProp == null || string.IsNullOrEmpty(newProp.Name))
                continue;
            if (SystemPropertyNames.Contains(newProp.Name))
                continue;

            var oldProp = older.Property[newProp.Name];
            var oldVal = Summarize(oldProp?.Value);
            var newVal = Summarize(newProp.Value);

            if (!string.Equals(oldVal, newVal, StringComparison.Ordinal))
            {
                changes.Add(new PropertyChangeDto
                {
                    PropertyName = newProp.TranslateDisplayName() ?? newProp.Name,
                    OldValue = Truncate(oldVal, 80),
                    NewValue = Truncate(newVal, 80)
                });
            }
        }

        return changes;
    }

    private static string? Summarize(object? value)
    {
        if (value == null) return null;
        if (value is ContentArea ca)
            return $"[{ca.FilteredItems?.Count() ?? 0} items]";
        var s = value.ToString();
        return string.IsNullOrEmpty(s) ? null : s;
    }

    private static string? Truncate(string? s, int max)
    {
        if (s == null || s.Length <= max) return s;
        return s[..max] + "…";
    }
}
