using UmageAI.Optimizely.EditorPowerTools.Tools.ContentAudit.Models;

namespace UmageAI.Optimizely.EditorPowerTools.Tools.ContentAudit;

/// <summary>
/// Pluggable content data source for the Content Audit tool.
/// Register a custom implementation via DI to replace the default GetDescendents-based provider.
/// Search-backed providers (e.g. Optimizely Find) can implement full server-side
/// filtering, sorting, and accurate TotalCount.
/// </summary>
public interface IContentAuditDataProvider
{
    /// <summary>
    /// Returns one page of matching rows.
    /// <para><see cref="ContentAuditPageResult.TotalCount"/> is <c>null</c> when the provider
    /// cannot determine the total without a full tree scan (default provider).</para>
    /// </summary>
    ContentAuditPageResult GetPage(ContentAuditRequest request, CancellationToken ct = default);

    /// <summary>
    /// Streams all matching rows for the export job.
    /// Implementations should yield items one-by-one to avoid loading everything into memory.
    /// </summary>
    IEnumerable<ContentAuditRow> GetAllRows(ContentAuditExportRequest request, CancellationToken ct = default);
}
