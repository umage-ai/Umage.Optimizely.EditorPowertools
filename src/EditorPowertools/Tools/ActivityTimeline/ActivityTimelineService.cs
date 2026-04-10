using EPiServer;
using EPiServer.Core;
using EPiServer.DataAbstraction;
using EPiServer.DataAbstraction.Activities;
using EPiServer.Shell;
using UmageAI.Optimizely.EditorPowerTools.Tools.ActivityTimeline.Models;
using Microsoft.Extensions.Logging;

namespace UmageAI.Optimizely.EditorPowerTools.Tools.ActivityTimeline;

public class ActivityTimelineService
{
    private readonly IContentVersionRepository _versionRepository;
    private readonly IContentRepository _contentRepository;
    private readonly IContentTypeRepository _contentTypeRepository;
    private readonly ContentActivityFeed _contentActivityFeed;
    private readonly ILogger<ActivityTimelineService> _logger;

    public ActivityTimelineService(
        IContentVersionRepository versionRepository,
        IContentRepository contentRepository,
        IContentTypeRepository contentTypeRepository,
        ContentActivityFeed contentActivityFeed,
        ILogger<ActivityTimelineService> logger)
    {
        _versionRepository = versionRepository;
        _contentRepository = contentRepository;
        _contentTypeRepository = contentTypeRepository;
        _contentActivityFeed = contentActivityFeed;
        _logger = logger;
    }

    public ActivityTimelineResponse GetActivities(ActivityFilterRequest request)
    {
        var statusFilters = GetStatusFilters(request.Action);

        // When filtering by a single content item, skip the full tree scan
        List<ContentReference> allDescendants;
        var allVersions = new List<ContentVersion>();

        if (request.ContentId.HasValue)
        {
            var singleRef = new ContentReference(request.ContentId.Value);
            allDescendants = new List<ContentReference> { singleRef };
            try
            {
                var versions = _versionRepository.List(singleRef);
                allVersions.AddRange(versions);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Could not list versions for content {ContentId}", request.ContentId.Value);
            }
        }
        else
        {
            allDescendants = _contentRepository.GetDescendents(ContentReference.RootPage).ToList();
            foreach (var contentRef in allDescendants)
            {
                try
                {
                    // Only include content the current user can access
                    if (!_contentRepository.TryGet<IContent>(contentRef, out _))
                        continue;

                    var versions = _versionRepository.List(contentRef);
                    allVersions.AddRange(versions);
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Could not list versions for {ContentRef}", contentRef);
                }
            }
        }

        // Apply filters
        var filtered = allVersions.AsEnumerable();

        // Filter by status
        if (statusFilters != null)
        {
            var statusSet = new HashSet<VersionStatus>(statusFilters);
            filtered = filtered.Where(v => statusSet.Contains(v.Status));
        }

        // Filter by date range
        if (request.FromUtc.HasValue)
            filtered = filtered.Where(v => v.Saved >= request.FromUtc.Value);
        if (request.ToUtc.HasValue)
            filtered = filtered.Where(v => v.Saved <= request.ToUtc.Value);

        // Filter by user
        if (!string.IsNullOrWhiteSpace(request.User))
            filtered = filtered.Where(v =>
                string.Equals(v.SavedBy, request.User, StringComparison.OrdinalIgnoreCase));

        // Filter by content type
        if (!string.IsNullOrWhiteSpace(request.ContentTypeName))
        {
            var contentType = _contentTypeRepository.List()
                .FirstOrDefault(ct => string.Equals(ct.Name, request.ContentTypeName, StringComparison.OrdinalIgnoreCase));

            if (contentType != null)
            {
                // Build set of content IDs that match this type
                var matchingIds = new HashSet<int>();
                foreach (var contentRef in allDescendants)
                {
                    try
                    {
                        if (_contentRepository.TryGet<IContent>(contentRef, out var content) &&
                            content.ContentTypeID == contentType.ID)
                        {
                            matchingIds.Add(contentRef.ID);
                        }
                    }
                    catch
                    {
                        // Skip inaccessible content
                    }
                }

                filtered = filtered.Where(v => matchingIds.Contains(v.ContentLink.ID));
            }
            else
            {
                // No matching content type, return empty
                return new ActivityTimelineResponse { TotalCount = 0, HasMore = false };
            }
        }

        // Sort by saved date descending (newest first)
        var sorted = filtered.OrderByDescending(v => v.Saved).ToList();

        // Map version entries to DTOs
        var contentTypeCache = _contentTypeRepository.List().ToDictionary(ct => ct.ID);
        var activities = new List<ActivityDto>();

        foreach (var version in sorted)
        {
            try
            {
                var contentRef = version.ContentLink.ToReferenceWithoutVersion();
                string contentName = version.Name ?? "[Unknown]";
                string contentTypeName = string.Empty;

                if (_contentRepository.TryGet<IContent>(contentRef, out var content))
                {
                    contentName = content.Name;
                    if (contentTypeCache.TryGetValue(content.ContentTypeID, out var ct))
                        contentTypeName = ct.DisplayName ?? ct.Name;
                }

                // Check if there's a previous version
                var contentVersions = _versionRepository.List(contentRef, version.LanguageBranch);
                var hasPrevious = contentVersions
                    .OrderByDescending(v => v.Saved)
                    .Any(v => v.Saved < version.Saved);

                var lang = version.LanguageBranch ?? string.Empty;

                activities.Add(new ActivityDto
                {
                    ContentId = contentRef.ID,
                    VersionId = version.ContentLink.WorkID,
                    ContentName = contentName,
                    ContentTypeName = contentTypeName,
                    Action = MapAction(version.Status),
                    User = version.SavedBy ?? string.Empty,
                    TimestampUtc = version.Saved,
                    Language = lang,
                    EditUrl = $"{Paths.ToResource("CMS", "")}#context=epi.cms.contentdata:///{contentRef.ID}&viewsetting=viewlanguage:///{lang}",
                    HasPreviousVersion = hasPrevious
                });
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Error mapping version {ContentLink}", version.ContentLink);
            }
        }

        // Merge in message/comment activities (unless filtering for a specific non-comment action)
        if (string.IsNullOrWhiteSpace(request.Action) || request.Action == "Comment")
        {
            activities.AddRange(GetMessageActivities(request));
        }

        // Re-sort merged list and paginate
        activities = activities.OrderByDescending(a => a.TimestampUtc).ToList();
        var totalCount = activities.Count;
        var paged = activities.Skip(request.Skip).Take(request.Take).ToList();

        // Resolve content name when filtering by a single item
        string? filteredContentName = null;
        if (request.ContentId.HasValue)
        {
            try
            {
                if (_contentRepository.TryGet<IContent>(new ContentReference(request.ContentId.Value), out var filteredContent))
                    filteredContentName = filteredContent.Name;
            }
            catch { /* Ignore */ }
        }

        return new ActivityTimelineResponse
        {
            Activities = paged,
            TotalCount = totalCount,
            HasMore = request.Skip + request.Take < totalCount,
            ContentName = filteredContentName
        };
    }

