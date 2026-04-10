using EPiServer;
using EPiServer.Core;
using EPiServer.DataAbstraction;
using EPiServer.Shell;
using UmageAI.Optimizely.EditorPowerTools.Services;
using UmageAI.Optimizely.EditorPowerTools.Tools.ContentStatistics.Models;
using Microsoft.Extensions.Logging;

namespace UmageAI.Optimizely.EditorPowerTools.Tools.ContentStatistics;

public class ContentStatisticsService
{
    private readonly IContentRepository _contentRepository;
    private readonly IContentVersionRepository _versionRepository;
    private readonly IContentTypeRepository _contentTypeRepository;
    private readonly ContentTypeStatisticsRepository _statisticsRepository;
    private readonly ILogger<ContentStatisticsService> _logger;

    public ContentStatisticsService(
        IContentRepository contentRepository,
        IContentVersionRepository versionRepository,
        IContentTypeRepository contentTypeRepository,
        ContentTypeStatisticsRepository statisticsRepository,
        ILogger<ContentStatisticsService> logger)
    {
        _contentRepository = contentRepository;
        _versionRepository = versionRepository;
        _contentTypeRepository = contentTypeRepository;
        _statisticsRepository = statisticsRepository;
        _logger = logger;
    }

    /// <summary>
    /// Aggregates all dashboard data in a single call.
    /// </summary>
    public ContentStatisticsDashboardDto GetDashboard()
    {
        var allStats = _statisticsRepository.GetAll().ToList();
        var contentTypes = _contentTypeRepository.List().ToList();
        var contentTypeMap = contentTypes.ToDictionary(ct => ct.ID);

        // Build summary and distribution from pre-computed stats
        var summary = BuildSummary(allStats, contentTypeMap);
        var distribution = BuildTypeDistribution(allStats, contentTypeMap);

        // Scan content for creation-over-time, staleness, and editor activity
        var descendants = _contentRepository.GetDescendents(ContentReference.RootPage).ToList();

        var creationOverTime = BuildCreationOverTime(descendants);
        var staleContent = BuildStaleContent(descendants, contentTypeMap);
        var topEditors = BuildEditorActivity(descendants);

        return new ContentStatisticsDashboardDto
        {
            Summary = summary,
            TypeDistribution = distribution,
            CreationOverTime = creationOverTime,
            StaleContent = staleContent,
            TopEditors = topEditors
        };
    }

    private SummaryStatsDto BuildSummary(
        List<ContentTypeStatisticsRecord> allStats,
        Dictionary<int, ContentType> contentTypeMap)
    {
        var totalContent = 0;
        var totalPages = 0;
        var totalBlocks = 0;
        var totalMedia = 0;
        DateTime? lastAnalyzed = null;

        foreach (var stat in allStats)
        {
            totalContent += stat.ContentCount;

            if (stat.LastUpdated > (lastAnalyzed ?? DateTime.MinValue))
                lastAnalyzed = stat.LastUpdated;

            if (!contentTypeMap.TryGetValue(stat.ContentTypeId, out var ct))
                continue;

            var baseType = ct.Base.ToString();
            if (baseType == "Page")
                totalPages += stat.ContentCount;
            else if (baseType == "Block")
                totalBlocks += stat.ContentCount;
            else if (baseType == "Media")
                totalMedia += stat.ContentCount;
        }

        // Compute average versions per item from a sample
        var averageVersions = ComputeAverageVersions();

        return new SummaryStatsDto
        {
            TotalContent = totalContent,
            TotalPages = totalPages,
            TotalBlocks = totalBlocks,
            TotalMedia = totalMedia,
            AverageVersionsPerItem = averageVersions,
            LastAnalyzed = lastAnalyzed
        };
    }

    private double ComputeAverageVersions()
    {
        // Sample up to 200 content items for average version count
        var descendants = _contentRepository.GetDescendents(ContentReference.RootPage)
            .Take(200)
            .ToList();

        if (descendants.Count == 0) return 0;

        var totalVersions = 0;
        var counted = 0;

        foreach (var contentRef in descendants)
        {
            try
            {
                var versions = _versionRepository.List(contentRef);
                totalVersions += versions.Count();
                counted++;
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Could not list versions for {ContentRef}", contentRef);
            }
        }

        return counted > 0 ? Math.Round((double)totalVersions / counted, 1) : 0;
    }

