using EPiServer;
using EPiServer.Core;
using EPiServer.Framework.Localization;
using EPiServer.PlugIn;
using EPiServer.Scheduler;
using Microsoft.Extensions.Logging;

namespace UmageAI.Optimizely.EditorPowerTools.Services;

[ScheduledPlugIn(
    DisplayName = "[UmageAI.Optimizely.EditorPowerTools] Content Analysis",
    Description = "Unified job that traverses all content once and runs all registered analyzers (content type stats, personalization, link checking, etc.).",
    LanguagePath = "/editorpowertools/jobs/contentanalysis",
    SortIndex = 10000)]
public class UnifiedContentAnalysisJob : ScheduledJobBase
{
    private readonly IContentRepository _contentRepository;
    private readonly IContentLoader _contentLoader;
    private readonly IEnumerable<IContentAnalyzer> _analyzers;
    private readonly ILogger<UnifiedContentAnalysisJob> _logger;
    private readonly LocalizationService _localization;
    private bool _stopSignaled;

    public UnifiedContentAnalysisJob(
        IContentRepository contentRepository,
        IContentLoader contentLoader,
        IEnumerable<IContentAnalyzer> analyzers,
        ILogger<UnifiedContentAnalysisJob> logger,
        LocalizationService localization)
    {
        _contentRepository = contentRepository;
        _contentLoader = contentLoader;
        _analyzers = analyzers;
        _logger = logger;
        _localization = localization;
        IsStoppable = true;
    }

    private string L(string path, string fallback) =>
        _localization.GetStringByCulture(path, fallback, System.Globalization.CultureInfo.CurrentUICulture);

    private const string Prefix = "/editorpowertools/jobs/contentanalysis/";

    public override string Execute()
    {
        _stopSignaled = false;
        var analyzerList = _analyzers.ToList();

        if (analyzerList.Count == 0)
            return L(Prefix + "noanalyzers", "No analyzers registered.");

        OnStatusChanged(string.Format(L(Prefix + "initializing", "Initializing {0} analyzers..."), analyzerList.Count));
        foreach (var analyzer in analyzerList)
        {
            try { analyzer.Initialize(); }
            catch (Exception ex) { _logger.LogError(ex, "Error initializing {Analyzer}", analyzer.Name); }
        }

        var descendants = _contentRepository.GetDescendents(ContentReference.RootPage).ToList();
        var total = descendants.Count;
        var processed = 0;

        OnStatusChanged(string.Format(L(Prefix + "analyzing", "Analyzing {0} content items with {1} analyzers..."), total, analyzerList.Count));

        foreach (var contentRef in descendants)
        {
            if (_stopSignaled)
            {
                return string.Format(L(Prefix + "stopped", "Stopped after {0}/{1} items."), processed, total);
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
                OnStatusChanged(string.Format(L(Prefix + "processing", "Processed {0}/{1} content items..."), processed, total));
        }

        OnStatusChanged(L(Prefix + "completing", "Completing analyzers..."));
        foreach (var analyzer in analyzerList)
        {
            try { analyzer.Complete(); }
            catch (Exception ex) { _logger.LogError(ex, "Error completing {Analyzer}", analyzer.Name); }
        }

        return string.Format(L(Prefix + "completed", "Completed. Analyzed {0} content items with {1} analyzers ({2})."),
            processed, analyzerList.Count, string.Join(", ", analyzerList.Select(a => a.Name)));
    }

    public override void Stop()
    {
        _stopSignaled = true;
        base.Stop();
    }
}
