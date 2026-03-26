using EPiServer;
using EPiServer.Core;
using EPiServer.Core.Html.StringParsing;
using EPiServer.DataAbstraction;
using EPiServer.Personalization.VisitorGroups;
using EPiServer.PlugIn;
using EPiServer.Scheduler;
using EPiServer.Security;
using EditorPowertools.Helpers;
using Microsoft.Extensions.Logging;

namespace EditorPowertools.Tools.PersonalizationAudit;

[ScheduledPlugIn(
    DisplayName = "[EditorPowertools] Analyze Personalization Usage",
    Description = "Scans all content for visitor group usage in access rights, content areas, and XHTML fields.",
    SortIndex = 10001)]
public class PersonalizationAnalysisJob : ScheduledJobBase
{
    private readonly IContentRepository _contentRepository;
    private readonly IContentLoader _contentLoader;
    private readonly IContentTypeRepository _contentTypeRepository;
    private readonly IVisitorGroupRepository _visitorGroupRepository;
    private readonly IContentSecurityRepository _contentSecurityRepository;
    private readonly PersonalizationUsageRepository _usageRepository;
    private readonly ILogger<PersonalizationAnalysisJob> _logger;
    private bool _stopSignaled;
    private Dictionary<string, string> _visitorGroupNames = new();
    private Dictionary<string, string> _visitorGroupNameToId = new();

    public PersonalizationAnalysisJob(
        IContentRepository contentRepository,
        IContentLoader contentLoader,
        IContentTypeRepository contentTypeRepository,
        IVisitorGroupRepository visitorGroupRepository,
        IContentSecurityRepository contentSecurityRepository,
        PersonalizationUsageRepository usageRepository,
        ILogger<PersonalizationAnalysisJob> logger)
    {
        _contentRepository = contentRepository;
        _contentLoader = contentLoader;
        _contentTypeRepository = contentTypeRepository;
        _visitorGroupRepository = visitorGroupRepository;
        _contentSecurityRepository = contentSecurityRepository;
        _usageRepository = usageRepository;
        _logger = logger;
        IsStoppable = true;
    }

    public override string Execute()
    {
        _stopSignaled = false;

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

        OnStatusChanged("Fetching content tree...");

        var descendants = _contentRepository.GetDescendents(ContentReference.RootPage).ToList();
        var total = descendants.Count;
        var processed = 0;
        var usagesFound = 0;

        OnStatusChanged($"Scanning {total} content items...");

        foreach (var contentRef in descendants)
        {
            if (_stopSignaled)
                return $"Job stopped after processing {processed}/{total} items. Found {usagesFound} usages.";

            try
            {
                if (!_contentLoader.TryGet<IContent>(contentRef, out var content))
                    continue;

                var contentType = _contentTypeRepository.Load(content.ContentTypeID);
                var contentTypeName = contentType?.DisplayName ?? contentType?.Name;
                var language = (content as ILocalizable)?.Language?.Name;
                var breadcrumb = content.GetBreadcrumb();
                var editUrl = $"/EPiServer/CMS/#/content/{contentRef.ID}/language/{language}";

                // 1. Check access rights for visitor groups
                usagesFound += CheckAccessRights(content, contentRef, contentTypeName, language, breadcrumb, editUrl);

                // 2. Check properties for ContentArea and XhtmlString personalization
                usagesFound += CheckProperties(content, contentRef, contentTypeName, language, breadcrumb, editUrl, string.Empty, null, null);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error processing content {ContentLink} for personalization", contentRef);
            }

            processed++;
            if (processed % 100 == 0)
                OnStatusChanged($"Processed {processed}/{total} content items... ({usagesFound} usages found)");
        }

        return $"Completed. Scanned {processed} content items, found {usagesFound} personalization usages.";
    }

    private int CheckAccessRights(IContent content, ContentReference contentRef, string? contentTypeName,
        string? language, string breadcrumb, string editUrl)
    {
        var count = 0;
        try
        {
            var acl = _contentSecurityRepository.Get(contentRef);
            if (acl == null) return 0;

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
                    count++;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error checking access rights for {ContentLink}", contentRef);
        }

        return count;
    }

    private int CheckProperties(IContent content, ContentReference contentRef, string? contentTypeName,
        string? language, string breadcrumb, string editUrl, string propertyPrefix,
        int? parentContentId, string? parentContentName)
    {
        var count = 0;

        foreach (var prop in content.Property)
        {
            if (_stopSignaled) break;

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
                                count++;
                            }
                        }

                        // Check nested blocks
                        if (item.ContentLink != null && !ContentReference.IsNullOrEmpty(item.ContentLink))
                        {
                            try
                            {
                                if (_contentLoader.TryGet<IContent>(item.ContentLink, out var nestedContent)
                                    && nestedContent is IContentData)
                                {
                                    count += CheckProperties(nestedContent, contentRef, contentTypeName,
                                        language, breadcrumb, editUrl, propertyName,
                                        parentContentId ?? contentRef.ID,
                                        parentContentName ?? content.Name);
                                }
                            }
                            catch (Exception ex)
                            {
                                _logger.LogWarning(ex, "Error checking nested block {ContentLink}", item.ContentLink);
                            }
                        }
                    }
                }
                // XhtmlString personalization
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
                                    count++;
                                }
                            }
                        }
                    }
                }
                // Nested block properties (IContentData that isn't ContentArea or XhtmlString)
                else if (prop.Value is IContentData nestedData && nestedData is IContent nestedContentItem)
                {
                    count += CheckProperties(nestedContentItem, contentRef, contentTypeName,
                        language, breadcrumb, editUrl, propertyName,
                        parentContentId ?? contentRef.ID,
                        parentContentName ?? content.Name);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error checking property {PropertyName} on {ContentLink}", propertyName, contentRef);
            }
        }

        return count;
    }

    public override void Stop()
    {
        _stopSignaled = true;
        base.Stop();
    }
}
