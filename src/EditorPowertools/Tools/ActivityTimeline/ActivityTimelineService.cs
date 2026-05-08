using EPiServer;
using EPiServer.Core;
using EPiServer.DataAbstraction;
using EPiServer.DataAbstraction.Activities;
using UmageAI.Optimizely.EditorPowerTools.Infrastructure;
using UmageAI.Optimizely.EditorPowerTools.Tools.ActivityTimeline.Models;
using Microsoft.Extensions.Logging;

namespace UmageAI.Optimizely.EditorPowerTools.Tools.ActivityTimeline;

public class ActivityTimelineService
{
    private readonly IActivityQueryService _activityQueryService;
    private readonly IContentVersionRepository _versionRepository;
    private readonly IContentRepository _contentRepository;
    private readonly IContentTypeRepository _contentTypeRepository;
    private readonly ILogger<ActivityTimelineService> _logger;

    /// <summary>
    /// Hard cap on activities pulled from the changelog per request. The user paginates client-side.
    /// </summary>
    private const int ActivityFetchLimit = 5000;

    /// <summary>
    /// Window used to populate the user filter dropdown — anyone who has touched content in
    /// the last N days. Older users would still be filterable by typing their username
    /// directly in the request, just not pre-listed.
    /// </summary>
    private const int UserDropdownWindowDays = 90;

    public ActivityTimelineService(
        IActivityQueryService activityQueryService,
        IContentVersionRepository versionRepository,
        IContentRepository contentRepository,
        IContentTypeRepository contentTypeRepository,
        ILogger<ActivityTimelineService> logger)
    {
        _activityQueryService = activityQueryService;
        _versionRepository = versionRepository;
        _contentRepository = contentRepository;
        _contentTypeRepository = contentTypeRepository;
        _logger = logger;
    }

    public ActivityTimelineResponse GetActivities(ActivityFilterRequest request)
    {
        return request.ContentId.HasValue
            ? GetActivitiesForSingleContent(request)
            : GetActivitiesFromChangelog(request);
    }

    /// <summary>
    /// Single-content history — use IContentVersionRepository.List, which is bounded by the
    /// item's own version count. Comments for that one item come from the changelog query.
    /// </summary>
    private ActivityTimelineResponse GetActivitiesForSingleContent(ActivityFilterRequest request)
    {
        var contentId = request.ContentId!.Value;
        var singleRef = new ContentReference(contentId);

        // Gather versions for this single content
        List<ContentVersion> versions;
        try
        {
            versions = _versionRepository.List(singleRef).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Could not list versions for content {ContentId}", contentId);
            versions = new List<ContentVersion>();
        }

        var statusFilters = GetStatusFilters(request.Action);
        var versionsByLang = versions
            .GroupBy(v => v.LanguageBranch ?? string.Empty)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(v => v.Saved).ToList());

        var activities = new List<ActivityDto>();
        var contentTypeCache = _contentTypeRepository.List().ToDictionary(ct => ct.ID);

        foreach (var version in versions)
        {
            if (statusFilters != null && !statusFilters.Contains(version.Status)) continue;
            if (request.FromUtc.HasValue && version.Saved < request.FromUtc.Value) continue;
            if (request.ToUtc.HasValue && version.Saved > request.ToUtc.Value) continue;
            if (!string.IsNullOrWhiteSpace(request.User) &&
                !string.Equals(version.SavedBy, request.User, StringComparison.OrdinalIgnoreCase)) continue;

            var contentRef = version.ContentLink.ToReferenceWithoutVersion();
            var lang = version.LanguageBranch ?? string.Empty;
            string contentName = version.Name ?? "[Unknown]";
            string contentTypeName = string.Empty;
            if (_contentRepository.TryGet<IContent>(contentRef, out var content))
            {
                contentName = content.Name;
                if (contentTypeCache.TryGetValue(content.ContentTypeID, out var ct))
                    contentTypeName = ct.DisplayName ?? ct.Name;
            }

            var hasPrevious = versionsByLang.TryGetValue(lang, out var langVersions)
                && langVersions.Any(v => v.Saved < version.Saved);

            activities.Add(new ActivityDto
            {
                ContentId = contentRef.ID,
                VersionId = version.ContentLink.WorkID,
                ContentName = contentName,
                ContentTypeName = contentTypeName,
                Action = MapVersionStatus(version.Status),
                User = version.SavedBy ?? string.Empty,
                TimestampUtc = version.Saved,
                Language = lang,
                EditUrl = $"{EditorPowertoolsShellPaths.CmsRoot()}#context=epi.cms.contentdata:///{contentRef.ID}&viewsetting=viewlanguage:///{lang}",
                HasPreviousVersion = hasPrevious
            });
        }

