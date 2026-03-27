using EPiServer;
using EPiServer.Core;
using EPiServer.PlugIn;
using EPiServer.Scheduler;
using Microsoft.Extensions.Logging;

namespace EditorPowertools.Services;

[ScheduledPlugIn(
    DisplayName = "[EditorPowertools] Content Analysis",
    Description = "Unified job that traverses all content once and runs all registered analyzers (content type stats, personalization, link checking, etc.).",
    SortIndex = 10000)]
public class UnifiedContentAnalysisJob : ScheduledJobBase
{
    private readonly IContentRepository _contentRepository;
    private readonly IContentLoader _contentLoader;
    private readonly IEnumerable<IContentAnalyzer> _analyzers;
    private readonly ILogger<UnifiedContentAnalysisJob> _logger;
    private bool _stopSignaled;

    public UnifiedContentAnalysisJob(
        IContentRepository contentRepository,
        IContentLoader contentLoader,
        IEnumerable<IContentAnalyzer> analyzers,
        ILogger<UnifiedContentAnalysisJob> logger)
    {
        _contentRepository = contentRepository;
        _contentLoader = contentLoader;
        _analyzers = analyzers;
        _logger = logger;
        IsStoppable = true;
    }

    public override string Execute()
    {
        _stopSignaled = false;
        var analyzerList = _analyzers.ToList();

        if (analyzerList.Count == 0)
            return "No analyzers registered.";

        OnStatusChanged($"Initializing {analyzerList.Count} analyzers...");
        foreach (var analyzer in analyzerList)
        {
            try { analyzer.Initialize(); }
            catch (Exception ex) { _logger.LogError(ex, "Error initializing {Analyzer}", analyzer.Name); }
        }

        var descendants = _contentRepository.GetDescendents(ContentReference.RootPage).ToList();
        var total = descendants.Count;
        var processed = 0;

        OnStatusChanged($"Analyzing {total} content items with {analyzerList.Count} analyzers...");

        foreach (var contentRef in descendants)
        {
            if (_stopSignaled)
            {
                return $"Stopped after {processed}/{total} items.";
            }

            try
            {
                if (!_contentLoader.TryGet<IContent>(contentRef, out var content))
                    continue;

                foreach (var analyzer in analyzerList)
                {
                    try { analyzer.Analyze(content, contentRef); }
                    catch (Exception ex) { _logger.LogWarning(ex, "Error in {Analyzer} for {ContentRef}", analyzer.Name, contentRef); }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error loading content {ContentRef}", contentRef);
            }

            processed++;
            if (processed % 100 == 0)
                OnStatusChanged($"Processed {processed}/{total} content items...");
        }

        OnStatusChanged("Completing analyzers...");
        foreach (var analyzer in analyzerList)
        {
            try { analyzer.Complete(); }
            catch (Exception ex) { _logger.LogError(ex, "Error completing {Analyzer}", analyzer.Name); }
        }

        return $"Completed. Analyzed {processed} content items with {analyzerList.Count} analyzers ({string.Join(", ", analyzerList.Select(a => a.Name))}).";
    }

    public override void Stop()
    {
        _stopSignaled = true;
        base.Stop();
    }
}
