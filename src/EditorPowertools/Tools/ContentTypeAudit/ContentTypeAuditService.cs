using System.Reflection;
using EPiServer;
using EPiServer.Core;
using EPiServer.DataAbstraction;
using EPiServer.Shell;
using EPiServer.Web.Routing;
using UmageAI.Optimizely.EditorPowerTools.Helpers;
using UmageAI.Optimizely.EditorPowerTools.Services;
using UmageAI.Optimizely.EditorPowerTools.Tools.ContentTypeAudit.Models;
using Microsoft.Extensions.Logging;

namespace UmageAI.Optimizely.EditorPowerTools.Tools.ContentTypeAudit;

public class ContentTypeAuditService
{
    private readonly IContentTypeRepository _contentTypeRepository;
    private readonly IContentModelUsage _contentModelUsage;
    private readonly IContentLoader _contentLoader;
    private readonly IContentSoftLinkRepository _softLinkRepository;
    private readonly IPropertyDefinitionRepository _propertyDefinitionRepository;
    private readonly ContentTypeStatisticsRepository _statisticsRepository;
    private readonly ILogger<ContentTypeAuditService> _logger;

    public ContentTypeAuditService(
        IContentTypeRepository contentTypeRepository,
        IContentModelUsage contentModelUsage,
        IContentLoader contentLoader,
        IContentSoftLinkRepository softLinkRepository,
        IPropertyDefinitionRepository propertyDefinitionRepository,
        ContentTypeStatisticsRepository statisticsRepository,
        ILogger<ContentTypeAuditService> logger)
    {
        _contentTypeRepository = contentTypeRepository;
        _contentModelUsage = contentModelUsage;
        _contentLoader = contentLoader;
        _softLinkRepository = softLinkRepository;
        _propertyDefinitionRepository = propertyDefinitionRepository;
        _statisticsRepository = statisticsRepository;
        _logger = logger;
    }

    /// <summary>
    /// Gets all content types with their statistics from DDS.
    /// </summary>
    public IEnumerable<ContentTypeDto> GetAllContentTypes()
    {
        var contentTypes = _contentTypeRepository.List().ToList();
        var statistics = _statisticsRepository.GetAll()
            .ToDictionary(s => s.ContentTypeId);

        return contentTypes.Select(ct =>
        {
            statistics.TryGetValue(ct.ID, out var stats);
            return MapToDto(ct, stats);
        });
    }

    /// <summary>
    /// Gets properties for a content type with origin information.
    /// </summary>
    public IEnumerable<PropertyDefinitionDto> GetProperties(int contentTypeId)
    {
        var contentType = _contentTypeRepository.Load(contentTypeId);
        if (contentType == null)
            return Enumerable.Empty<PropertyDefinitionDto>();

        // Determine inherited property names via reflection
        var inheritedNames = new HashSet<string>();
        if (contentType.ModelType != null)
        {
            var props = contentType.ModelType.GetProperties(
                BindingFlags.Instance | BindingFlags.Public);
            inheritedNames = props
                .Where(p => p.DeclaringType != contentType.ModelType)
                .Select(p => p.Name)
                .ToHashSet();
        }

        return contentType.PropertyDefinitions
            .OrderBy(pd => pd.Tab?.Name)
            .ThenBy(pd => pd.FieldOrder)
            .Select(pd =>
            {
                PropertyOrigin origin;
                if (!pd.ExistsOnModel)
                    origin = PropertyOrigin.Orphaned;
                else if (inheritedNames.Contains(pd.Name))
                    origin = PropertyOrigin.Inherited;
                else
                    origin = PropertyOrigin.Defined;

                return new PropertyDefinitionDto
                {
                    Id = pd.ID,
                    Name = pd.Name,
                    TypeName = pd.Type?.Name ?? "Unknown",
                    TabName = pd.Tab?.Name,
                    SortOrder = pd.FieldOrder,
                    Required = pd.Required,
                    Searchable = pd.Searchable,
                    LanguageSpecific = pd.LanguageSpecific,
                    ExistsOnModel = pd.ExistsOnModel,
                    Origin = origin
                };
            });
    }