        // Comments for this single content come from the changelog
        if (string.IsNullOrWhiteSpace(request.Action) || request.Action == "Comment")
        {
            activities.AddRange(GetCommentsForContent(contentId, request));
        }

        activities = activities.OrderByDescending(a => a.TimestampUtc).ToList();
        var totalCount = activities.Count;
        var paged = activities.Skip(request.Skip).Take(request.Take).ToList();

        string? filteredContentName = null;
        if (_contentRepository.TryGet<IContent>(singleRef, out var single))
            filteredContentName = single.Name;

        return new ActivityTimelineResponse
        {
            Activities = paged,
            TotalCount = totalCount,
            HasMore = request.Skip + request.Take < totalCount,
            ContentName = filteredContentName
        };
    }

    /// <summary>
    /// Multi-content path — pull from the EPiServer activity changelog directly.
    /// IActivityQueryService.ListActivitiesAsync is indexed and date-bounded; never walks the
    /// content tree, so this scales to busy sites without GetDescendents.
    /// </summary>
    private ActivityTimelineResponse GetActivitiesFromChangelog(ActivityFilterRequest request)
    {
        var query = new ActivityQuery
        {
            CreatedAfter = request.FromUtc,
            CreatedBefore = request.ToUtc,
            ChangedBy = string.IsNullOrWhiteSpace(request.User) ? null : request.User,
            MaxResults = ActivityFetchLimit,
            Order = ActivityOrder.LatestFirst,
            IncludeArchived = false
        };

        IEnumerable<Activity> rawActivities;
        try
        {
            rawActivities = _activityQueryService.ListActivitiesAsync(query).GetAwaiter().GetResult()
                ?? Enumerable.Empty<Activity>();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Activity query failed; returning empty timeline.");
            return new ActivityTimelineResponse { TotalCount = 0, HasMore = false };
        }

        var activitiesList = rawActivities as IList<Activity> ?? rawActivities.ToList();
        var truncated = activitiesList.Count >= ActivityFetchLimit;

        var contentTypeCache = _contentTypeRepository.List().ToDictionary(ct => ct.ID);
        int? matchingContentTypeId = null;
        if (!string.IsNullOrWhiteSpace(request.ContentTypeName))
        {
            var ct = contentTypeCache.Values.FirstOrDefault(c =>
                string.Equals(c.Name, request.ContentTypeName, StringComparison.OrdinalIgnoreCase));
            if (ct == null)
                return new ActivityTimelineResponse { TotalCount = 0, HasMore = false, Truncated = truncated };
            matchingContentTypeId = ct.ID;
        }

        var dtos = new List<ActivityDto>();
        foreach (var activity in activitiesList)
        {
            var dto = MapActivity(activity, contentTypeCache);
            if (dto == null) continue;
            if (matchingContentTypeId.HasValue && !MatchesContentType(activity, matchingContentTypeId.Value))
                continue;
            if (!MatchesActionFilter(dto.Action, request.Action)) continue;
            dtos.Add(dto);
        }

        // ListActivitiesAsync already returns latest-first, but a safety re-sort is cheap.
        dtos.Sort((a, b) => b.TimestampUtc.CompareTo(a.TimestampUtc));

        var totalCount = dtos.Count;
        var paged = dtos.Skip(request.Skip).Take(request.Take).ToList();

        return new ActivityTimelineResponse
        {
            Activities = paged,
            TotalCount = totalCount,
            HasMore = request.Skip + request.Take < totalCount,
            Truncated = truncated
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

        IEnumerable<Activity> activities;
        try
        {
            var query = new ActivityQuery
            {
                CreatedAfter = today,
                CreatedBefore = tomorrow,
                MaxResults = ActivityFetchLimit,
                Order = ActivityOrder.LatestFirst,
                IncludeArchived = false
            };
            activities = _activityQueryService.ListActivitiesAsync(query).GetAwaiter().GetResult()
                ?? Enumerable.Empty<Activity>();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not load today's activity stats from changelog.");
            return new ActivityStatsDto();
        }

        int total = 0, publishes = 0, drafts = 0;
        var distinctEditors = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var activity in activities)
        {
            if (activity is not ContentActivity ca) continue;
            total++;
            if (!string.IsNullOrWhiteSpace(ca.ChangedBy))
                distinctEditors.Add(ca.ChangedBy);
            if (ca.ActionType == ContentActionType.Publish) publishes++;
            else if (ca.ActionType == ContentActionType.Save) drafts++;
        }

        return new ActivityStatsDto
        {
            TotalToday = total,
            ActiveEditorsToday = distinctEditors.Count,
            PublishesToday = publishes,
            DraftsToday = drafts
        };
    }

    /// <summary>
    /// Distinct usernames from the recent activity window. Backs the filter dropdown only;
    /// users active before the window are still filterable by entering their name directly.
    /// </summary>
    public IEnumerable<string> GetDistinctUsers()
    {
        var since = DateTime.UtcNow.AddDays(-UserDropdownWindowDays);
        var users = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            var query = new ActivityQuery
            {
                CreatedAfter = since,
                MaxResults = ActivityFetchLimit,
                Order = ActivityOrder.LatestFirst,
                IncludeArchived = false
            };
            var activities = _activityQueryService.ListActivitiesAsync(query).GetAwaiter().GetResult()
                ?? Enumerable.Empty<Activity>();

            foreach (var activity in activities)
            {
                if (!string.IsNullOrWhiteSpace(activity.ChangedBy))
                    users.Add(activity.ChangedBy);
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Could not enumerate distinct users from changelog.");
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

    private ActivityDto? MapActivity(Activity activity, Dictionary<int, ContentType> contentTypeCache)
    {
        if (activity is ContentActivity contentActivity)
        {
            var contentLink = contentActivity.ContentLink;
            if (contentLink == null || ContentReference.IsNullOrEmpty(contentLink))
                return null;

            var contentRef = contentLink.ToReferenceWithoutVersion();
            var lang = contentActivity.Language?.Name ?? string.Empty;
            var contentTypeName = contentTypeCache.TryGetValue(contentActivity.ContentTypeId, out var ct)
                ? (ct.DisplayName ?? ct.Name)
                : string.Empty;

            // Only "saved" / "publish" / "checkin" style events have a meaningful previous version
            // for the Compare button. Setting true is safe — CompareVersions handles "no previous".
            var hasPrevious = contentActivity.ActionType is
                ContentActionType.Save or ContentActionType.Publish or
                ContentActionType.CheckIn or ContentActionType.DelayedPublish or
                ContentActionType.Rejected;

            return new ActivityDto
            {
                ContentId = contentRef.ID,
                VersionId = contentLink.WorkID,
                ContentName = contentActivity.Name ?? "[Unknown]",
                ContentTypeName = contentTypeName,
                Action = MapContentActionType(contentActivity.ActionType),
                User = contentActivity.ChangedBy ?? string.Empty,
                TimestampUtc = contentActivity.Created,
                Language = lang,
                EditUrl = $"{EditorPowertoolsShellPaths.CmsRoot()}#context=epi.cms.contentdata:///{contentRef.ID}&viewsetting=viewlanguage:///{lang}",
                HasPreviousVersion = hasPrevious
            };
        }

        // Non-content activities: comments
        if (string.Equals(activity.ActivityType, "Message", StringComparison.OrdinalIgnoreCase))
        {
            var (cid, cname, cTypeName) = ResolveCommentContent(activity, contentTypeCache);
            var msg = string.Empty;
            if (activity.ExtendedData?.TryGetValue("Message", out var m) == true)
                msg = m?.ToString() ?? string.Empty;

            return new ActivityDto
            {
                ContentId = cid,
                VersionId = 0,
                ContentName = cname,
                ContentTypeName = cTypeName,
                Action = "Comment",
                User = activity.ChangedBy ?? string.Empty,
                TimestampUtc = activity.Created,
                Language = null,
                EditUrl = cid > 0 ? $"{EditorPowertoolsShellPaths.CmsRoot()}#context=epi.cms.contentdata:///{cid}" : null,
                HasPreviousVersion = false,
                Message = msg
            };
        }

        return null;
    }

    private (int ContentId, string ContentName, string ContentTypeName) ResolveCommentContent(
        Activity activity, Dictionary<int, ContentType> contentTypeCache)
    {
        if (activity.ExtendedData?.TryGetValue("contentLink", out var raw) != true)
            return (0, "[Unknown]", string.Empty);

        var s = raw?.ToString();
        if (string.IsNullOrEmpty(s)) return (0, "[Unknown]", string.Empty);

        var parsed = ContentReference.Parse(s);
        if (ContentReference.IsNullOrEmpty(parsed))
            return (0, "[Unknown]", string.Empty);

        var contentId = parsed.ID;
        try
        {
            if (_contentRepository.TryGet<IContent>(parsed.ToReferenceWithoutVersion(), out var content))
            {
                var typeName = contentTypeCache.TryGetValue(content.ContentTypeID, out var ct)
                    ? (ct.DisplayName ?? ct.Name)
                    : string.Empty;
                return (contentId, content.Name, typeName);
            }
        }
        catch
        {
            // user lacks read access — keep the activity row but without details
        }
        return (contentId, "[Unknown]", string.Empty);
    }

    private List<ActivityDto> GetCommentsForContent(int contentId, ActivityFilterRequest request)
    {
        var results = new List<ActivityDto>();
        try
        {
            var query = new ActivityQuery
            {
                CreatedAfter = request.FromUtc,
                CreatedBefore = request.ToUtc,
                ChangedBy = string.IsNullOrWhiteSpace(request.User) ? null : request.User,
                ActivityType = "Message",
                MaxResults = ActivityFetchLimit,
                Order = ActivityOrder.LatestFirst,
                IncludeArchived = false
            };
            var activities = _activityQueryService.ListActivitiesAsync(query).GetAwaiter().GetResult()
                ?? Enumerable.Empty<Activity>();

            var contentTypeCache = _contentTypeRepository.List().ToDictionary(ct => ct.ID);

            foreach (var activity in activities)
            {
                var (cid, _, _) = ResolveCommentContent(activity, contentTypeCache);
                if (cid != contentId) continue;
                var dto = MapActivity(activity, contentTypeCache);
                if (dto != null) results.Add(dto);
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Could not load comments for content {ContentId}", contentId);
        }
        return results;
    }

    private static bool MatchesContentType(Activity activity, int contentTypeId)
    {
        if (activity is ContentActivity ca)
            return ca.ContentTypeId == contentTypeId;
        return false;
    }

    private static bool MatchesActionFilter(string mappedAction, string? requestedAction)
    {
        if (string.IsNullOrWhiteSpace(requestedAction)) return true;
        return string.Equals(mappedAction, requestedAction, StringComparison.OrdinalIgnoreCase);
    }

    private static string MapVersionStatus(VersionStatus status) => status switch
    {
        VersionStatus.Published => "Published",
        VersionStatus.CheckedOut => "Draft",
        VersionStatus.CheckedIn => "ReadyToPublish",
        VersionStatus.DelayedPublish => "Scheduled",
        VersionStatus.Rejected => "Rejected",
        VersionStatus.PreviouslyPublished => "PreviouslyPublished",
        _ => status.ToString()
    };

    private static string MapContentActionType(ContentActionType actionType) => actionType switch
    {
        ContentActionType.Publish => "Published",
        ContentActionType.CheckIn => "ReadyToPublish",
        ContentActionType.Save => "Draft",
        ContentActionType.DelayedPublish => "Scheduled",
        ContentActionType.Rejected => "Rejected",
        ContentActionType.Create => "Created",
        ContentActionType.Move => "Moved",
        ContentActionType.Delete or ContentActionType.DeleteLanguage or
        ContentActionType.DeleteChildren or ContentActionType.DeleteVersion or
        ContentActionType.DeletedItems => "Deleted",
        ContentActionType.RequestApproval => "ReadyToPublish",
        _ => actionType.ToString()
    };

    private static VersionStatus[]? GetStatusFilters(string? action)
    {
        if (string.IsNullOrWhiteSpace(action)) return null;
        return action switch
        {
            "Published" => new[] { VersionStatus.Published },
            "Draft" => new[] { VersionStatus.CheckedOut },
            "ReadyToPublish" => new[] { VersionStatus.CheckedIn },
            "Scheduled" => new[] { VersionStatus.DelayedPublish },
            "Rejected" => new[] { VersionStatus.Rejected },
            "PreviouslyPublished" => new[] { VersionStatus.PreviouslyPublished },
            "Comment" => null,
            _ => null
        };
    }

    private static string TruncateValue(string value, int maxLength = 500)
    {
        if (value.Length <= maxLength) return value;
        return value[..maxLength] + "...";
    }
}
