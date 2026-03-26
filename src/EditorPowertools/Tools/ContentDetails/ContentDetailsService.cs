using EPiServer;
using EPiServer.Core;
using EPiServer.DataAbstraction;
using EditorPowertools.Tools.ContentDetails.Models;
using Microsoft.Extensions.Logging;

namespace EditorPowertools.Tools.ContentDetails;

public class ContentDetailsService
{
    private readonly IContentLoader _contentLoader;
    private readonly IContentVersionRepository _versionRepository;
    private readonly IContentTypeRepository _contentTypeRepository;
    private readonly IContentSoftLinkRepository _softLinkRepository;
    private readonly ILogger<ContentDetailsService> _logger;

    // System property names to exclude from the properties tab
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

    public ContentDetailsService(
        IContentLoader contentLoader,
        IContentVersionRepository versionRepository,
        IContentTypeRepository contentTypeRepository,
        IContentSoftLinkRepository softLinkRepository,
        ILogger<ContentDetailsService> logger)
    {
        _contentLoader = contentLoader;
        _versionRepository = versionRepository;
        _contentTypeRepository = contentTypeRepository;
        _softLinkRepository = softLinkRepository;
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

        // IChangeTrackable data
        if (content is IChangeTrackable trackable)
        {
            dto.CreatedBy = trackable.CreatedBy;
            dto.Created = trackable.Created;
            dto.ChangedBy = trackable.ChangedBy;
            dto.Changed = trackable.Changed;
        }

        // IVersionable data
        if (content is IVersionable versionable)
        {
            dto.Status = versionable.Status.ToString();
            dto.Published = versionable.StartPublish;
        }

        // Language
        if (content is ILocalizable localizable)
        {
            dto.Language = localizable.Language?.Name;
        }

        // Parent info
        if (!ContentReference.IsNullOrEmpty(content.ParentLink))
        {
            try
            {
                if (_contentLoader.TryGet<IContent>(content.ParentLink, out var parent))
                {
                    dto.ParentName = parent.Name;
                }
            }
            catch
            {
                // Parent may not be accessible
            }
        }

        // Properties summary
        dto.Properties = GetPropertySummary(content);

        // Versions
        var versions = GetVersions(contentRef);
        dto.Versions = versions;
        dto.VersionCount = versions.Count;

        // References (who links to this content)
        dto.ReferencedBy = GetReferences(contentRef);

        return dto;
    }

    private List<PropertySummaryDto> GetPropertySummary(IContent content)
    {
        var properties = new List<PropertySummaryDto>();

        foreach (var prop in content.Property)
        {
            if (prop == null || string.IsNullOrEmpty(prop.Name))
                continue;

            // Skip system properties
            if (SystemPropertyNames.Contains(prop.Name))
                continue;

            var summary = new PropertySummaryDto
            {
                Name = prop.Name,
                DisplayName = prop.TranslateDisplayName() ?? prop.Name,
                TypeName = prop.Type.ToString()
            };

            if (prop.Value is ContentArea contentArea)
            {
                summary.IsContentArea = true;
                summary.ItemCount = contentArea.FilteredItems?.Count() ?? 0;
            }
            else if (prop.Value != null)
            {
                var valueStr = prop.Value.ToString();
                // Truncate long values for display
                if (valueStr != null && valueStr.Length > 100)
                {
                    summary.Value = valueStr[..100] + "...";
                }
                else
                {
                    summary.Value = valueStr;
                }
            }

            properties.Add(summary);
        }

        return properties;
    }

    private List<VersionSummaryDto> GetVersions(ContentReference contentRef)
    {
        var versions = new List<VersionSummaryDto>();

        try
        {
            var allVersions = _versionRepository.List(contentRef)
                .OrderByDescending(v => v.Saved)
                .Take(10);

            foreach (var ver in allVersions)
            {
                versions.Add(new VersionSummaryDto
                {
                    VersionId = ver.ContentLink.WorkID,
                    Status = ver.Status.ToString(),
                    Saved = ver.Saved,
                    SavedBy = ver.SavedBy
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load versions for content {ContentId}", contentRef.ID);
        }

        return versions;
    }

    private List<ContentReferenceDto> GetReferences(ContentReference contentRef)
    {
        var references = new List<ContentReferenceDto>();

        try
        {
            var softLinks = _softLinkRepository.Load(contentRef, true);
            if (softLinks == null)
                return references;

            foreach (var link in softLinks
                .Where(sl => !sl.OwnerContentLink.CompareToIgnoreWorkID(contentRef))
                .Take(20))
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
}
