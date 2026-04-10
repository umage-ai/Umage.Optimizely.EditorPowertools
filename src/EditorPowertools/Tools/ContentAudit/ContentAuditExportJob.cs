using System.Text.Json;
using EditorPowertools.Configuration;
using EditorPowertools.Tools.ContentAudit.Models;
using EPiServer;
using EPiServer.Core;
using EPiServer.Data.Dynamic;
using EPiServer.DataAccess;
using EPiServer.Framework.Blobs;
using EPiServer.PlugIn;
using EPiServer.Scheduler;
using EPiServer.Security;
using EPiServer.Web;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EditorPowertools.Tools.ContentAudit;

[ScheduledPlugIn(
    DisplayName    = "[EditorPowertools] Content Audit Export",
    Description    = "Generates a Content Audit export file and saves it to the CMS media library.",
    LanguagePath   = "/editorpowertools/jobs/contentauditexport",
    SortIndex      = 10001)]
public class ContentAuditExportJob : ScheduledJobBase
{
    private readonly IContentAuditDataProvider _provider;
    private readonly ContentAuditExportRenderer _renderer;
    private readonly IContentRepository _contentRepository;
    private readonly IBlobFactory _blobFactory;
    private readonly DynamicDataStoreFactory _storeFactory;
    private readonly EditorPowertoolsOptions _options;
    private readonly ILogger<ContentAuditExportJob> _logger;
    private bool _stopSignaled;

    public ContentAuditExportJob(
        IContentAuditDataProvider provider,
        ContentAuditExportRenderer renderer,
        IContentRepository contentRepository,
        IBlobFactory blobFactory,
        DynamicDataStoreFactory storeFactory,
        IOptions<EditorPowertoolsOptions> options,
        ILogger<ContentAuditExportJob> logger)
    {
        _provider        = provider;
        _renderer        = renderer;
        _contentRepository = contentRepository;
        _blobFactory     = blobFactory;
        _storeFactory    = storeFactory;
        _options         = options.Value;
        _logger          = logger;
        IsStoppable      = true;
    }

    public override void Stop() { _stopSignaled = true; base.Stop(); }

    public override string Execute()
    {
        _stopSignaled = false;
        var store = GetStore();

        // Clean up old records first (>7 days)
        var cutoff = DateTime.UtcNow.AddDays(-7);
        var old = store.Items<ContentAuditExportJobRequest>()
            .Where(r => r.CompletedAt.HasValue && r.CompletedAt < cutoff)
            .ToList();
        foreach (var o in old) store.Delete(o.Id);

        // Get all pending requests
        var pending = store.Items<ContentAuditExportJobRequest>()
            .Where(r => r.Status == "Pending")
            .OrderBy(r => r.RequestedAt)
            .ToList();

        if (pending.Count == 0)
            return "No pending export requests.";

        var folderRef = EnsureReportFolder();
        int processed = 0;

        foreach (var jobRequest in pending)
        {
            if (_stopSignaled) break;

            jobRequest.Status = "Running";
            store.Save(jobRequest);

            try
            {
                ProcessRequest(jobRequest, folderRef, store);
                processed++;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Content audit export failed for request {RequestId}", jobRequest.RequestId);
                jobRequest.Status       = "Failed";
                jobRequest.ErrorMessage = "Export failed. Check server logs for details.";
                jobRequest.CompletedAt  = DateTime.UtcNow;
                store.Save(jobRequest);
            }
        }

        return $"Processed {processed} export request(s).";
    }

    private void ProcessRequest(ContentAuditExportJobRequest jobRequest, ContentReference folderRef, DynamicDataStore store)
    {
        var exportRequest = BuildExportRequest(jobRequest);
        var ct = _stopSignaled ? new CancellationToken(true) : CancellationToken.None;
        var rows = _provider.GetAllRows(exportRequest, ct);

        string ext    = _renderer.GetExtension(jobRequest.Format);
        string mime   = _renderer.GetContentType(jobRequest.Format);
        string name   = $"content-audit-{DateTime.UtcNow:yyyyMMdd-HHmmss}{ext}";

        // Create the CMS media item
        var media = _contentRepository.GetDefault<ContentAuditReportMedia>(folderRef);
        media.Name = name;

        // Write blob
        var blob = _blobFactory.CreateBlob(media.BinaryDataContainer, ext);
        using (var stream = blob.OpenWrite())
        {
            byte[] bytes = jobRequest.Format.ToLowerInvariant() switch
            {
                "xlsx" => _renderer.RenderXlsx(rows, exportRequest.Columns ?? GetAllColumns()),
                "csv"  => _renderer.RenderCsv(rows,  exportRequest.Columns ?? GetAllColumns()),
                "json" => _renderer.RenderJson(rows),
                _      => _renderer.RenderXlsx(rows, exportRequest.Columns ?? GetAllColumns())
            };
            stream.Write(bytes, 0, bytes.Length);
        }

        media.BinaryData = blob;
        var savedRef = _contentRepository.Save(media, SaveAction.Publish, AccessLevel.NoAccess);

        jobRequest.Status          = "Completed";
        jobRequest.ResultContentId = savedRef.ID.ToString();
        jobRequest.CompletedAt     = DateTime.UtcNow;
        store.Save(jobRequest);
    }

    private ContentAuditExportRequest BuildExportRequest(ContentAuditExportJobRequest jobRequest)
    {
        List<ContentAuditFilter>? filters = null;
        if (!string.IsNullOrEmpty(jobRequest.FiltersJson))
        {
            try { filters = JsonSerializer.Deserialize<List<ContentAuditFilter>>(jobRequest.FiltersJson); }
            catch { /* ignore malformed filters */ }
        }

        List<string>? columns = string.IsNullOrEmpty(jobRequest.Columns)
            ? null
            : jobRequest.Columns.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();

        return new ContentAuditExportRequest
        {
            Format         = jobRequest.Format,
            Columns        = columns,
            MainTypeFilter = jobRequest.MainTypeFilter,
            QuickFilter    = jobRequest.QuickFilter,
            Search         = jobRequest.Search,
            Filters        = filters
        };
    }

    private ContentReference EnsureReportFolder()
    {
        var folderName = _options.ContentAudit.ReportFolderName;
        var globalAssets = SiteDefinition.Current.GlobalAssetsRoot;

        var existing = _contentRepository
            .GetChildren<ContentFolder>(globalAssets)
            .FirstOrDefault(f => string.Equals(f.Name, folderName, StringComparison.OrdinalIgnoreCase));

        if (existing != null)
            return existing.ContentLink;

        var newFolder = _contentRepository.GetDefault<ContentFolder>(globalAssets);
        newFolder.Name = folderName;
        return _contentRepository.Save(newFolder, SaveAction.Publish, AccessLevel.NoAccess);
    }

    private DynamicDataStore GetStore() =>
        _storeFactory.GetStore(typeof(ContentAuditExportJobRequest))
        ?? _storeFactory.CreateStore(typeof(ContentAuditExportJobRequest));

    private static List<string> GetAllColumns() =>
    [
        "contentId", "name", "language", "contentType", "mainType",
        "url", "editUrl", "breadcrumb", "status",
        "createdBy", "created", "changedBy", "changed",
        "published", "publishedUntil",
        "masterLanguage", "allLanguages",
        "referenceCount", "versionCount", "hasPersonalizations"
    ];
}
