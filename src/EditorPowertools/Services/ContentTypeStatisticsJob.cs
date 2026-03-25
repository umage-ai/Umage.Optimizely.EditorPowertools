using EPiServer;
using EPiServer.Core;
using EPiServer.DataAbstraction;
using EPiServer.PlugIn;
using EPiServer.Scheduler;
using Microsoft.Extensions.Logging;

namespace EditorPowertools.Services;

[ScheduledPlugIn(
    DisplayName = "[EditorPowertools] Aggregate Content Type Statistics",
    Description = "Traverses all content and aggregates statistics per content type. Used by Content Type Audit and other tools.",
    SortIndex = 10000)]
public class ContentTypeStatisticsJob : ScheduledJobBase
{
    private readonly IContentTypeRepository _contentTypeRepository;
    private readonly IContentModelUsage _contentModelUsage;
    private readonly IContentLoader _contentLoader;
    private readonly IContentSoftLinkRepository _softLinkRepository;
    private readonly ContentTypeStatisticsRepository _statisticsRepository;
    private readonly ILogger<ContentTypeStatisticsJob> _logger;
    private bool _stopSignaled;

    public ContentTypeStatisticsJob(
        IContentTypeRepository contentTypeRepository,
        IContentModelUsage contentModelUsage,
        IContentLoader contentLoader,
        IContentSoftLinkRepository softLinkRepository,
        ContentTypeStatisticsRepository statisticsRepository,
        ILogger<ContentTypeStatisticsJob> logger)
    {
        _contentTypeRepository = contentTypeRepository;
        _contentModelUsage = contentModelUsage;
        _contentLoader = contentLoader;
        _softLinkRepository = softLinkRepository;
        _statisticsRepository = statisticsRepository;
        _logger = logger;
        IsStoppable = true;
    }

    public override string Execute()
    {
        _stopSignaled = false;
        var contentTypes = _contentTypeRepository.List().ToList();
        var processed = 0;
        var total = contentTypes.Count;

        OnStatusChanged($"Processing {total} content types...");

        foreach (var contentType in contentTypes)
        {
            if (_stopSignaled)
                return $"Job stopped after processing {processed}/{total} content types.";

            try
            {
                var usages = _contentModelUsage.ListContentOfContentType(contentType)
                    .DistinctBy(cu => cu.ContentLink.ToReferenceWithoutVersion().ToString() + "-" + cu.LanguageBranch)
                    .ToList();

                var contentCount = usages.Count;
                var publishedCount = 0;
                var referencedCount = 0;

                foreach (var usage in usages)
                {
                    if (_stopSignaled)
                        return $"Job stopped after processing {processed}/{total} content types.";

                    try
                    {
                        var contentRef = usage.ContentLink.ToReferenceWithoutVersion();

                        // Check if published
                        if (_contentLoader.TryGet<IContent>(contentRef, out var content))
                        {
                            if (content is IVersionable versionable &&
                                versionable.Status == VersionStatus.Published)
                            {
                                publishedCount++;
                            }
                        }

                        // Check if referenced by other content
                        var softLinks = _softLinkRepository.Load(contentRef, true);
                        if (softLinks != null && softLinks.Any(sl =>
                            !sl.OwnerContentLink.CompareToIgnoreWorkID(contentRef)))
                        {
                            referencedCount++;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Error processing content {ContentLink} for type {ContentType}",
                            usage.ContentLink, contentType.Name);
                    }
                }

                _statisticsRepository.SaveOrUpdate(new ContentTypeStatisticsRecord
                {
                    ContentTypeId = contentType.ID,
                    ContentCount = contentCount,
                    PublishedCount = publishedCount,
                    ReferencedCount = referencedCount,
                    UnreferencedCount = contentCount - referencedCount,
                    LastUpdated = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing content type {ContentType}", contentType.Name);
            }

            processed++;
            if (processed % 10 == 0)
                OnStatusChanged($"Processed {processed}/{total} content types...");
        }

        return $"Completed. Processed {processed} content types.";
    }

    public override void Stop()
    {
        _stopSignaled = true;
        base.Stop();
    }
}
