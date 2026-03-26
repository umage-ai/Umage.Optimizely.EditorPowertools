using System.Globalization;
using EditorPowertools.Tools.BulkPropertyEditor.Models;
using EPiServer;
using EPiServer.Core;
using EPiServer.DataAbstraction;
using EPiServer.DataAccess;
using EPiServer.Editor;
using EPiServer.Security;
using Microsoft.Extensions.Logging;

namespace EditorPowertools.Tools.BulkPropertyEditor;

public class BulkPropertyEditorService
{
    private readonly IContentTypeRepository _contentTypeRepository;
    private readonly IContentRepository _contentRepository;
    private readonly IContentModelUsage _contentModelUsage;
    private readonly ILanguageBranchRepository _languageBranchRepository;
    private readonly ILogger<BulkPropertyEditorService> _logger;

    public BulkPropertyEditorService(
        IContentTypeRepository contentTypeRepository,
        IContentRepository contentRepository,
        IContentModelUsage contentModelUsage,
        ILanguageBranchRepository languageBranchRepository,
        ILogger<BulkPropertyEditorService> logger)
    {
        _contentTypeRepository = contentTypeRepository;
        _contentRepository = contentRepository;
        _contentModelUsage = contentModelUsage;
        _languageBranchRepository = languageBranchRepository;
        _logger = logger;
    }

    public List<ContentTypeListItem> GetContentTypes()
    {
        return _contentTypeRepository.List()
            .Where(ct => ct.ModelType != null)
            .Select(ct => new ContentTypeListItem(
                ct.ID,
                ct.LocalizedName ?? ct.Name,
                GetBaseType(ct.ModelType)))
            .OrderBy(ct => ct.Name)
            .ToList();
    }

    public List<LanguageInfo> GetLanguages()
    {
        return _languageBranchRepository.ListEnabled()
            .Select(lb => new LanguageInfo(
                lb.LanguageID,
                lb.Name,
                lb.LanguageID == "en"))
            .OrderBy(l => l.Code)
            .ToList();
    }

    public List<PropertyColumnInfo> GetProperties(int contentTypeId)
    {
        ContentType? contentType = _contentTypeRepository.Load(contentTypeId);
        if (contentType == null)
        {
            return [];
        }

        return contentType.PropertyDefinitions
            .Where(pd => !IsSystemProperty(pd.Name))
            .Select(pd => new PropertyColumnInfo(
                pd.Name,
                pd.EditCaption ?? pd.Name,
                GetPropertyTypeName(pd),
                IsEditableType(pd.Type?.DataType)))
            .OrderBy(p => p.DisplayName)
            .ToList();
    }

    public Task<ContentFilterResponse> GetContentAsync(ContentFilterRequest request)
    {
        ContentType? contentType = _contentTypeRepository.Load(request.ContentTypeId);
        if (contentType == null)
        {
            return Task.FromResult(new ContentFilterResponse([], 0, request.Page, request.PageSize, 0));
        }

        IList<ContentUsage> usages = _contentModelUsage.ListContentOfContentType(contentType);

        List<ContentReference> distinctLinks = usages
            .Select(u => u.ContentLink.ToReferenceWithoutVersion())
            .GroupBy(r => r.ID)
            .Select(g => g.First())
            .ToList();

        List<IContent> contentItems = [];
        foreach (ContentReference link in distinctLinks)
        {
            try
            {
                if (ContentReference.IsNullOrEmpty(link))
                    continue;

                IContent? content = _contentRepository.Get<IContent>(
                    link,
                    new LanguageSelector(request.Language));
                if (content != null)
                    contentItems.Add(content);
            }
            catch (ContentNotFoundException)
            {
                // Content does not exist in the requested language
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load content {ContentLink} in language {Language}",
                    link, request.Language);
            }
        }

        // Apply filters
        if (request.Filters is { Count: > 0 })
        {
            foreach (PropertyFilter filter in request.Filters)
            {
                contentItems = contentItems
                    .Where(c => MatchesFilter(c, filter))
                    .ToList();
            }
        }