    public VersionComparisonDto CompareVersions(int contentId, int versionId, string? language)
    {
        var contentRef = new ContentReference(contentId, versionId);

        IContent currentContent;
        try
        {
            currentContent = _contentRepository.Get<IContent>(contentRef);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not load content {ContentId} version {VersionId}", contentId, versionId);
            return new VersionComparisonDto { HasPrevious = false };
        }

        // Find previous version
        var versions = _versionRepository.List(new ContentReference(contentId), language)
            .OrderByDescending(v => v.Saved)
            .ToList();

        var currentIndex = versions.FindIndex(v => v.ContentLink.WorkID == versionId);
        if (currentIndex < 0 || currentIndex >= versions.Count - 1)
            return new VersionComparisonDto { HasPrevious = false, ContentName = currentContent.Name };

        var previousVersion = versions[currentIndex + 1];

        IContent previousContent;
        try
        {
            previousContent = _contentRepository.Get<IContent>(previousVersion.ContentLink);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not load previous version {ContentLink}", previousVersion.ContentLink);
            return new VersionComparisonDto { HasPrevious = false, ContentName = currentContent.Name };
        }

        // Compare properties
        var changes = new List<PropertyChangeDto>();
        foreach (var prop in currentContent.Property)
        {
            if (prop.Name.StartsWith("Page", StringComparison.Ordinal) &&
                (prop.Name == "PageLink" || prop.Name == "PageTypeID" || prop.Name == "PageTypeName" ||
                 prop.Name == "PageParentLink" || prop.Name == "PageGUID" || prop.Name == "PageWorkStatus"))
                continue;

            var prevProp = previousContent.Property[prop.Name];
            var currentVal = prop.Value?.ToString() ?? string.Empty;
            var prevVal = prevProp?.Value?.ToString() ?? string.Empty;

            if (currentVal != prevVal)
            {
                var isHtml = prop.Value is XhtmlString || prevProp?.Value is XhtmlString;
                changes.Add(new PropertyChangeDto
                {
                    PropertyName = prop.Name,
                    OldValue = isHtml ? prevVal : TruncateValue(prevVal),
                    NewValue = isHtml ? currentVal : TruncateValue(currentVal),
                    IsHtml = isHtml
                });
            }
        }

        return new VersionComparisonDto
        {
            HasPrevious = true,
            ContentName = currentContent.Name,
            CurrentVersion = versionId,
            PreviousVersion = previousVersion.ContentLink.WorkID,
            Changes = changes
        };
    }

