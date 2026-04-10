using UmageAI.Optimizely.EditorPowerTools.Tools.ContentAudit.Models;
using Microsoft.Extensions.Logging;

namespace UmageAI.Optimizely.EditorPowerTools.Tools.ContentAudit;

public class ContentAuditService
{
    private readonly IContentAuditDataProvider _provider;
    private readonly ILogger<ContentAuditService> _logger;

    public ContentAuditService(IContentAuditDataProvider provider, ILogger<ContentAuditService> logger)
    {
        _provider = provider;
        _logger = logger;
    }

    public ContentAuditResponse GetContent(ContentAuditRequest request, CancellationToken ct = default)
    {
        var result = _provider.GetPage(request, ct);

        int? totalPages = result.TotalCount.HasValue
            ? (int)Math.Ceiling((double)result.TotalCount.Value / request.PageSize)
            : null;

        return new ContentAuditResponse
        {
            Items      = result.Items,
            TotalCount = result.TotalCount,
            Page       = request.Page,
            PageSize   = request.PageSize,
            TotalPages = totalPages
        };
    }

    public IEnumerable<ContentAuditRow> GetAllMatchingRows(ContentAuditExportRequest request, CancellationToken ct = default)
        => _provider.GetAllRows(request, ct);
}