        int totalCount = contentItems.Count;

        // Apply sorting
        if (!string.IsNullOrEmpty(request.SortBy))
        {
            contentItems = ApplySorting(contentItems, request.SortBy, request.SortDirection);
        }

        // Apply paging
        int totalPages = (int)Math.Ceiling((double)totalCount / request.PageSize);
        List<IContent> pagedItems = contentItems
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToList();

        // Build rows
        List<ContentItemRow> rows = pagedItems
            .Select(content => BuildContentItemRow(content, request))
            .ToList();

        ContentFilterResponse response = new(rows, totalCount, request.Page, request.PageSize, totalPages);
        return Task.FromResult(response);
    }

    public Task<List<ContentReferenceInfo>> GetReferencesAsync(int contentId)
    {
        ContentReference contentLink = new ContentReference(contentId);
        IEnumerable<ReferenceInformation> references = _contentRepository.GetReferencesToContent(contentLink, false);

        List<ContentReferenceInfo> result = [];
        foreach (ReferenceInformation reference in references)
        {
            try
            {
                IContent content = _contentRepository.Get<IContent>(reference.OwnerID);
                result.Add(new ContentReferenceInfo(
                    content.ContentLink.ID,
                    content.Name,
                    _contentTypeRepository.Load(content.ContentTypeID)?.Name ?? "Unknown",
                    PageEditing.GetEditUrl(content.ContentLink)));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load reference owner {OwnerID}", reference.OwnerID);
            }
        }

        return Task.FromResult(result);
    }

    public Task SaveAsync(InlineEditRequest request)
    {
        ContentReference contentLink = new ContentReference(request.ContentId);
        IContent content = _contentRepository.Get<IContent>(
            contentLink,
            new LanguageSelector(request.Language));

        ContentData writable = (ContentData)((ContentData)content).CreateWritableClone();

        PropertyData? property = ((IContent)writable).Property[request.PropertyName];
        if (property == null)
        {
            throw new InvalidOperationException($"Property '{request.PropertyName}' not found on content {request.ContentId}.");
        }

        property.Value = ConvertPropertyValue(request.Value, property.GetType());

        _contentRepository.Save(
            (IContent)writable,
            SaveAction.Save | SaveAction.ForceCurrentVersion,
            AccessLevel.Edit);

        return Task.CompletedTask;
    }

    public Task PublishAsync(int contentId, string language)
    {
        ContentReference contentLink = new ContentReference(contentId);
        IContent content = _contentRepository.Get<IContent>(
            contentLink,
            new LanguageSelector(language));

        ContentData writable = (ContentData)((ContentData)content).CreateWritableClone();

        _contentRepository.Save(
            (IContent)writable,
            SaveAction.Publish,
            AccessLevel.Publish);

        return Task.CompletedTask;
    }

    public Task BulkSaveAsync(BulkSaveRequest request)
    {
        SaveAction saveAction = request.Action switch
        {
            "publish" => SaveAction.Publish,
            _ => SaveAction.Save | SaveAction.ForceCurrentVersion
        };

        AccessLevel accessLevel = request.Action == "publish" ? AccessLevel.Publish : AccessLevel.Edit;

        int successCount = 0;
        List<string> errors = [];

        foreach (ContentEditItem item in request.Items)
        {
            try
            {
                ContentReference contentLink = new ContentReference(item.ContentId);
                IContent content = _contentRepository.Get<IContent>(
                    contentLink,
                    new LanguageSelector(item.Language));

                ContentData writable = (ContentData)((ContentData)content).CreateWritableClone();

                foreach (KeyValuePair<string, string?> change in item.PropertyChanges)
                {
                    PropertyData? property = ((IContent)writable).Property[change.Key];
                    if (property != null)
                    {
                        property.Value = ConvertPropertyValue(change.Value, property.GetType());
                    }
                    else
                    {
                        _logger.LogWarning("Property '{PropertyName}' not found on content {ContentId}",
                            change.Key, item.ContentId);
                    }
                }

                _contentRepository.Save((IContent)writable, saveAction, accessLevel);
                successCount++;
            }
            catch (Exception ex)
            {
                errors.Add($"Content {item.ContentId}: {ex.Message}");
                _logger.LogError(ex, "Bulk save failed for content {ContentId}", item.ContentId);
            }
        }

        if (errors.Count > 0)
        {
            throw new InvalidOperationException(
                $"Bulk save partially failed. Saved {successCount}/{request.Items.Count}. Errors: {string.Join("; ", errors)}");
        }

        return Task.CompletedTask;
    }

    private bool CanEdit(ContentReference contentLink)
    {
        try
        {
            IContent content = _contentRepository.Get<IContent>(contentLink);
            if (content is IContentSecurable securable)
            {
                return securable.GetContentSecurityDescriptor()
                    .HasAccess(PrincipalInfo.CurrentPrincipal, AccessLevel.Edit);
            }

            return false;
        }
        catch
        {
            return false;
        }
    }

    private bool CanPublish(ContentReference contentLink)
    {
        try
        {
            IContent content = _contentRepository.Get<IContent>(contentLink);
            if (content is IContentSecurable securable)
            {
                return securable.GetContentSecurityDescriptor()
                    .HasAccess(PrincipalInfo.CurrentPrincipal, AccessLevel.Publish);
            }

            return false;
        }
        catch
        {
            return false;
        }
    }

    private static string GetBaseType(Type? modelType)
    {
        if (modelType == null)
        {
            return "Unknown";
        }

        if (typeof(PageData).IsAssignableFrom(modelType))
        {
            return "Page";
        }

        if (typeof(BlockData).IsAssignableFrom(modelType))
        {
            return "Block";
        }

        if (typeof(MediaData).IsAssignableFrom(modelType))
        {
            return "Media";
        }

        return "Other";
    }

    private static bool IsSystemProperty(string propertyName)
    {
        // Keep PageName as it's useful, but skip other system Page* properties
        if (propertyName == "PageName")
        {
            return false;
        }

        HashSet<string> systemProperties =
        [
            "PageLink",
            "PageTypeID",
            "PageParentLink",
            "PagePendingPublish",
            "PageWorkStatus",
            "PageDeleted",
            "PageSaved",
            "PageTypeName",
            "PageChanged",
            "PageCreated",
            "PageMasterLanguageBranch",
            "PageLanguageBranch",
            "PageGUID",
            "PageContentAssetsID",
            "PageContentOwnerID",
            "PageFolderID",
            "PageShortcutType",
            "PageShortcutLink",
            "PageTargetFrame",
            "PageExternalURL",
            "PageStartPublish",
            "PageStopPublish",
            "PageCreatedBy",
            "PageChangedBy",
            "PageChangedOnPublish",
            "PageCategory",
            "PageVisibleInMenu"
        ];

        return systemProperties.Contains(propertyName);
    }

    /// <summary>
    /// Returns a meaningful type name from a property definition (e.g. "Url", "PageReference", "String").
    /// </summary>
    private static string GetPropertyTypeName(PropertyDefinition pd)
    {
        var typeName = pd.Type?.DefinitionType?.Name;
        if (typeName != null)
        {
            if (typeName.Contains("Url", StringComparison.OrdinalIgnoreCase)) return "Url";
            if (typeName.Contains("PageReference", StringComparison.OrdinalIgnoreCase) ||
                typeName.Contains("ContentReference", StringComparison.OrdinalIgnoreCase)) return "PageReference";
        }
        return pd.Type?.DataType.ToString() ?? "Unknown";
    }

    /// <summary>
    /// Returns a meaningful type name from a runtime property instance.
    /// </summary>
    private static string GetRuntimePropertyTypeName(PropertyData prop)
    {
        var clrType = prop.GetType().Name;
        if (clrType.Contains("Url", StringComparison.OrdinalIgnoreCase)) return "Url";
        if (clrType.Contains("PageReference", StringComparison.OrdinalIgnoreCase) ||
            clrType.Contains("ContentReference", StringComparison.OrdinalIgnoreCase)) return "PageReference";
        return prop.Type.ToString();
    }

    private static bool IsEditableType(PropertyDataType? dataType)
    {
        if (dataType == null)
        {
            return false;
        }

        return dataType.Value is PropertyDataType.String
            or PropertyDataType.LongString
            or PropertyDataType.Number
            or PropertyDataType.FloatNumber
            or PropertyDataType.Boolean
            or PropertyDataType.Date
            or PropertyDataType.PageReference;
    }

    private ContentItemRow BuildContentItemRow(IContent content, ContentFilterRequest request)
    {
        string status = GetContentStatus(content);
        IChangeTrackable? trackable = content as IChangeTrackable;

        Dictionary<string, PropertyValue> properties = [];
        List<string> columns = request.Columns ?? [];

        foreach (string column in columns)
        {
            PropertyData? prop = content.Property[column];
            if (prop != null)
            {
                var displayValue = prop.Value?.ToString();
                object? rawValue = prop.Value;

                // Resolve ContentReference to show content name + ID
                if (prop.Value is ContentReference contentRef && !ContentReference.IsNullOrEmpty(contentRef))
                {
                    rawValue = contentRef.ID;
                    try
                    {
                        if (_contentRepository.TryGet<IContent>(contentRef, out var refContent))
                            displayValue = $"{refContent.Name} (ID: {contentRef.ID})";
                        else
                            displayValue = $"ID: {contentRef.ID}";
                    }
                    catch
                    {
                        displayValue = $"ID: {contentRef.ID}";
                    }
                }

                properties[column] = new PropertyValue(
                    displayValue,
                    rawValue,
                    IsEditableType(prop.Type),
                    GetRuntimePropertyTypeName(prop));
            }
            else
            {
                properties[column] = new PropertyValue(null, null, false, "Unknown");
            }
        }

        List<ContentReferenceInfo>? references = request.IncludeReferences
            ? GetReferencesAsync(content.ContentLink.ID).GetAwaiter().GetResult()
            : null;

        string editUrl = PageEditing.GetEditUrl(content.ContentLink);

        // For "For this page" assets, walk up the tree to find the ContentAssetFolder owner
        string? parentName = null;
        string? parentEditUrl = null;
        try
        {
            ContentReference? walkRef = content.ParentLink;
            while (!ContentReference.IsNullOrEmpty(walkRef))
            {
                IContent? ancestor = _contentRepository.Get<IContent>(walkRef);
                if (ancestor is ContentAssetFolder assetFolder)
                {
                    // ContentAssetFolder.ContentOwnerID is a Guid pointing to the owning content
                    Guid ownerGuid = assetFolder.ContentOwnerID;
                    if (ownerGuid != Guid.Empty)
                    {
                        IContent? owner = _contentRepository.Get<IContent>(ownerGuid);
                        if (owner != null)
                        {
                            parentName = owner.Name;
                            parentEditUrl = PageEditing.GetEditUrl(owner.ContentLink);
                        }
                    }
                    break;
                }
                walkRef = ancestor?.ParentLink;
            }
        }
        catch
        {
            // Ignore errors resolving parent
        }

        return new ContentItemRow(
            content.ContentLink.ID,
            content.Name,
            _contentTypeRepository.Load(content.ContentTypeID)?.Name ?? "Unknown",
            status,
            trackable?.Changed,
            trackable?.ChangedBy,
            (content as ILocalizable)?.Language?.Name ?? request.Language,
            CanEdit(content.ContentLink),
            CanPublish(content.ContentLink),
            editUrl,
            parentName,
            parentEditUrl,
            properties,
            references);
    }

    private static string GetContentStatus(IContent content)
    {
        if (content is IVersionable versionable)
        {
            return versionable.Status switch
            {
                VersionStatus.Published => "Published",
                VersionStatus.CheckedOut => "Draft",
                VersionStatus.CheckedIn => "Ready to Publish",
                VersionStatus.PreviouslyPublished => "Previously Published",
                VersionStatus.DelayedPublish => "Scheduled",
                VersionStatus.Rejected => "Rejected",
                _ => versionable.Status.ToString()
            };
        }

        return "Unknown";
    }

    private static bool MatchesFilter(IContent content, PropertyFilter filter)
    {
        // Resolve the value based on the property name - handle built-in fields
        string? value = filter.PropertyName switch
        {
            "Name" => content.Name,
            "Status" => GetContentStatus(content),
            "EditedBy" => (content as IChangeTrackable)?.ChangedBy,
            "LastEdited" => (content as IChangeTrackable)?.Changed.ToString("o"),
            _ => content.Property?[filter.PropertyName]?.Value?.ToString()
        };

        return filter.Operator switch
        {
            "contains" => value?.Contains(filter.Value, StringComparison.OrdinalIgnoreCase) == true,
            "startsWith" => value?.StartsWith(filter.Value, StringComparison.OrdinalIgnoreCase) == true,
            "equals" => string.Equals(value, filter.Value, StringComparison.OrdinalIgnoreCase),
            "notEmpty" => !string.IsNullOrEmpty(value),
            _ => true
        };
    }

    private static List<IContent> ApplySorting(List<IContent> items, string sortBy, string direction)
    {
        items = items.Where(c => c != null).ToList();
        if (items.Count == 0) return items;

        bool descending = string.Equals(direction, "desc", StringComparison.OrdinalIgnoreCase);

        string GetSortKey(IContent c)
        {
            try
            {
                return sortBy switch
                {
                    "Name" => c.Name ?? "",
                    "EditedBy" => (c as IChangeTrackable)?.ChangedBy ?? "",
                    _ => c.Property?[sortBy]?.Value?.ToString() ?? ""
                };
            }
            catch
            {
                return "";
            }
        }

        DateTime? GetDateKey(IContent c)
        {
            try { return (c as IChangeTrackable)?.Changed; }
            catch { return null; }
        }

        if (sortBy == "LastEdited")
        {
            return descending
                ? items.OrderByDescending(GetDateKey).ToList()
                : items.OrderBy(GetDateKey).ToList();
        }

        return descending
            ? items.OrderByDescending(GetSortKey).ToList()
            : items.OrderBy(GetSortKey).ToList();
    }

    private static object? ConvertPropertyValue(string? value, Type propertyType)
    {
        if (value == null)
        {
            return null;
        }

        string typeName = propertyType.Name;

        if (typeName.Contains("String", StringComparison.OrdinalIgnoreCase))
        {
            return value;
        }

        if (typeName.Contains("Number", StringComparison.OrdinalIgnoreCase)
            && !typeName.Contains("Float", StringComparison.OrdinalIgnoreCase))
        {
            return int.TryParse(value, CultureInfo.InvariantCulture, out int intResult) ? intResult : null;
        }

        if (typeName.Contains("Float", StringComparison.OrdinalIgnoreCase))
        {
            return double.TryParse(value, CultureInfo.InvariantCulture, out double doubleResult) ? doubleResult : null;
        }

        if (typeName.Contains("Boolean", StringComparison.OrdinalIgnoreCase))
        {
            return bool.TryParse(value, out bool boolResult) ? boolResult : null;
        }

        if (typeName.Contains("Date", StringComparison.OrdinalIgnoreCase))
        {
            return DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime dateResult)
                ? dateResult
                : null;
        }

        if (typeName.Contains("Url", StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;
            return new EPiServer.Url(value);
        }

        if (typeName.Contains("PageReference", StringComparison.OrdinalIgnoreCase)
            || typeName.Contains("ContentReference", StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(value) || value == "0")
                return ContentReference.EmptyReference;
            return int.TryParse(value, CultureInfo.InvariantCulture, out int refId)
                ? new ContentReference(refId)
                : ContentReference.EmptyReference;
        }

        // Default: set as string and let the property handle conversion
        return value;
    }
}
