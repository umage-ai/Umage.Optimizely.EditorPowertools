using EPiServer;
using EPiServer.Core;
using EPiServer.DataAbstraction;
using EPiServer.Forms;
using EPiServer.Forms.Core;
using EPiServer.Forms.Core.Data;
using EPiServer.Forms.Core.Models;
using EPiServer.Forms.Core.Models.Internal;
using EPiServer.Forms.Implementation.Elements;
using EPiServer.Shell;
using Microsoft.Extensions.Logging;
using UmageAI.Optimizely.EditorPowerTools.Forms.Tools.FormsOverview.Models;
using UmageAI.Optimizely.EditorPowerTools.Helpers;
using ItemRange = EPiServer.Shell.Services.Rest.ItemRange;
using SortColumn = EPiServer.Shell.Services.Rest.SortColumn;

namespace UmageAI.Optimizely.EditorPowerTools.Forms.Services;

/// <summary>
/// Aggregates form metadata, submission counts and usage information.
/// Used by the Forms Overview and Submissions Timeline tools.
/// </summary>
public class FormsAggregationService : IFormsAggregationService
{
    private readonly IContentModelUsage _contentModelUsage;
    private readonly IContentTypeRepository _contentTypeRepository;
    private readonly IContentLoader _contentLoader;
    private readonly IContentSoftLinkRepository _softLinkRepository;
    private readonly IFormDataRepository _formDataRepository;
    private readonly IFormRepository _formRepository;
    private readonly ILogger<FormsAggregationService> _logger;

    public FormsAggregationService(
        IContentModelUsage contentModelUsage,
        IContentTypeRepository contentTypeRepository,
        IContentLoader contentLoader,
        IContentSoftLinkRepository softLinkRepository,
        IFormDataRepository formDataRepository,
        IFormRepository formRepository,
        ILogger<FormsAggregationService> logger)
    {
        _contentModelUsage = contentModelUsage;
        _contentTypeRepository = contentTypeRepository;
        _contentLoader = contentLoader;
        _softLinkRepository = softLinkRepository;
        _formDataRepository = formDataRepository;
        _formRepository = formRepository;
        _logger = logger;
    }

