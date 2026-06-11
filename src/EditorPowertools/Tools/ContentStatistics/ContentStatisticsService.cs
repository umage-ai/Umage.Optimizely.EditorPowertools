using System.Text.Json;
using EPiServer.DataAbstraction;
using UmageAI.Optimizely.EditorPowerTools.Abstractions;
using UmageAI.Optimizely.EditorPowerTools.Infrastructure;
using UmageAI.Optimizely.EditorPowerTools.Services;
using UmageAI.Optimizely.EditorPowerTools.Tools.ContentStatistics.Models;
using Microsoft.Extensions.Logging;

namespace UmageAI.Optimizely.EditorPowerTools.Tools.ContentStatistics;

public class ContentStatisticsService
{
    private readonly IContentTypeRepository _contentTypeRepository;
    private readonly ContentTypeStatisticsRepository _statisticsRepository;
    private readonly ContentDashboardSnapshotRepository _snapshotRepository;
    private readonly IContentTypeMetadataProvider _metadataProvider;
    private readonly ILogger<ContentStatisticsService> _logger;

    public ContentStatisticsService(
        IContentTypeRepository contentTypeRepository,
        ContentTypeStatisticsRepository statisticsRepository,
        ContentDashboardSnapshotRepository snapshotRepository,
        IContentTypeMetadataProvider metadataProvider,
        ILogger<ContentStatisticsService> logger)
    {
        _contentTypeRepository = contentTypeRepository;
        _statisticsRepository = statisticsRepository;
        _snapshotRepository = snapshotRepository;
        _metadataProvider = metadataProvider;
        _logger = logger;
    }

    /// <summary>
    /// Aggregates all dashboard data in a single call. All sections come from data the
    /// unified content analysis job pre-computes — totals/distribution/blockBreakdown
    /// from <see cref="ContentTypeStatisticsRepository"/>, and creation-over-time / stale
    /// content / top editors / average versions from <see cref="ContentDashboardSnapshotRepository"/>.
    /// If the job has never run the snapshot is empty; the dashboard renders empty sections
    /// and the run-now alert tells the user to run the job.
    /// </summary>
    public ContentStatisticsDashboardDto GetDashboard()
    {
        var allStats = _statisticsRepository.GetAll().ToList();
        var contentTypeMap = _contentTypeRepository.List().ToDictionary(ct => ct.ID);
        var snapshot = _snapshotRepository.GetCurrent();

        var summary = BuildSummary(allStats, contentTypeMap, snapshot?.AverageVersionsPerItem ?? 0);
        var distribution = BuildTypeDistribution(allStats, contentTypeMap);
        var blockBreakdown = BuildBlockBreakdown(allStats, contentTypeMap);
        var creationOverTime = BuildCreationOverTime(snapshot);
        var staleContent = BuildStaleContent(snapshot);
        var topEditors = BuildTopEditors(snapshot);

        return new ContentStatisticsDashboardDto
        {
            Summary = summary,
            TypeDistribution = distribution,
            BlockBreakdown = blockBreakdown,
            CreationOverTime = creationOverTime,
            StaleContent = staleContent,
            TopEditors = topEditors
        };
    }

    private SummaryStatsDto BuildSummary(
        List<ContentTypeStatisticsRecord> allStats,
        Dictionary<int, ContentType> contentTypeMap,
        double averageVersionsPerItem)
    {
        var totalContent = 0;
        var totalPages = 0;
        var totalBlocks = 0;
        var totalMedia = 0;
        var totalContracts = 0;
        DateTime? lastAnalyzed = null;

        foreach (var stat in allStats)
        {
            totalContent += stat.ContentCount;

            if (stat.LastUpdated > (lastAnalyzed ?? DateTime.MinValue))
                lastAnalyzed = stat.LastUpdated;

            if (!contentTypeMap.TryGetValue(stat.ContentTypeId, out var ct))
                continue;

            var metadata = _metadataProvider.Get(ct);
            var baseType = ct.Base.ToString();

            if (CmsFeatureFlags.ContractsAvailable && metadata.IsContract)
                totalContracts += stat.ContentCount;
            else if (baseType == "Page")
                totalPages += stat.ContentCount;
            else if (baseType == "Block")
                totalBlocks += stat.ContentCount;
            else if (baseType == "Media" || baseType == "Image" || baseType == "Video")
                totalMedia += stat.ContentCount;
        }

        return new SummaryStatsDto
        {
            TotalContent = totalContent,
            TotalPages = totalPages,
            TotalBlocks = totalBlocks,
            TotalMedia = totalMedia,
            TotalContracts = CmsFeatureFlags.ContractsAvailable ? totalContracts : (int?)null,
            AverageVersionsPerItem = averageVersionsPerItem,
            LastAnalyzed = lastAnalyzed
        };
    }

