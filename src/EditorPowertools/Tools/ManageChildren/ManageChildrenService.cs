using EPiServer;
using EPiServer.Core;
using EPiServer.DataAbstraction;
using EPiServer.DataAccess;
using EPiServer.Security;
using EPiServer.Shell;
using UmageAI.Optimizely.EditorPowerTools.Tools.ManageChildren.Models;
using Microsoft.Extensions.Logging;

namespace UmageAI.Optimizely.EditorPowerTools.Tools.ManageChildren;

public class ManageChildrenService
{
    private readonly IContentRepository _contentRepository;
    private readonly IContentLoader _contentLoader;
    private readonly IContentTypeRepository _contentTypeRepository;
    private readonly ILogger<ManageChildrenService> _logger;

    public ManageChildrenService(
        IContentRepository contentRepository,
        IContentLoader contentLoader,
        IContentTypeRepository contentTypeRepository,
        ILogger<ManageChildrenService> logger)
    {
        _contentRepository = contentRepository;
        _contentLoader = contentLoader;
        _contentTypeRepository = contentTypeRepository;
        _logger = logger;
    }

    public List<ChildItemDto> GetChildren(int parentId, string? sortBy = null, bool sortDesc = false)
    {
        var parentRef = parentId == 0 ? ContentReference.RootPage : new ContentReference(parentId);
        var children = _contentLoader.GetChildren<IContent>(
            parentRef,
            new LoaderOptions { LanguageLoaderOption.FallbackWithMaster() });

        var cmsPath = Paths.ToResource("CMS", "");

        var items = children.Select(c =>
        {
            var ct = _contentTypeRepository.Load(c.ContentTypeID);
            var dto = new ChildItemDto
            {
                ContentId = c.ContentLink.ID,
                Name = c.Name,
                ContentTypeName = ct?.DisplayName ?? ct?.Name ?? "Unknown",
                EditUrl = $"{cmsPath}#context=epi.cms.contentdata:///{c.ContentLink.ID}"
            };

            if (c is IVersionable v)
                dto.Status = v.Status.ToString();
            if (c is IChangeTrackable t)
            {
                dto.Changed = t.Changed;
                dto.ChangedBy = t.ChangedBy;
            }
            if (c is ILocalizable l)
                dto.Language = l.Language?.Name;
            if (c is PageData p)
                dto.SortIndex = p.Property["PagePeerOrder"]?.Value as int? ?? 0;

            dto.HasChildren = _contentLoader.GetChildren<IContent>(c.ContentLink,
                new LoaderOptions { LanguageLoaderOption.FallbackWithMaster() }).Any();

            return dto;
        }).ToList();

        // Sort
        items = (sortBy?.ToLowerInvariant()) switch
        {
            "name" => sortDesc ? items.OrderByDescending(i => i.Name).ToList() : items.OrderBy(i => i.Name).ToList(),
            "type" => sortDesc ? items.OrderByDescending(i => i.ContentTypeName).ToList() : items.OrderBy(i => i.ContentTypeName).ToList(),
            "status" => sortDesc ? items.OrderByDescending(i => i.Status).ToList() : items.OrderBy(i => i.Status).ToList(),
            "changed" => sortDesc ? items.OrderByDescending(i => i.Changed).ToList() : items.OrderBy(i => i.Changed).ToList(),
            _ => items // default: CMS sort order
        };

        return items;
    }

    public ChildItemDto? GetParentInfo(int contentId)
    {
        var contentRef = contentId == 0 ? ContentReference.RootPage : new ContentReference(contentId);
        if (!_contentLoader.TryGet<IContent>(contentRef, out var content)) return null;

        var ct = _contentTypeRepository.Load(content.ContentTypeID);
        return new ChildItemDto
        {
            ContentId = content.ContentLink.ID,
            Name = content.Name,
            ContentTypeName = ct?.DisplayName ?? ct?.Name ?? "Unknown",
            HasChildren = _contentLoader.GetChildren<IContent>(content.ContentLink,
                new LoaderOptions { LanguageLoaderOption.FallbackWithMaster() }).Any()
        };
    }

    public BulkActionResult BulkDelete(List<int> contentIds)
    {
        var result = new BulkActionResult();
        foreach (var id in contentIds)
        {
            try
            {
                _contentRepository.Delete(new ContentReference(id), true, AccessLevel.Delete);
                result.Succeeded++;
            }
            catch (Exception ex)
            {
                result.Failed++;
                result.Errors.Add($"ID {id}: {ex.Message}");
                _logger.LogWarning(ex, "Failed to delete content {ContentId}", id);
            }
        }
        return result;
    }

    public BulkActionResult BulkMoveToTrash(List<int> contentIds)
    {
        var result = new BulkActionResult();
        foreach (var id in contentIds)
        {
            try
            {
                _contentRepository.MoveToWastebasket(new ContentReference(id), "UmageAI.Optimizely.EditorPowerTools");
                result.Succeeded++;
            }
            catch (Exception ex)
            {
                result.Failed++;
                result.Errors.Add($"ID {id}: {ex.Message}");
                _logger.LogWarning(ex, "Failed to trash content {ContentId}", id);
            }
        }
        return result;
    }

    public BulkActionResult BulkPublish(List<int> contentIds)
    {
        var result = new BulkActionResult();
        foreach (var id in contentIds)
        {
            try
            {
                var content = _contentRepository.Get<IContent>(new ContentReference(id));
                var writable = (IContent)((ContentData)content).CreateWritableClone();
                _contentRepository.Save(writable, SaveAction.Publish, AccessLevel.Publish);
                result.Succeeded++;
            }
            catch (Exception ex)
            {
                result.Failed++;
                result.Errors.Add($"ID {id}: {ex.Message}");
                _logger.LogWarning(ex, "Failed to publish content {ContentId}", id);
            }
        }
        return result;
    }

    public BulkActionResult BulkUnpublish(List<int> contentIds)
    {
        var result = new BulkActionResult();
        foreach (var id in contentIds)
        {
            try
            {
                var content = _contentRepository.Get<IContent>(new ContentReference(id));
                if (content is IVersionable versionable)
                {
                    var writable = (IContent)((ContentData)content).CreateWritableClone();
                    ((IVersionable)writable).StopPublish = DateTime.Now;
                    _contentRepository.Save(writable, SaveAction.Save, AccessLevel.Publish);
                    result.Succeeded++;
                }
            }
            catch (Exception ex)
            {
                result.Failed++;
                result.Errors.Add($"ID {id}: {ex.Message}");
                _logger.LogWarning(ex, "Failed to unpublish content {ContentId}", id);
            }
        }
        return result;
    }

    public BulkActionResult BulkMove(List<int> contentIds, int targetParentId)
    {
        var result = new BulkActionResult();
        var targetRef = new ContentReference(targetParentId);
        foreach (var id in contentIds)
        {
            try
            {
                _contentRepository.Move(new ContentReference(id), targetRef, AccessLevel.Read, AccessLevel.Publish);
                result.Succeeded++;
            }
            catch (Exception ex)
            {
                result.Failed++;
                result.Errors.Add($"ID {id}: {ex.Message}");
                _logger.LogWarning(ex, "Failed to move content {ContentId} to {TargetId}", id, targetParentId);
            }
        }
        return result;
    }
}