    public ActivityStatsDto GetStats()
    {
        var today = DateTime.UtcNow.Date;
        var tomorrow = today.AddDays(1);

        var allDescendants = _contentRepository.GetDescendents(ContentReference.RootPage).ToList();
        var todayVersions = new List<ContentVersion>();

        foreach (var contentRef in allDescendants)
        {
            try
            {
                // Only include content the current user can access
                if (!_contentRepository.TryGet<IContent>(contentRef, out _))
                    continue;

                var versions = _versionRepository.List(contentRef)
                    .Where(v => v.Saved >= today && v.Saved < tomorrow);
                todayVersions.AddRange(versions);
            }
            catch
            {
                // Skip inaccessible content
            }
        }

        return new ActivityStatsDto
        {
            TotalToday = todayVersions.Count,
            ActiveEditorsToday = todayVersions.Select(v => v.SavedBy).Distinct(StringComparer.OrdinalIgnoreCase).Count(),
            PublishesToday = todayVersions.Count(v => v.Status == VersionStatus.Published),
            DraftsToday = todayVersions.Count(v => v.Status == VersionStatus.CheckedOut)
        };
    }

    public IEnumerable<string> GetDistinctUsers()
    {
        var allDescendants = _contentRepository.GetDescendents(ContentReference.RootPage).ToList();
        var users = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var contentRef in allDescendants)
        {
            try
            {
                // Only include content the current user can access
                if (!_contentRepository.TryGet<IContent>(contentRef, out _))
                    continue;

                var versions = _versionRepository.List(contentRef);
                foreach (var v in versions)
                {
                    if (!string.IsNullOrWhiteSpace(v.SavedBy))
                        users.Add(v.SavedBy);
                }
            }
            catch
            {
                // Skip inaccessible content
            }
        }