    private IReadOnlyList<ContentTypeDistributionDto> BuildTypeDistribution(
        List<ContentTypeStatisticsRecord> allStats,
        Dictionary<int, ContentType> contentTypeMap)
    {
        var categories = new Dictionary<string, int>
        {
            ["Pages"] = 0,
            ["Blocks"] = 0,
            ["Media"] = 0,
            ["Contracts"] = 0,
            ["Other"] = 0
        };

        foreach (var stat in allStats)
        {
            if (!contentTypeMap.TryGetValue(stat.ContentTypeId, out var ct))
            {
                categories["Other"] += stat.ContentCount;
                continue;
            }

            var metadata = _metadataProvider.Get(ct);
            var baseType = ct.Base.ToString();

            var category = (CmsFeatureFlags.ContractsAvailable && metadata.IsContract)
                ? "Contracts"
                : baseType switch
                {
                    "Page" => "Pages",
                    "Block" => "Blocks",
                    "Media" or "Image" or "Video" => "Media",
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

#pragma warning disable CS0162 // Unreachable code — CmsFeatureFlags.ContractsAvailable is a compile-time constant per TFM.
    private BlockBreakdownDto? BuildBlockBreakdown(
        List<ContentTypeStatisticsRecord> allStats,
        Dictionary<int, ContentType> contentTypeMap)
    {
        if (!CmsFeatureFlags.ContractsAvailable) return null;

        int sections = 0, elements = 0, plain = 0;
        foreach (var stat in allStats)
        {
            if (!contentTypeMap.TryGetValue(stat.ContentTypeId, out var ct)) continue;
            if (ct.Base.ToString() != "Block") continue;

            var m = _metadataProvider.Get(ct);
            var hasSection = m.CompositionBehaviors.Contains("SectionEnabled");
            var hasElement = m.CompositionBehaviors.Contains("ElementEnabled");

            if (hasSection) sections += stat.ContentCount;
            if (hasElement) elements += stat.ContentCount;
            if (!hasSection && !hasElement) plain += stat.ContentCount;
        }

        return new BlockBreakdownDto { Sections = sections, Elements = elements, Plain = plain };
    }
#pragma warning restore CS0162

    private IReadOnlyList<ContentCreationMonthDto> BuildCreationOverTime(ContentDashboardSnapshotRecord? snapshot)
    {
        if (string.IsNullOrEmpty(snapshot?.CreationByMonthJson))
            return Array.Empty<ContentCreationMonthDto>();

        try
        {
            var dict = JsonSerializer.Deserialize<Dictionary<string, int>>(snapshot.CreationByMonthJson)
                       ?? new Dictionary<string, int>();
            return dict
                .OrderBy(kv => kv.Key)
                .Select(kv => new ContentCreationMonthDto { Month = kv.Key, Count = kv.Value })
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to deserialise CreationByMonthJson from dashboard snapshot.");
            return Array.Empty<ContentCreationMonthDto>();
        }
    }

    private IReadOnlyList<StaleContentDto> BuildStaleContent(ContentDashboardSnapshotRecord? snapshot)
    {
        if (string.IsNullOrEmpty(snapshot?.StaleContentJson))
            return Array.Empty<StaleContentDto>();

        try
        {
            var items = JsonSerializer.Deserialize<List<StaleContentSnapshotItem>>(snapshot.StaleContentJson)
                        ?? new List<StaleContentSnapshotItem>();
            var now = DateTime.UtcNow;
            return items
                .Select(i => new StaleContentDto
                {
                    ContentId = i.ContentId,
                    Name = i.Name,
                    ContentTypeName = i.ContentTypeName,
                    LastModified = i.LastModified,
                    DaysSinceModified = (int)(now - i.LastModified).TotalDays,
                    EditUrl = $"{EditorPowertoolsShellPaths.CmsRoot()}#context=epi.cms.contentdata:///{i.ContentId}"
                })
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to deserialise StaleContentJson from dashboard snapshot.");
            return Array.Empty<StaleContentDto>();
        }
    }

    private IReadOnlyList<EditorActivityDto> BuildTopEditors(ContentDashboardSnapshotRecord? snapshot)
    {
        if (string.IsNullOrEmpty(snapshot?.TopEditorsJson))
            return Array.Empty<EditorActivityDto>();

        try
        {
            var items = JsonSerializer.Deserialize<List<EditorActivitySnapshotItem>>(snapshot.TopEditorsJson)
                        ?? new List<EditorActivitySnapshotItem>();
            return items
                .Select(i => new EditorActivityDto
                {
                    Username = i.Username,
                    EditCount = i.EditCount,
                    PublishCount = i.PublishCount,
                    LastActive = i.LastActive
                })
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to deserialise TopEditorsJson from dashboard snapshot.");
            return Array.Empty<EditorActivityDto>();
        }
    }
}
