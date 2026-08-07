using EPiServer;
using EPiServer.Core;
using EPiServer.Framework.Localization;
#if !OPTIMIZELY_CMS13
using EPiServer.PlugIn;
#endif
using EPiServer.Scheduler;
using Microsoft.Extensions.Logging;

namespace UmageAI.Optimizely.EditorPowerTools.Services;

#if OPTIMIZELY_CMS13
[ScheduledJobAttribute(
    DisplayName = "[EditorPowertools] Content Analysis",
    Description = "Unified job that traverses all content once and runs all registered analyzers (content type stats, personalization, link checking, etc.).",
    LanguagePath = "/editorpowertools/jobs/contentanalysis")]
#else
[ScheduledPlugIn(
    DisplayName = "[EditorPowertools] Content Analysis",
    Description = "Unified job that traverses all content once and runs all registered analyzers (content type stats, personalization, link checking, etc.).",
    LanguagePath = "/editorpowertools/jobs/contentanalysis",
    SortIndex = 10000)]
#endif
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
        var failedAnalyzers = new List<IContentAnalyzer>();
        foreach (var analyzer in analyzerList)
        {
            try { analyzer.Initialize(); }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error initializing {Analyzer}", analyzer.Name);
                failedAnalyzers.Add(analyzer);
            }
        }

        // An analyzer whose Initialize() failed didn't clear its prior data (e.g. a DDS delete
        // timed out) — running Analyze()/Complete() for it would append new rows on top of
        // stale ones, so skip it entirely for this run instead of silently compounding the data.
        var activeAnalyzers = analyzerList.Except(failedAnalyzers).ToList();

        var descendants = _contentRepository.GetDescendents(ContentReference.RootPage).ToList();
        var total = descendants.Count;
        var processed = 0;

        OnStatusChanged(string.Format(L(Prefix + "analyzing", "Analyzing {0} content items with {1} analyzers..."), total, activeAnalyzers.Count));

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

                foreach (var analyzer in activeAnalyzers)
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
        foreach (var analyzer in activeAnalyzers)
        {
            try { analyzer.Complete(); }
            catch (Exception ex) { _logger.LogError(ex, "Error completing {Analyzer}", analyzer.Name); }
        }

        var message = string.Format(L(Prefix + "completed", "Completed. Analyzed {0} content items with {1} analyzers ({2})."),
            processed, activeAnalyzers.Count, string.Join(", ", activeAnalyzers.Select(a => a.Name)));

        if (failedAnalyzers.Count > 0)
        {
            message += " " + string.Format(
                L(Prefix + "initfailed", "Skipped {0} analyzer(s) due to initialization errors: {1}."),
                failedAnalyzers.Count,
                string.Join(", ", failedAnalyzers.Select(a => a.Name)));
        }

        return message;
    }

    public override void Stop()
    {
        _stopSignaled = true;
        base.Stop();
    }
}