    /// <summary>
    /// Gets content items of a specific content type.
    /// </summary>
    public IEnumerable<ContentUsageDto> GetContentOfType(int contentTypeId)
    {
        var contentType = _contentTypeRepository.Load(contentTypeId);
        if (contentType == null)
            return Enumerable.Empty<ContentUsageDto>();

        var usages = _contentModelUsage.ListContentOfContentType(contentType)
            .DistinctBy(cu => cu.ContentLink.ToReferenceWithoutVersion().ToString() + "-" + cu.LanguageBranch)
            .ToList();

        var results = new List<ContentUsageDto>();
        foreach (var usage in usages)
        {
            try
            {
                var contentRef = usage.ContentLink.ToReferenceWithoutVersion();
                var isPublished = false;

                if (_contentLoader.TryGet<IContent>(contentRef, out var content))
                {
                    if (content is IVersionable v && v.Status == VersionStatus.Published)
                        isPublished = true;
                }

                // Count incoming soft links
                var softLinks = _softLinkRepository.Load(contentRef, true);
                var referenceCount = softLinks?.Count(sl =>
                    !sl.OwnerContentLink.CompareToIgnoreWorkID(contentRef)) ?? 0;

                var lang = usage.LanguageBranch ?? string.Empty;
                results.Add(new ContentUsageDto
                {
                    ContentId = contentRef.ID,
                    Name = usage.Name ?? "[No name]",
                    Language = lang,
                    Breadcrumb = contentRef.GetBreadcrumb(),
                    EditUrl = $"{Paths.ToResource("CMS", "")}#context=epi.cms.contentdata:///{contentRef.ID}&viewsetting=viewlanguage:///{lang}",
                    IsPublished = isPublished,
                    ReferenceCount = referenceCount
                });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error loading content usage for {ContentLink}", usage.ContentLink);
                results.Add(new ContentUsageDto
                {
                    ContentId = usage.ContentLink.ID,
                    Name = usage.Name ?? "[Error loading]",
                    Language = usage.LanguageBranch ?? string.Empty,
                    ReferenceCount = 0
                });
            }
        }

        return results;
    }

    /// <summary>
    /// Gets soft links (references) pointing to a specific content item.
    /// </summary>
    public IEnumerable<SoftLinkDto> GetContentReferences(int contentId)
    {
        var contentRef = new ContentReference(contentId);
        var softLinks = _softLinkRepository.Load(contentRef, true);

        if (softLinks == null)
            return Enumerable.Empty<SoftLinkDto>();

        var results = new List<SoftLinkDto>();
        foreach (var link in softLinks.Where(sl =>
            !sl.OwnerContentLink.CompareToIgnoreWorkID(contentRef)))
        {
            var ownerName = "[Unknown]";
            var ownerTypeName = (string?)null;

            try
            {
                if (_contentLoader.TryGet<IContent>(link.OwnerContentLink, out var owner))
                {
                    ownerName = owner.Name;
                    var ownerType = _contentTypeRepository.Load(owner.ContentTypeID);
                    ownerTypeName = ownerType?.DisplayName ?? ownerType?.Name;
                }
            }
            catch
            {
                ownerName = $"[Missing content {link.OwnerContentLink.ID}]";
            }

            var ownerLang = link.OwnerLanguage?.TwoLetterISOLanguageName ?? "";
            results.Add(new SoftLinkDto
            {
                OwnerContentId = link.OwnerContentLink.ID,
                OwnerName = ownerName,
                OwnerTypeName = ownerTypeName,
                Language = ownerLang,
                PropertyName = link.OwnerPropertyDefinition?.Name,
                EditUrl = $"{Paths.ToResource("CMS", "")}#context=epi.cms.contentdata:///{link.OwnerContentLink.ID}&viewsetting=viewlanguage:///{ownerLang}"
            });
        }

        return results;
    }

