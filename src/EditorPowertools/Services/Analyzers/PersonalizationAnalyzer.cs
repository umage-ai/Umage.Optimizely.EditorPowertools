using EPiServer;
using EPiServer.Core;
using EPiServer.Core.Html.StringParsing;
using EPiServer.DataAbstraction;
using EPiServer.Personalization.VisitorGroups;
using EPiServer.Security;
using EPiServer.Shell;
using UmageAI.Optimizely.EditorPowerTools.Helpers;
using UmageAI.Optimizely.EditorPowerTools.Tools.PersonalizationAudit;
using Microsoft.Extensions.Logging;

namespace UmageAI.Optimizely.EditorPowerTools.Services.Analyzers;

/// <summary>
/// Analyzer that finds visitor group usage in access rights, content areas, and XHTML fields.
/// Saves per-item during Analyze() (like the original job).
/// </summary>
public class PersonalizationAnalyzer : IContentAnalyzer
{
    private readonly IContentLoader _contentLoader;
    private readonly IContentTypeRepository _contentTypeRepository;
    private readonly IVisitorGroupRepository _visitorGroupRepository;
    private readonly IContentSecurityRepository _contentSecurityRepository;
    private readonly PersonalizationUsageRepository _usageRepository;
    private readonly ILogger<PersonalizationAnalyzer> _logger;

    private Dictionary<string, string> _visitorGroupNames = new();
    private Dictionary<string, string> _visitorGroupNameToId = new();

    public string Name => "Personalization";

    public PersonalizationAnalyzer(
        IContentLoader contentLoader,
        IContentTypeRepository contentTypeRepository,
        IVisitorGroupRepository visitorGroupRepository,
        IContentSecurityRepository contentSecurityRepository,
        PersonalizationUsageRepository usageRepository,
        ILogger<PersonalizationAnalyzer> logger)
    {
        _contentLoader = contentLoader;
        _contentTypeRepository = contentTypeRepository;
        _visitorGroupRepository = visitorGroupRepository;
        _contentSecurityRepository = contentSecurityRepository;
        _usageRepository = usageRepository;
        _logger = logger;
    }

    public void Initialize()
    {
        // Build visitor group lookups (by ID and by name, since access rights use names)
        var visitorGroups = _visitorGroupRepository.List().ToList();
        _visitorGroupNames = visitorGroups.ToDictionary(
            vg => vg.Id.ToString(),
            vg => vg.Name);
        _visitorGroupNameToId = visitorGroups.ToDictionary(
            vg => vg.Name,
            vg => vg.Id.ToString(),
            StringComparer.OrdinalIgnoreCase);

        // Clear old data
        _usageRepository.Clear();
    }

    public void Analyze(IContent content, ContentReference contentRef)
    {
        var contentType = _contentTypeRepository.Load(content.ContentTypeID);
        var contentTypeName = contentType?.DisplayName ?? contentType?.Name;
        var language = (content as ILocalizable)?.Language?.Name;
        var breadcrumb = content.GetBreadcrumb();
        var editUrl = $"{Paths.ToResource("CMS", "")}#/content/{contentRef.ID}/language/{language}";

        // 1. Check access rights for visitor groups
        CheckAccessRights(content, contentRef, contentTypeName, language, breadcrumb, editUrl);

        // 2. Check properties for ContentArea and XhtmlString personalization.
        // Track visited content links to prevent stack overflow on circular block references.
        var visited = new HashSet<int>();
        if (!ContentReference.IsNullOrEmpty(contentRef))
            visited.Add(contentRef.ID);
        CheckProperties(content, contentRef, contentTypeName, language, breadcrumb, editUrl, string.Empty, null, null, visited, 0);
    }

    private const int MaxRecursionDepth = 32;

    public void Complete()
    {
        // Nothing needed - saves happen during Analyze
    }

