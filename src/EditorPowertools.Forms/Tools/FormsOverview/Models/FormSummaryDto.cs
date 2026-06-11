namespace UmageAI.Optimizely.EditorPowerTools.Forms.Tools.FormsOverview.Models;

/// <summary>
/// One row in the Forms Overview list.
/// </summary>
public class FormSummaryDto
{
    public int ContentId { get; set; }
    public Guid FormGuid { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Language { get; set; }
    public string? Breadcrumb { get; set; }
    public string? EditUrl { get; set; }

    public int FieldCount { get; set; }
    public int SubmissionCount { get; set; }
    public DateTime? LastSubmissionUtc { get; set; }

    /// <summary>True if the form has a published (live) version.</summary>
    public bool IsPublished { get; set; }

    /// <summary>True if the form is configured to persist submission data (AllowToStoreSubmissionData).</summary>
    public bool StoresSubmissionData { get; set; }

    /// <summary>Input fields whose label appears more than once on the same form
    /// (their submission columns collide / are ambiguous). Empty when none.</summary>
    public List<string> DuplicateFieldLabels { get; set; } = new();

    /// <summary>Convenience flag — true when <see cref="DuplicateFieldLabels"/> is non-empty.</summary>
    public bool HasDuplicateFields => DuplicateFieldLabels.Count > 0;

    /// <summary>Labels of fields that look like they capture personal / GDPR-sensitive data.</summary>
    public List<string> PiiFieldLabels { get; set; } = new();

    /// <summary>True when the form captures at least one PII-shaped field.</summary>
    public bool CapturesPii => PiiFieldLabels.Count > 0;

    /// <summary>
    /// True when this form is a privacy/GDPR risk worth flagging: it captures
    /// PII-shaped fields AND stores submission data AND runs on the default
    /// (effectively indefinite) retention policy. When the form is also published
    /// and already has submissions, that personal data is very likely sitting in
    /// storage forever — see <see cref="PrivacyRiskIsLive"/>.
    /// </summary>
    public bool PrivacyRisk => CapturesPii && StoresSubmissionData && UsesDefaultRetention;

    /// <summary>True when a <see cref="PrivacyRisk"/> form is published and already
    /// holds submissions — i.e. old personal data most likely already exists.</summary>
    public bool PrivacyRiskIsLive => PrivacyRisk && IsPublished && SubmissionCount > 0;

    /// <summary>Number of pages/blocks that reference this form.</summary>
    public int UsageCount { get; set; }

    /// <summary>Top usage locations (content name + edit URL).</summary>
    public List<FormUsageDto> Usage { get; set; } = new();

    /// <summary>Retention policy key for partial (non-finalized) submissions.</summary>
    public string? PartialRetentionPolicy { get; set; }

    /// <summary>Retention policy key for finalized submissions.</summary>
    public string? FinalizedRetentionPolicy { get; set; }

    /// <summary>True if at least one of the retention keys is "EPiServer.RetentionPolicy.Default" or unset.</summary>
    public bool UsesDefaultRetention { get; set; }

    /// <summary>True if the form has at least one email-template post-submission actor configured.</summary>
    public bool HasEmailHandler { get; set; }

    /// <summary>Number of configured email-template handlers (informational).</summary>
    public int EmailHandlerCount { get; set; }

    /// <summary>True if the form has at least one webhook post-submission actor configured.</summary>
    public bool HasWebhookHandler { get; set; }

    /// <summary>Number of configured webhook handlers (informational).</summary>
    public int WebhookHandlerCount { get; set; }
}

public class FormUsageDto
{
    public int OwnerContentId { get; set; }
    public string OwnerName { get; set; } = string.Empty;
    public string? OwnerTypeName { get; set; }
    public string? Language { get; set; }
    public string? EditUrl { get; set; }
}

/// <summary>
/// One entry in the cross-form submissions timeline.
/// </summary>
public class SubmissionEventDto
{
    public string SubmissionId { get; set; } = string.Empty;
    public Guid FormGuid { get; set; }
    public int FormContentId { get; set; }
    public string FormName { get; set; } = string.Empty;
    public string? FormEditUrl { get; set; }
    public string? SubmissionViewUrl { get; set; }
    public DateTime SubmittedUtc { get; set; }
    public string? SubmittedBy { get; set; }
    public string? HostedPageUrl { get; set; }
    public string? Language { get; set; }
    public bool Finalized { get; set; }

    /// <summary>
    /// Submission field values resolved against the form's own field schema —
    /// each entry carries the friendly column label and a format hint so the
    /// UI can render dates, numbers, etc. correctly. Populated only when the
    /// consumer asks for full details.
    /// </summary>
    public List<SubmissionFieldDto>? Fields { get; set; }
}

/// <summary>One key/value entry inside <see cref="SubmissionEventDto.Fields"/>.</summary>
public class SubmissionFieldDto
{
    /// <summary>Internal element id (e.g. <c>__field_abc123</c>) — preserved so
    /// editors who know the form's schema can disambiguate fields with similar labels.</summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>Human-readable column label as configured on the form element.</summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>Format hint from EPiServer.Forms — "Text", "Date", "Number", "MultiLineText", "FileUpload", etc.</summary>
    public string Format { get; set; } = "Text";

    /// <summary>Stringified submission value, or null/empty when the field was not submitted.</summary>
    public string? Value { get; set; }
}

/// <summary>
/// Lightweight tuple for the form-filter dropdown on the timeline.
/// </summary>
public class FormChoiceDto
{
    public Guid FormGuid { get; set; }
    public int ContentId { get; set; }
    public string Name { get; set; } = string.Empty;
}

/// <summary>
/// Per-form PII analysis result used by the CMS Doctor PII retention check.
/// </summary>
public class FormPiiAnalysisDto
{
    public int ContentId { get; set; }
    public string FormName { get; set; } = string.Empty;
    public string? EditUrl { get; set; }
    public bool UsesDefaultRetention { get; set; }
    public bool StoresSubmissionData { get; set; }
    public List<string> PiiFieldLabels { get; set; } = new();
}
