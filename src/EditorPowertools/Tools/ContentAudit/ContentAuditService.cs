using UmageAI.Optimizely.EditorPowerTools.Abstractions;
using UmageAI.Optimizely.EditorPowerTools.Tools.ContentAudit.Models;
using EPiServer;
using EPiServer.Core;
using EPiServer.DataAbstraction;
using Microsoft.Extensions.Logging;

namespace UmageAI.Optimizely.EditorPowerTools.Tools.ContentAudit;

public class ContentAuditService
{
    private readonly IContentAuditDataProvider _provider;
    private readonly IContentRepository _contentRepository;
    private readonly IContentTypeRepository _contentTypeRepository;
    private readonly IContentTypeMetadataProvider _metadataProvider;
    private readonly ILogger<ContentAuditService> _logger;

    public ContentAuditService(
        IContentAuditDataProvider provider,
        IContentRepository contentRepository,
        IContentTypeRepository contentTypeRepository,
        IContentTypeMetadataProvider metadataProvider,
        ILogger<ContentAuditService> logger)
    {
        _provider = provider;
        _contentRepository = contentRepository;
        _contentTypeRepository = contentTypeRepository;
        _metadataProvider = metadataProvider;
        _logger = logger;
    }

    public ContentAuditResponse GetContent(ContentAuditRequest request, CancellationToken ct = default)
    {
        var result = _provider.GetPage(request, ct);

        // Post-page CMS 13 filter (no-op on CMS 12 via CmsFeatureFlags short-circuit).
        var filteredItems = result.Items
            .Where(r => ShouldIncludeRow(r, request.ContractFilter, request.CompositionFilter))
            .ToList();

        // When filters change row count, we can't trust the provider's TotalCount; keep it null so UI shows "?".
        int? totalCount = result.TotalCount;
        if (filteredItems.Count != result.Items.Count)
            totalCount = null;

        int? totalPages = totalCount.HasValue
            ? (int)Math.Ceiling((double)totalCount.Value / request.PageSize)
            : null;

        return new ContentAuditResponse
        {
            Items      = filteredItems,
            TotalCount = totalCount,
            Page       = request.Page,
            PageSize   = request.PageSize,
            TotalPages = totalPages
        };
    }

    public IEnumerable<ContentAuditRow> GetAllMatchingRows(ContentAuditExportRequest request, CancellationToken ct = default)
    {
        foreach (var row in _provider.GetAllRows(request, ct))
        {
            if (ShouldIncludeRow(row, request.ContractFilter, request.CompositionFilter))
                yield return row;
        }
    }

    // ---- Filter helpers ----

    /// <summary>
    /// Applies CMS 13 ContractFilter / CompositionFilter to an audit row.
    /// On CMS 12 (CmsFeatureFlags.ContractsAvailable == false) this short-circuits and returns true,
    /// so all rows pass through unchanged.
    /// </summary>
    private bool ShouldIncludeRow(ContentAuditRow row, string? contractFilter, string? compositionFilter)
    {
#pragma warning disable CS0162 // Unreachable code: one branch is always dead depending on TFM (CMS 12 vs CMS 13).
        if (!CmsFeatureFlags.ContractsAvailable) return true;
        if (string.IsNullOrEmpty(contractFilter) && string.IsNullOrEmpty(compositionFilter)) return true;

        var contentType = GetContentTypeForRow(row);
        if (contentType == null) return true;  // can't determine — don't drop

        var m = _metadataProvider.Get(contentType);

        if (contractFilter == "only"    && !m.IsContract) return false;
        if (contractFilter == "exclude" &&  m.IsContract) return false;

        if (!string.IsNullOrEmpty(compositionFilter) && contentType.Base.ToString() == "Block")
        {
            if (compositionFilter == "section" && !m.CompositionBehaviors.Contains("SectionEnabled")) return false;
            if (compositionFilter == "element" && !m.CompositionBehaviors.Contains("ElementEnabled")) return false;
        }
        return true;
#pragma warning restore CS0162
    }

    private ContentType? GetContentTypeForRow(ContentAuditRow row)
    {
        try
        {
            var contentLink = new ContentReference(row.ContentId);
            if (ContentReference.IsNullOrEmpty(contentLink)) return null;
            var content = _contentRepository.Get<IContent>(contentLink);
            return _contentTypeRepository.Load(content.ContentTypeID);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Could not resolve content type for audit row {ContentId}", row.ContentId);
            return null;
        }
    }
}
