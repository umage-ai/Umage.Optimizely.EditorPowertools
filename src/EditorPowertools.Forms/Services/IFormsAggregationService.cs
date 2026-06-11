using UmageAI.Optimizely.EditorPowerTools.Forms.Tools.FormsOverview.Models;

namespace UmageAI.Optimizely.EditorPowerTools.Forms.Services;

/// <summary>
/// Aggregates form metadata, submission counts, usage and privacy analysis.
/// Abstracted behind an interface so the tools and CMS Doctor checks that
/// depend on it can be unit-tested with a mock instead of the EPiServer stack.
/// </summary>
public interface IFormsAggregationService
{
    /// <summary>One row per form (master version) with stats, usage and risk flags.</summary>
    IReadOnlyList<FormSummaryDto> GetForms();

    /// <summary>Most recent submissions across all forms, newest first.</summary>
    IReadOnlyList<SubmissionEventDto> GetSubmissionsTimeline(int top, int days, Guid? formGuid = null, bool includeData = false);

    /// <summary>Lightweight list of forms for the timeline filter dropdown.</summary>
    IReadOnlyList<FormChoiceDto> GetFormChoices();

    /// <summary>Per-form PII analysis used by the privacy CMS Doctor checks.</summary>
    IReadOnlyList<FormPiiAnalysisDto> AnalyzePii();
}