    /// <summary>
    /// Gets the content type inheritance tree.
    /// </summary>
    public IEnumerable<ContentTypeTreeNodeDto> GetInheritanceTree()
    {
        var allTypes = _contentTypeRepository.List().ToList();
        var statistics = _statisticsRepository.GetAll()
            .ToDictionary(s => s.ContentTypeId);

        // Build lookup of type name -> children
        var typesWithModel = allTypes.Where(t => t.ModelType != null).ToList();

        // Find root types (whose ModelType.BaseType is not another content type in the system)
        var modelTypeSet = typesWithModel.Select(t => t.ModelType!).ToHashSet();
        var roots = typesWithModel
            .Where(t => !modelTypeSet.Contains(t.ModelType!.BaseType!))
            .OrderBy(t => t.Name)
            .ToList();

        return roots.Select(r => BuildTreeNode(r, typesWithModel, statistics));
    }

    private ContentTypeTreeNodeDto BuildTreeNode(
        ContentType contentType,
        List<ContentType> allTypes,
        Dictionary<int, ContentTypeStatisticsRecord> statistics)
    {
        statistics.TryGetValue(contentType.ID, out var stats);

        var children = allTypes
            .Where(t => t.ModelType?.BaseType == contentType.ModelType)
            .OrderBy(t => t.Name)
            .Select(child => BuildTreeNode(child, allTypes, statistics))
            .ToList();

        return new ContentTypeTreeNodeDto
        {
            Id = contentType.ID,
            Name = contentType.Name,
            DisplayName = contentType.DisplayName,
            ContentCount = stats?.ContentCount,
            IsOrphaned = contentType.ModelType == null,
            Children = children
        };
    }

    private static ContentTypeDto MapToDto(ContentType ct, ContentTypeStatisticsRecord? stats)
    {
        return new ContentTypeDto
        {
            Id = ct.ID,
            Guid = ct.GUID,
            Name = ct.Name,
            DisplayName = ct.DisplayName,
            Description = ct.LocalizedDescription ?? ct.Description,
            GroupName = ct.GroupName,
            Base = ct.Base.ToString(),
            ModelType = ct.ModelTypeString,
            ParentTypeName = ct.ModelType?.BaseType?.Name,
            DefaultController = ct.DefaultMvcController?.Name,
            EditUrl = $"{Paths.ToResource("EPiServer.Cms.UI.Admin", "default")}#/ContentType/{ct.GUID}",
            PropertyCount = ct.PropertyDefinitions.Count,
            IsSystemType = IsSystemType(ct),
            IsOrphaned = ct.ModelType == null,
            IconUrl = GetIconUrl(ct),
            Created = ct.Created,
            Saved = ct.Saved,
            SavedBy = ct.SavedBy,
            ContentCount = stats?.ContentCount,
            PublishedCount = stats?.PublishedCount,
            ReferencedCount = stats?.ReferencedCount,
            UnreferencedCount = stats?.UnreferencedCount,
            StatisticsUpdated = stats?.LastUpdated
        };
    }

    private static bool IsSystemType(ContentType ct)
    {
        // Check model namespace
        var ns = ct.ModelType?.Namespace;
        if (ns != null && ns.StartsWith("EPiServer", StringComparison.OrdinalIgnoreCase))
            return true;

        // Optimizely built-in types like SysContentFolder, SysRoot, SysRecycleBin etc.
        if (ct.Name.StartsWith("Sys", StringComparison.Ordinal))
            return true;

        return false;
    }

    private static string? GetIconUrl(ContentType ct)
    {
        var attr = ct.ModelType?.GetCustomAttribute<ImageUrlAttribute>();
        return attr?.Path;
    }
}