    private static IReadOnlyList<ContentTypeDistributionDto> BuildTypeDistribution(
        List<ContentTypeStatisticsRecord> allStats,
        Dictionary<int, ContentType> contentTypeMap)
    {
        var categories = new Dictionary<string, int>
        {
            ["Pages"] = 0,
            ["Blocks"] = 0,
            ["Media"] = 0,
            ["Other"] = 0
        };

        foreach (var stat in allStats)
        {
            if (!contentTypeMap.TryGetValue(stat.ContentTypeId, out var ct))
            {
                categories["Other"] += stat.ContentCount;
                continue;
            }

            var baseType = ct.Base.ToString();
            var category = baseType switch
            {
                "Page" => "Pages",
                "Block" => "Blocks",
                "Media" => "Media",
                _ => "Other"
            };
            categories[category] += stat.ContentCount;
        }

        return categories
            .Where(kv => kv.Value > 0)
            .Select(kv => new ContentTypeDistributionDto
            {
                Category = kv.Key,
                Count = kv.Value
            })
            .OrderByDescending(d => d.Count)
            .ToList();
    }

    private IReadOnlyList<ContentCreationMonthDto> BuildCreationOverTime(
        List<ContentReference> descendants)
    {
        var cutoff = DateTime.UtcNow.AddMonths(-12);
        var monthlyCounts = new Dictionary<string, int>();

        // Pre-fill 12 months
        for (var i = 11; i >= 0; i--)
        {
            var date = DateTime.UtcNow.AddMonths(-i);
            var key = date.ToString("yyyy-MM");
            monthlyCounts[key] = 0;
        }

        foreach (var contentRef in descendants)
        {
            try
            {
                if (!_contentRepository.TryGet<IContent>(contentRef, out var content))
                    continue;

                if (content is IChangeTrackable trackable && trackable.Created >= cutoff)
                {
                    var key = trackable.Created.ToString("yyyy-MM");
                    if (monthlyCounts.ContainsKey(key))
                        monthlyCounts[key]++;
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Could not load content {ContentRef}", contentRef);
            }
        }

        return monthlyCounts
            .OrderBy(kv => kv.Key)
            .Select(kv => new ContentCreationMonthDto
            {
                Month = kv.Key,
                Count = kv.Value
            })
            .ToList();
    }

    private IReadOnlyList<StaleContentDto> BuildStaleContent(
        List<ContentReference> descendants,
        Dictionary<int, ContentType> contentTypeMap)
    {
        var items = new List<(IContent Content, DateTime LastModified)>();

        foreach (var contentRef in descendants)
        {
            try
            {
                if (!_contentRepository.TryGet<IContent>(contentRef, out var content))
                    continue;

                // Only include pages for staleness analysis
                if (content is not PageData)
                    continue;

                var lastModified = (content as IChangeTrackable)?.Changed ?? DateTime.MinValue;
                items.Add((content, lastModified));
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Could not load content {ContentRef}", contentRef);
            }
        }

        return items
            .OrderBy(i => i.LastModified)
            .Take(20)
            .Select(i =>
            {
                var ct = contentTypeMap.GetValueOrDefault(i.Content.ContentTypeID);
                return new StaleContentDto
                {
                    ContentId = i.Content.ContentLink.ID,
                    Name = i.Content.Name ?? "[No name]",
                    ContentTypeName = ct?.DisplayName ?? ct?.Name ?? "Unknown",
                    LastModified = i.LastModified,
                    DaysSinceModified = (int)(DateTime.UtcNow - i.LastModified).TotalDays,
                    EditUrl = $"{Paths.ToResource("CMS", "")}#context=epi.cms.contentdata:///{i.Content.ContentLink.ID}"
                };
            })
            .ToList();
    }

    private IReadOnlyList<EditorActivityDto> BuildEditorActivity(
        List<ContentReference> descendants)
    {
        var editorStats = new Dictionary<string, (int EditCount, int PublishCount, DateTime LastActive)>(
            StringComparer.OrdinalIgnoreCase);

        foreach (var contentRef in descendants)
        {
            try
            {
                var versions = _versionRepository.List(contentRef);
                foreach (var version in versions)
                {
                    var user = version.SavedBy;
                    if (string.IsNullOrEmpty(user)) continue;

                    if (!editorStats.TryGetValue(user, out var existing))
                    {
                        existing = (0, 0, DateTime.MinValue);
                    }

                    var editCount = existing.EditCount + 1;
                    var publishCount = existing.PublishCount +
                        (version.Status == VersionStatus.Published ? 1 : 0);
                    var lastActive = version.Saved > existing.LastActive
                        ? version.Saved
                        : existing.LastActive;

                    editorStats[user] = (editCount, publishCount, lastActive);
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Could not list versions for {ContentRef}", contentRef);
            }
        }

        return editorStats
            .OrderByDescending(kv => kv.Value.EditCount)
            .Take(10)
            .Select(kv => new EditorActivityDto
            {
                Username = kv.Key,
                EditCount = kv.Value.EditCount,
                PublishCount = kv.Value.PublishCount,
                LastActive = kv.Value.LastActive != DateTime.MinValue ? kv.Value.LastActive : null
            })
            .ToList();
    }
}
