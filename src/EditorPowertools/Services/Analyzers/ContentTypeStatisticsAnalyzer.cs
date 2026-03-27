using EPiServer;
using EPiServer.Core;
using EPiServer.DataAbstraction;
using Microsoft.Extensions.Logging;

namespace EditorPowertools.Services.Analyzers;

/// <summary>
/// Analyzer that counts content per type and checks published/referenced status.
/// Accumulates counts during Analyze() and flushes them to DDS in Finalize().
/// </summary>
public class ContentTypeStatisticsAnalyzer : IContentAnalyzer
{
    private readonly IContentTypeRepository _contentTypeRepository;
    private readonly IContentModelUsage _contentModelUsage;
    private readonly IContentLoader _contentLoader;
    private readonly IContentSoftLinkRepository _softLinkRepository;
    private readonly ContentTypeStatisticsRepository _statisticsRepository;
    private readonly ILogger<ContentTypeStatisticsAnalyzer> _logger;

    // Accumulated statistics per content type ID
    private readonly Dictionary<int, TypeStats> _stats = new();

    public string Name => "Content Type Statistics";

    public ContentTypeStatisticsAnalyzer(
        IContentTypeRepository contentTypeRepository,
        IContentModelUsage contentModelUsage,
        IContentLoader contentLoader,
        IContentSoftLinkRepository softLinkRepository,
        ContentTypeStatisticsRepository statisticsRepository,
        ILogger<ContentTypeStatisticsAnalyzer> logger)
    {
        _contentTypeRepository = contentTypeRepository;
        _contentModelUsage = contentModelUsage;
        _contentLoader = contentLoader;
        _softLinkRepository = softLinkRepository;
        _statisticsRepository = statisticsRepository;
        _logger = logger;
    }

    public void Initialize()
    {
        _stats.Clear();
    }

    public void Analyze(IContent content, ContentReference contentRef)
    {
        var contentTypeId = content.ContentTypeID;

        if (!_stats.TryGetValue(contentTypeId, out var typeStats))
        {
            typeStats = new TypeStats();
            _stats[contentTypeId] = typeStats;
        }

        typeStats.ContentCount++;

        // Check if published
        if (content is IVersionable versionable &&
            versionable.Status == VersionStatus.Published)
        {
            typeStats.PublishedCount++;
        }

        // Check if referenced by other content
        try
        {
            var softLinks = _softLinkRepository.Load(contentRef, true);
            if (softLinks != null && softLinks.Any(sl =>
                !sl.OwnerContentLink.CompareToIgnoreWorkID(contentRef)))
            {
                typeStats.ReferencedCount++;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error checking references for {ContentRef}", contentRef);
        }
    }

    public void Complete()
    {
        foreach (var (contentTypeId, stats) in _stats)
        {
            try
            {
                _statisticsRepository.SaveOrUpdate(new ContentTypeStatisticsRecord
                {
                    ContentTypeId = contentTypeId,
                    ContentCount = stats.ContentCount,
                    PublishedCount = stats.PublishedCount,
                    ReferencedCount = stats.ReferencedCount,
                    UnreferencedCount = stats.ContentCount - stats.ReferencedCount,
                    LastUpdated = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving statistics for content type {ContentTypeId}", contentTypeId);
            }
        }
    }

    private class TypeStats
    {
        public int ContentCount { get; set; }
        public int PublishedCount { get; set; }
        public int ReferencedCount { get; set; }
    }
}
