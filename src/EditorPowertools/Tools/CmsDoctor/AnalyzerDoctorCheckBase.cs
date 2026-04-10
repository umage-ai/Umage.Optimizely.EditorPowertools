using EPiServer.Core;
using UmageAI.Optimizely.EditorPowerTools.Services;
using UmageAI.Optimizely.EditorPowerTools.Tools.CmsDoctor.Models;

namespace UmageAI.Optimizely.EditorPowerTools.Tools.CmsDoctor;

/// <summary>
/// Base class for health checks that need to traverse all content via the unified scheduled job.
/// Implements both IDoctorCheck (for the CMS Doctor dashboard) and IContentAnalyzer
/// (for the scheduled job). Data is collected during job traversal and used when
/// PerformCheck() is called on the dashboard.
///
/// Third-party packages can inherit from this to create checks that hook into the job.
/// Register as both IDoctorCheck and IContentAnalyzer in DI.
/// </summary>
public abstract class AnalyzerDoctorCheckBase : DoctorCheckBase, IContentAnalyzer
{
    /// <summary>Whether the analyzer has run at least once.</summary>
    protected bool HasAnalyzed { get; private set; }

    /// <summary>When the last analysis completed.</summary>
    protected DateTime? LastAnalyzed { get; private set; }

    // ── IContentAnalyzer ──

    /// <summary>Called before traversal. Override to clear accumulated data.</summary>
    public virtual void Initialize()
    {
        HasAnalyzed = false;
        OnInitialize();
    }

    /// <summary>Called for each content item during traversal.</summary>
    public void Analyze(IContent content, ContentReference contentRef)
    {
        OnAnalyze(content, contentRef);
    }

    /// <summary>Called after traversal completes.</summary>
    public void Complete()
    {
        HasAnalyzed = true;
        LastAnalyzed = DateTime.UtcNow;
        OnComplete();
    }

    // ── IDoctorCheck ──

    public override DoctorCheckResult PerformCheck()
    {
        if (!HasAnalyzed)
            return new DoctorCheckResult
            {
                CheckName = Name,
                CheckType = GetType().FullName ?? GetType().Name,
                Group = Group,
                Status = HealthStatus.NotChecked,
                StatusText = L("/editorpowertools/cmsdoctor/runjobfirst", "Run the '[EditorPowertools] Content Analysis' scheduled job first."),
                Tags = Tags,
                CanFix = false
            };

        return EvaluateResults();
    }

    // ── Override these ──

    /// <summary>Clear any accumulated data before a new traversal.</summary>
    protected abstract void OnInitialize();

    /// <summary>Process a single content item during traversal.</summary>
    protected abstract void OnAnalyze(IContent content, ContentReference contentRef);

    /// <summary>Finalize after traversal (optional). Default does nothing.</summary>
    protected virtual void OnComplete() { }

    /// <summary>Evaluate the accumulated data and return a health check result.</summary>
    protected abstract DoctorCheckResult EvaluateResults();
}