        return users.OrderBy(u => u);
    }

    public IEnumerable<string> GetDistinctContentTypes()
    {
        return _contentTypeRepository.List()
            .Select(ct => ct.DisplayName ?? ct.Name)
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .OrderBy(n => n);
    }

    /// <summary>
    /// Fetches message/comment activities using ContentActivityFeed.ListActivitiesAsync.
    /// </summary>
    private List<ActivityDto> GetMessageActivities(ActivityFilterRequest request)
    {
        var results = new List<ActivityDto>();

        try
        {
            var allDescendants = _contentRepository.GetDescendents(ContentReference.RootPage)
                .Where(cr => _contentRepository.TryGet<IContent>(cr, out _))
                .ToList();
            var contentTypeCache = _contentTypeRepository.List().ToDictionary(ct => ct.ID);

            // Use the batch overload: ListActivitiesAsync(IEnumerable<ContentReference>, startIndex, maxCount)
            var pagedResult = _contentActivityFeed.ListActivitiesAsync(allDescendants, 0, 1000)
                .GetAwaiter().GetResult();

            if (pagedResult?.PagedResult == null) return results;

            foreach (var activity in pagedResult.PagedResult)
            {
                // Only interested in Message activities (ActivityType == "Message")
                if (!string.Equals(activity.ActivityType, "Message", StringComparison.OrdinalIgnoreCase))
                    continue;

                var changed = activity.Created;
                var changedBy = activity.ChangedBy ?? string.Empty;

                if (request.FromUtc.HasValue && changed < request.FromUtc.Value)
                    continue;
                if (request.ToUtc.HasValue && changed > request.ToUtc.Value)
                    continue;

                if (!string.IsNullOrWhiteSpace(request.User) &&
                    !string.Equals(changedBy, request.User, StringComparison.OrdinalIgnoreCase))
                    continue;

                if (!string.IsNullOrWhiteSpace(request.Action) && request.Action != "Comment")
                    continue;

                // Filter by content ID if specified
                if (request.ContentId.HasValue)
                {
                    int activityContentId = 0;
                    if (activity.ExtendedData?.TryGetValue("contentLink", out var clCheck) == true)
                    {
                        var clCheckStr = clCheck?.ToString();
                        if (!string.IsNullOrEmpty(clCheckStr))
                        {
                            var checkRef = ContentReference.Parse(clCheckStr);
                            if (!ContentReference.IsNullOrEmpty(checkRef))
                                activityContentId = checkRef.ID;
                        }
                    }
                    if (activityContentId != request.ContentId.Value)
                        continue;
                }

                // Extract message text from ExtendedData
                var messageText = string.Empty;
                if (activity.ExtendedData?.TryGetValue("Message", out var msgVal) == true)
                    messageText = msgVal?.ToString() ?? string.Empty;

                // Extract content link from ExtendedData (format: "5_103")
                string contentName = "[Unknown]";
                string contentTypeName = string.Empty;
                int contentId = 0;

                if (activity.ExtendedData?.TryGetValue("contentLink", out var clVal) == true)
                {
                    var clStr = clVal?.ToString();
                    if (!string.IsNullOrEmpty(clStr))
                    {
                        var contentRef = ContentReference.Parse(clStr);
                        if (!ContentReference.IsNullOrEmpty(contentRef))
                        {
                            contentId = contentRef.ID;
                            try
                            {
                                if (_contentRepository.TryGet<IContent>(contentRef.ToReferenceWithoutVersion(), out var content))
                                {
                                    contentName = content.Name;
                                    if (contentTypeCache.TryGetValue(content.ContentTypeID, out var ct))
                                        contentTypeName = ct.DisplayName ?? ct.Name;
                                }
                            }
                            catch { /* Skip inaccessible content */ }
                        }
                    }
                }

                results.Add(new ActivityDto
                {
                    ContentId = contentId,
                    VersionId = 0,
                    ContentName = contentName,
                    ContentTypeName = contentTypeName,
                    Action = "Comment",
                    User = changedBy,
                    TimestampUtc = changed,
                    Language = null,
                    EditUrl = contentId > 0 ? $"{Paths.ToResource("CMS", "")}#context=epi.cms.contentdata:///{contentId}" : null,
                    HasPreviousVersion = false,
                    Message = messageText
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Could not query message activities from ContentActivityFeed");
        }

        return results;
    }

    private static string MapAction(VersionStatus status)
    {
        return status switch
        {
            VersionStatus.Published => "Published",
            VersionStatus.CheckedOut => "Draft",
            VersionStatus.CheckedIn => "ReadyToPublish",
            VersionStatus.DelayedPublish => "Scheduled",
            VersionStatus.Rejected => "Rejected",
            VersionStatus.PreviouslyPublished => "PreviouslyPublished",
            _ => status.ToString()
        };
    }

    private static VersionStatus[]? GetStatusFilters(string? action)
    {
        if (string.IsNullOrWhiteSpace(action))
            return null;

        return action switch
        {
            "Published" => new[] { VersionStatus.Published },
            "Draft" => new[] { VersionStatus.CheckedOut },
            "ReadyToPublish" => new[] { VersionStatus.CheckedIn },
            "Scheduled" => new[] { VersionStatus.DelayedPublish },
            "Rejected" => new[] { VersionStatus.Rejected },
            "PreviouslyPublished" => new[] { VersionStatus.PreviouslyPublished },
            "Comment" => null, // Comments come from activities, not versions
            _ => null
        };
    }

    private static string TruncateValue(string value, int maxLength = 500)
    {
        if (value.Length <= maxLength)
            return value;
        return value[..maxLength] + "...";
    }
}