    private void CheckAccessRights(IContent content, ContentReference contentRef, string? contentTypeName,
        string? language, string breadcrumb, string editUrl)
    {
        try
        {
            var acl = _contentSecurityRepository.Get(contentRef);
            if (acl == null) return;

            foreach (var entry in acl.Entries)
            {
                if (entry.EntityType == SecurityEntityType.VisitorGroup)
                {
                    // Access rights store VG by name, resolve to GUID for consistent lookups
                    var vgId = _visitorGroupNameToId.GetValueOrDefault(entry.Name, entry.Name);
                    var vgName = entry.Name;
                    _usageRepository.Save(new PersonalizationUsageRecord
                    {
                        ContentId = contentRef.ID,
                        ContentName = content.Name,
                        ContentTypeName = contentTypeName,
                        Language = language,
                        PropertyName = "[Access Rights]",
                        VisitorGroupId = vgId,
                        VisitorGroupName = vgName,
                        UsageType = "AccessRight",
                        Breadcrumb = breadcrumb,
                        EditUrl = editUrl,
                        LastUpdated = DateTime.UtcNow
                    });
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error checking access rights for {ContentLink}", contentRef);
        }
    }

    private void CheckProperties(IContent content, ContentReference contentRef, string? contentTypeName,
        string? language, string breadcrumb, string editUrl, string propertyPrefix,
        int? parentContentId, string? parentContentName,
        HashSet<int> visitedContentIds, int depth)
    {
        if (depth >= MaxRecursionDepth)
        {
            _logger.LogWarning("Personalization analyzer hit max recursion depth ({Depth}) while scanning {ContentLink}; skipping deeper nested blocks.", depth, contentRef);
            return;
        }

        foreach (var prop in content.Property)
        {
            var propertyName = string.IsNullOrEmpty(propertyPrefix) ? prop.Name : $"{propertyPrefix}.{prop.Name}";

            try
            {
                // ContentArea personalization
                if (prop.Value is ContentArea contentArea)
                {
                    foreach (var item in contentArea.Items)
                    {
                        var roles = item.AllowedRoles;
                        if (roles != null)
                        {
                            foreach (var role in roles)
                            {
                                var vgName = _visitorGroupNames.GetValueOrDefault(role, role);
                                _usageRepository.Save(new PersonalizationUsageRecord
                                {
                                    ContentId = contentRef.ID,
                                    ContentName = content.Name,
                                    ContentTypeName = contentTypeName,
                                    Language = language,
                                    PropertyName = propertyName,
                                    VisitorGroupId = role,
                                    VisitorGroupName = vgName,
                                    UsageType = "ContentArea",
                                    Breadcrumb = breadcrumb,
                                    EditUrl = editUrl,
                                    ParentContentId = parentContentId,
                                    ParentContentName = parentContentName,
                                    LastUpdated = DateTime.UtcNow
                                });
                            }
                        }

                        // Check nested blocks
                        if (item.ContentLink != null && !ContentReference.IsNullOrEmpty(item.ContentLink))
                        {
                            // Skip if we've already walked this block on the current path — guards against circular references.
                            if (!visitedContentIds.Add(item.ContentLink.ID))
                                continue;

                            try
                            {
                                if (_contentLoader.TryGet<IContent>(item.ContentLink, out var nestedContent)
                                    && nestedContent is IContentData)
                                {
                                    CheckProperties(nestedContent, contentRef, contentTypeName,
                                        language, breadcrumb, editUrl, propertyName,
                                        parentContentId ?? contentRef.ID,
                                        parentContentName ?? content.Name,
                                        visitedContentIds, depth + 1);
                                }
                            }
                            catch (Exception ex)
                            {
                                _logger.LogWarning(ex, "Error checking nested block {ContentLink}", item.ContentLink);
                            }
                            finally
                            {
                                // Allow this block to be visited via other branches (sibling content areas).
                                visitedContentIds.Remove(item.ContentLink.ID);
                            }
                        }
                    }
                }
#if !OPTIMIZELY_CMS13
                // XhtmlString personalization — PersonalizedContentFragment.GetRoles() removed in CMS 13
                else if (prop.Value is XhtmlString xhtml)
                {
                    foreach (var fragment in xhtml.Fragments)
                    {
                        if (fragment is PersonalizedContentFragment personalized)
                        {
                            var roles = personalized.GetRoles();
                            if (roles != null)
                            {
                                foreach (var role in roles)
                                {
                                    var vgName = _visitorGroupNames.GetValueOrDefault(role, role);
                                    _usageRepository.Save(new PersonalizationUsageRecord
                                    {
                                        ContentId = contentRef.ID,
                                        ContentName = content.Name,
                                        ContentTypeName = contentTypeName,
                                        Language = language,
                                        PropertyName = propertyName,
                                        VisitorGroupId = role,
                                        VisitorGroupName = vgName,
                                        UsageType = "XhtmlString",
                                        Breadcrumb = breadcrumb,
                                        EditUrl = editUrl,
                                        ParentContentId = parentContentId,
                                        ParentContentName = parentContentName,
                                        LastUpdated = DateTime.UtcNow
                                    });
                                }
                            }
                        }
                    }
                }
#endif
                // Nested block properties (IContentData that isn't ContentArea or XhtmlString).
                // Inline blocks have no stable ContentLink — depth limit alone bounds the walk.
                else if (prop.Value is IContentData nestedData && nestedData is IContent nestedContentItem)
                {
                    CheckProperties(nestedContentItem, contentRef, contentTypeName,
                        language, breadcrumb, editUrl, propertyName,
                        parentContentId ?? contentRef.ID,
                        parentContentName ?? content.Name,
                        visitedContentIds, depth + 1);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error checking property {PropertyName} on {ContentLink}", propertyName, contentRef);
            }
        }
    }
}