    /// <summary>
    /// Returns one row per form (master language version) with stats and usage.
    /// </summary>
    public IReadOnlyList<FormSummaryDto> GetForms()
    {
        var formContainerType = _contentTypeRepository.Load(typeof(FormContainerBlock));
        if (formContainerType == null)
            return Array.Empty<FormSummaryDto>();

        // ListContentOfContentType enumerates all instances (including drafts) — we
        // dedupe per ContentReference master version below.
        var usages = _contentModelUsage
            .ListContentOfContentType(formContainerType)
            .GroupBy(u => u.ContentLink.ToReferenceWithoutVersion().ID)
            .Select(g => g.First())
            .ToList();

        var results = new List<FormSummaryDto>(usages.Count);

        foreach (var usage in usages)
        {
            try
            {
                var contentRef = usage.ContentLink.ToReferenceWithoutVersion();
                if (!_contentLoader.TryGet<FormContainerBlock>(contentRef, out var formBlock))
                    continue;

                var formContent = formBlock as IContent;
                if (formContent == null)
                    continue;

                var formGuid = formContent.ContentGuid;
                var lang = (formContent as ILocalizable)?.Language?.Name
                    ?? usage.LanguageBranch
                    ?? string.Empty;

                var formIden = new FormIdentity(formGuid, lang);

                // Single pass over the form's elements: total field count, duplicate
                // input-field labels (ambiguous submission columns), and PII-shaped
                // fields. Uses `Items` (not `FilteredItems` — obsolete in CMS 13) so the
                // total is independent of the current user's access rights.
                var scan = ScanElements(formBlock);

                // Submission count + last submission. Use a wide date window so we
                // catch everything; finalizedOnly:false counts partials too.
                var submissionCount = SafeCount(formIden);
                var lastSubmission = SafeLastSubmissionUtc(formIden);

                // Usage: incoming soft links from content that references the form block.
                var (usageCount, usageList) = LoadUsage(contentRef);

                // Notification handlers: email-template + webhook post-submission actors.
                var (emailCount, webhookCount) = DetectNotificationHandlers(formContent);

                var partial = formBlock.PartialSubmissionRetentionPeriod;
                var final = formBlock.FinalizedSubmissionRetentionPeriod;
                var usesDefault = string.IsNullOrEmpty(partial)
                    || string.IsNullOrEmpty(final)
                    || string.Equals(partial, "EPiServer.RetentionPolicy.Default", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(final, "EPiServer.RetentionPolicy.Default", StringComparison.OrdinalIgnoreCase);

                results.Add(new FormSummaryDto
                {
                    ContentId = contentRef.ID,
                    FormGuid = formGuid,
                    Name = formContent.Name ?? string.Empty,
                    Language = lang,
                    Breadcrumb = contentRef.GetBreadcrumb(),
                    EditUrl = BuildEditUrl(contentRef.ID, lang),
                    FieldCount = scan.FieldCount,
                    SubmissionCount = submissionCount,
                    LastSubmissionUtc = lastSubmission,
                    IsPublished = IsPublished(formContent),
                    StoresSubmissionData = formBlock.AllowToStoreSubmissionData,
                    DuplicateFieldLabels = scan.DuplicateLabels,
                    PiiFieldLabels = scan.PiiLabels,
                    UsageCount = usageCount,
                    Usage = usageList,
                    PartialRetentionPolicy = partial,
                    FinalizedRetentionPolicy = final,
                    UsesDefaultRetention = usesDefault,
                    HasEmailHandler = emailCount > 0,
                    EmailHandlerCount = emailCount,
                    HasWebhookHandler = webhookCount > 0,
                    WebhookHandlerCount = webhookCount
                });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Skipping form usage {ContentLink}", usage.ContentLink);
            }
        }

        return results
            .OrderByDescending(r => r.LastSubmissionUtc ?? DateTime.MinValue)
            .ThenBy(r => r.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>
    /// Returns the most recent submissions across all forms, newest first.
    /// When <paramref name="formGuid"/> is non-empty, restricts to a single form.
    /// When <paramref name="includeData"/> is true, populates the friendly-named
    /// field list on each event (heavier — opt in only). Friendly-name info
    /// is fetched once per form and cached for the duration of the call.
    /// </summary>
    public IReadOnlyList<SubmissionEventDto> GetSubmissionsTimeline(int top, int days, Guid? formGuid = null, bool includeData = false)
    {
        if (top <= 0) top = 100;
        if (top > 1000) top = 1000;
        if (days <= 0) days = 30;

        var endDate = DateTime.UtcNow.AddDays(1);
        var beginDate = DateTime.UtcNow.AddDays(-days);

        var formContainerType = _contentTypeRepository.Load(typeof(FormContainerBlock));
        if (formContainerType == null)
            return Array.Empty<SubmissionEventDto>();

        var seen = new HashSet<Guid>();
        var events = new List<SubmissionEventDto>();
        // Friendly-name info is per-form-and-language; computing it walks the
        // form's elements via FormBusinessService, so cache it for the call.
        var fieldSchemaCache = new Dictionary<(Guid, string), List<FriendlyNameInfo>>();

        foreach (var usage in _contentModelUsage.ListContentOfContentType(formContainerType))
        {
            try
            {
                var contentRef = usage.ContentLink.ToReferenceWithoutVersion();
                if (!_contentLoader.TryGet<FormContainerBlock>(contentRef, out var formBlock))
                    continue;

                var formContent = formBlock as IContent;
                if (formContent == null) continue;

                if (formGuid.HasValue && formGuid.Value != Guid.Empty &&
                    formContent.ContentGuid != formGuid.Value)
                    continue;

                var lang = (formContent as ILocalizable)?.Language?.Name
                    ?? usage.LanguageBranch
                    ?? string.Empty;

                if (!seen.Add(formContent.ContentGuid)) continue;

                var formIden = new FormIdentity(formContent.ContentGuid, lang);
                var sortColumns = new[] { new SortColumn { ColumnName = Constants.SYSTEMCOLUMN_SubmitTime, SortDescending = true } };
                var requestRange = new ItemRange { Start = 0, End = top - 1 };
                ItemRange actualRange;

                var submissions = _formDataRepository.GetSubmissionData(
                    formIden, beginDate, endDate, sortColumns, requestRange, out actualRange, finalizedOnly: false);

                if (submissions == null) continue;

                List<FriendlyNameInfo>? schema = null;
                if (includeData)
                {
                    var key = (formContent.ContentGuid, lang);
                    if (!fieldSchemaCache.TryGetValue(key, out schema))
                    {
                        schema = LoadFieldSchema(formIden);
                        fieldSchemaCache[key] = schema;
                    }
                }

                foreach (var s in submissions)
                {
                    var ev = MapSubmission(s, formContent.ContentGuid, contentRef.ID, formContent.Name ?? string.Empty, lang, includeData, schema);
                    if (ev != null) events.Add(ev);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Skipping submissions for form usage {ContentLink}", usage.ContentLink);
            }
        }

        return events
            .OrderByDescending(e => e.SubmittedUtc)
            .Take(top)
            .ToList();
    }

    private List<FriendlyNameInfo> LoadFieldSchema(FormIdentity formIden)
    {
        try
        {
            // GetDataFriendlyNameInfos returns only the user-facing input
            // elements (no display-only paragraphs etc.) which is what we
            // want for column headers in a submission detail view.
            return _formRepository.GetDataFriendlyNameInfos(formIden)?.ToList()
                ?? new List<FriendlyNameInfo>();
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Loading friendly-name info failed for {Guid}", formIden.Guid);
            return new List<FriendlyNameInfo>();
        }
    }

    /// <summary>
    /// Walks every FormContainerBlock and returns a per-form PII analysis used
    /// by the Forms CMS Doctor checks. Heuristic: a field is "PII-like" if its
    /// content type name or its element label matches a curated keyword list.
    /// Combined with retention info so callers can quickly find privacy risks.
    /// </summary>
    public IReadOnlyList<FormPiiAnalysisDto> AnalyzePii()
    {
        var formContainerType = _contentTypeRepository.Load(typeof(FormContainerBlock));
        if (formContainerType == null) return Array.Empty<FormPiiAnalysisDto>();

        var seen = new HashSet<int>();
        var results = new List<FormPiiAnalysisDto>();

        foreach (var usage in _contentModelUsage.ListContentOfContentType(formContainerType))
        {
            try
            {
                var contentRef = usage.ContentLink.ToReferenceWithoutVersion();
                if (!seen.Add(contentRef.ID)) continue;
                if (!_contentLoader.TryGet<FormContainerBlock>(contentRef, out var formBlock)) continue;
                var formContent = formBlock as IContent;
                if (formContent == null) continue;

                var partial = formBlock.PartialSubmissionRetentionPeriod;
                var final = formBlock.FinalizedSubmissionRetentionPeriod;
                var usesDefault = string.IsNullOrEmpty(partial)
                    || string.IsNullOrEmpty(final)
                    || string.Equals(partial, "EPiServer.RetentionPolicy.Default", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(final, "EPiServer.RetentionPolicy.Default", StringComparison.OrdinalIgnoreCase);

                var pii = ScanElements(formBlock).PiiLabels;

                if (pii.Count > 0)
                {
                    results.Add(new FormPiiAnalysisDto
                    {
                        ContentId = contentRef.ID,
                        FormName = formContent.Name ?? string.Empty,
                        EditUrl = BuildEditUrl(contentRef.ID, (formContent as ILocalizable)?.Language?.Name),
                        UsesDefaultRetention = usesDefault,
                        StoresSubmissionData = formBlock.AllowToStoreSubmissionData,
                        PiiFieldLabels = pii
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Skipping PII analysis for {ContentLink}", usage.ContentLink);
            }
        }

        return results;
    }

    /// <summary>
    /// Element type names that are structural / display / navigation rather than
    /// data inputs. Excluded from duplicate-label detection so two paragraphs or
    /// step headers sharing a caption don't read as "duplicate fields".
    /// </summary>
    private static readonly HashSet<string> _nonInputElementTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "ParagraphTextElementBlock", "RichTextElementBlock", "ImageElementBlock",
        "FormStepBlock", "SubmitButtonElementBlock", "ResetButtonElementBlock",
        "NextButtonElementBlock", "PreviousButtonElementBlock"
    };

    /// <summary>
    /// Single pass over a form's elements. Returns the total field count, the set
    /// of duplicate input-field labels (ambiguous submission columns), and the
    /// PII-shaped field labels. Loading each element once keeps GetForms and
    /// AnalyzePii consistent and avoids walking the ElementsArea twice.
    /// </summary>
    private (int FieldCount, List<string> DuplicateLabels, List<string> PiiLabels) ScanElements(FormContainerBlock formBlock)
    {
        var fieldCount = 0;
        var piiLabels = new List<string>();
        var labelCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var labelOrder = new List<string>();

        if (formBlock.ElementsArea?.Items != null)
        {
            foreach (var item in formBlock.ElementsArea.Items)
            {
                fieldCount++;
                if (item?.ContentLink == null) continue;
                if (!_contentLoader.TryGet<IContent>(item.ContentLink.ToReferenceWithoutVersion(), out var element))
                    continue;

                var elementType = _contentTypeRepository.Load(element.ContentTypeID);
                var typeName = elementType?.Name ?? element.GetType().Name;
                var label = element.Name ?? string.Empty;

                // Duplicate detection: only real input fields, only non-empty labels.
                if (!string.IsNullOrWhiteSpace(label) && !_nonInputElementTypes.Contains(typeName))
                {
                    if (!labelCounts.ContainsKey(label)) labelOrder.Add(label);
                    labelCounts[label] = labelCounts.TryGetValue(label, out var n) ? n + 1 : 1;
                }

                if (FormsPiiHeuristics.LooksLikePii(typeName, label, out var hint))
                    piiLabels.Add(string.IsNullOrEmpty(label) ? hint : $"{label} ({hint})");
            }
        }

        var duplicateLabels = labelOrder.Where(l => labelCounts[l] > 1).ToList();
        return (fieldCount, duplicateLabels, piiLabels);
    }

    /// <summary>True when the content has a published version (best-effort).</summary>
    private static bool IsPublished(IContent content)
        => content is not IVersionable versionable || versionable.Status == VersionStatus.Published;

    /// <summary>
    /// Returns a lightweight list of forms suitable for a filter dropdown on
    /// the timeline page (one entry per unique form GUID, master version).
    /// </summary>
    public IReadOnlyList<FormChoiceDto> GetFormChoices()
    {
        var formContainerType = _contentTypeRepository.Load(typeof(FormContainerBlock));
        if (formContainerType == null) return Array.Empty<FormChoiceDto>();

        var seen = new HashSet<Guid>();
        var results = new List<FormChoiceDto>();

        foreach (var usage in _contentModelUsage.ListContentOfContentType(formContainerType))
        {
            try
            {
                var contentRef = usage.ContentLink.ToReferenceWithoutVersion();
                if (!_contentLoader.TryGet<IContent>(contentRef, out var content)) continue;
                if (!seen.Add(content.ContentGuid)) continue;
                results.Add(new FormChoiceDto
                {
                    FormGuid = content.ContentGuid,
                    ContentId = contentRef.ID,
                    Name = content.Name ?? string.Empty
                });
            }
            catch { }
        }

        return results.OrderBy(r => r.Name, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private int SafeCount(FormIdentity formIden)
    {
        try
        {
            return _formDataRepository.GetSubmissionDataCount(
                formIden, DateTime.MinValue.ToUniversalTime(), DateTime.MaxValue.ToUniversalTime(), finalizedOnly: false);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "GetSubmissionDataCount failed for form {Guid}", formIden.Guid);
            return 0;
        }
    }

    private DateTime? SafeLastSubmissionUtc(FormIdentity formIden)
    {
        try
        {
            var sortColumns = new[] { new SortColumn { ColumnName = Constants.SYSTEMCOLUMN_SubmitTime, SortDescending = true } };
            var requestRange = new ItemRange { Start = 0, End = 0 };
            ItemRange actualRange;
            var data = _formDataRepository.GetSubmissionData(
                formIden,
                DateTime.MinValue.ToUniversalTime(),
                DateTime.MaxValue.ToUniversalTime(),
                sortColumns,
                requestRange,
                out actualRange,
                finalizedOnly: false);
            var first = data?.FirstOrDefault();
            if (first == null) return null;
            return ExtractSubmitTime(first);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Last submission lookup failed for form {Guid}", formIden.Guid);
            return null;
        }
    }

    private (int Count, List<FormUsageDto> Items) LoadUsage(ContentReference contentRef)
    {
        var items = new List<FormUsageDto>();
        try
        {
            var softLinks = _softLinkRepository.Load(contentRef, true);
            if (softLinks == null) return (0, items);

            var grouped = softLinks
                .Where(sl => !sl.OwnerContentLink.CompareToIgnoreWorkID(contentRef))
                .GroupBy(sl => sl.OwnerContentLink.ToReferenceWithoutVersion().ID);

            foreach (var g in grouped)
            {
                var ownerLink = g.First().OwnerContentLink.ToReferenceWithoutVersion();
                var ownerName = "[Unknown]";
                string? ownerType = null;
                string? ownerLang = g.First().OwnerLanguage?.TwoLetterISOLanguageName;

                if (_contentLoader.TryGet<IContent>(ownerLink, out var owner))
                {
                    ownerName = owner.Name;
                    var ct = _contentTypeRepository.Load(owner.ContentTypeID);
                    ownerType = ct?.DisplayName ?? ct?.Name;
                }

                items.Add(new FormUsageDto
                {
                    OwnerContentId = ownerLink.ID,
                    OwnerName = ownerName,
                    OwnerTypeName = ownerType,
                    Language = ownerLang,
                    EditUrl = BuildEditUrl(ownerLink.ID, ownerLang)
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Soft link load failed for {ContentRef}", contentRef);
        }

        return (items.Count, items);
    }

    private SubmissionEventDto? MapSubmission(Submission s, Guid formGuid, int formContentId, string formName, string formLang, bool includeData = false, List<FriendlyNameInfo>? schema = null)
    {
        try
        {
            var submitted = ExtractSubmitTime(s);
            if (submitted == null) return null;

            string? user = null;
            if (s.Data != null && s.Data.TryGetValue(Constants.SYSTEMCOLUMN_SubmitUser, out var u))
                user = u?.ToString();

            string? hosted = null;
            if (s.Data != null && s.Data.TryGetValue(Constants.SYSTEMCOLUMN_HostedPage, out var h))
                hosted = h?.ToString();

            string? lang = formLang;
            if (s.Data != null && s.Data.TryGetValue(Constants.SYSTEMCOLUMN_Language, out var l))
                lang = l?.ToString() ?? lang;

            var finalized = false;
            if (s.Data != null && s.Data.TryGetValue(Constants.SYSTEMCOLUMN_FinalizedSubmission, out var f))
                bool.TryParse(f?.ToString(), out finalized);

            List<SubmissionFieldDto>? fields = null;
            if (includeData && s.Data != null)
            {
                fields = BuildFieldList(s.Data, schema);
            }

            return new SubmissionEventDto
            {
                SubmissionId = s.Id ?? string.Empty,
                FormGuid = formGuid,
                FormContentId = formContentId,
                FormName = formName,
                FormEditUrl = BuildEditUrl(formContentId, formLang),
                SubmissionViewUrl = BuildSubmissionViewUrl(formContentId, formLang),
                SubmittedUtc = submitted.Value,
                SubmittedBy = user,
                HostedPageUrl = hosted,
                Language = lang,
                Finalized = finalized,
                Fields = fields
            };
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to map submission {Id}", s?.Id);
            return null;
        }
    }

    /// <summary>
    /// Produces an ordered, friendly-named list of submission fields. Order
    /// matches the form's own field order from <see cref="FriendlyNameInfo"/>;
    /// any data keys not in the schema (e.g. dynamically added fields, or
    /// data captured before a field was renamed) are appended at the end.
    /// SYSTEMCOLUMN_* keys are stripped — those go in the metadata header.
    /// </summary>
    private static List<SubmissionFieldDto> BuildFieldList(IDictionary<string, object> data, List<FriendlyNameInfo>? schema)
    {
        var result = new List<SubmissionFieldDto>(data.Count);
        var consumed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (schema != null)
        {
            foreach (var info in schema)
            {
                if (string.IsNullOrEmpty(info?.ElementId)) continue;
                data.TryGetValue(info.ElementId, out var value);
                consumed.Add(info.ElementId);
                result.Add(new SubmissionFieldDto
                {
                    Key = info.ElementId,
                    Label = !string.IsNullOrWhiteSpace(info.Label) ? info.Label
                          : !string.IsNullOrWhiteSpace(info.FriendlyName) ? info.FriendlyName
                          : StripFieldPrefix(info.ElementId),
                    Format = info.FormatType.ToString(),
                    Value = value?.ToString()
                });
            }
        }

        // Append leftovers (keys not described by schema) so editors can still
        // see legacy or out-of-band data captured by the submission.
        foreach (var kv in data)
        {
            if (kv.Key == null) continue;
            if (kv.Key.StartsWith(Constants.SYSTEMCOLUMN_PREFIX, StringComparison.Ordinal)) continue;
            if (consumed.Contains(kv.Key)) continue;
            result.Add(new SubmissionFieldDto
            {
                Key = kv.Key,
                Label = StripFieldPrefix(kv.Key),
                Format = "Text",
                Value = kv.Value?.ToString()
            });
        }

        return result;
    }

    private static string StripFieldPrefix(string key)
        => key.StartsWith(Constants.ElementIdPrefix, StringComparison.Ordinal)
            ? key.Substring(Constants.ElementIdPrefix.Length)
            : key;

    /// <summary>
    /// Builds a URL that opens the form block in CMS edit mode and selects the
    /// "Form Submissions" view tab. The Forms.UI module renders a submission
    /// data view there, which is what an editor expects when they say "go to
    /// submissions". Falls back to the regular edit URL if Forms.UI isn't
    /// installed.
    /// </summary>
    private static string BuildSubmissionViewUrl(int contentId, string? lang)
    {
        // The Forms.UI submissions tab uses the same edit-mode URL with a
        // viewname=formsdataview hash fragment. Editors land on the form
        // block and Forms.UI swaps in its data view.
        var langSegment = string.IsNullOrEmpty(lang) ? string.Empty : $"&viewsetting=viewlanguage:///{lang}";
        return $"{Paths.ToResource("CMS", "")}#context=epi.cms.contentdata:///{contentId}{langSegment}&viewsetting=viewlanguage:///{lang}&viewsetting=viewname:///formsdataview";
    }

    /// <summary>
    /// Counts configured email-template and webhook post-submission actors on
    /// the form by scanning its property collection for actor-model lists.
    /// Detection is type-name-based to avoid binding to internal Forms.UI types
    /// (which are otherwise reachable but private/internal).
    /// </summary>
    private static (int Email, int Webhook) DetectNotificationHandlers(IContent content)
    {
        if (content is not IContentData data) return (0, 0);

        var email = 0;
        var webhook = 0;

        foreach (var prop in data.Property)
        {
            if (prop?.Value is not System.Collections.IEnumerable enumerable) continue;
            // Don't iterate strings as character arrays.
            if (prop.Value is string) continue;

            foreach (var item in enumerable)
            {
                if (item == null) continue;
                var name = item.GetType().Name;
                if (name == "EmailTemplateActorModel") email++;
                else if (name == "WebhookActorModel") webhook++;
            }
        }

        return (email, webhook);
    }

    private static DateTime? ExtractSubmitTime(Submission s)
    {
        if (s.Data == null) return null;
        if (!s.Data.TryGetValue(Constants.SYSTEMCOLUMN_SubmitTime, out var raw) || raw == null)
            return null;

        if (raw is DateTime dt) return dt.ToUniversalTime();
        if (DateTime.TryParse(raw.ToString(), out var parsed)) return parsed.ToUniversalTime();
        return null;
    }

    private static string BuildEditUrl(int contentId, string? lang)
    {
        var langSegment = string.IsNullOrEmpty(lang) ? string.Empty : $"&viewsetting=viewlanguage:///{lang}";
        return $"{Paths.ToResource("CMS", "")}#context=epi.cms.contentdata:///{contentId}{langSegment}";
    }
}
