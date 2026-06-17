using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Security.Principal;
using EPiServer;
using EPiServer.Core;
using EPiServer.Core.Internal;
using EPiServer.Web;
using EPiServer.DataAbstraction;
using EPiServer.DataAccess;
using EPiServer.Framework.Blobs;
using EPiServer.Security;
using UmageAI.Optimizely.EditorPowerTools.PropertyTypes;
using UmageAI.Optimizely.EditorPowerTools.Tools.ContentImporter.Models;
using UmageAI.Optimizely.EditorPowerTools.Tools.ContentImporter.Parsers;
using Microsoft.Extensions.Logging;

namespace UmageAI.Optimizely.EditorPowerTools.Tools.ContentImporter;

public class ContentImporterService
{
    private readonly ImportSessionStore _sessionStore;
    private readonly IEnumerable<IFileParser> _parsers;
    private readonly IContentTypeRepository _contentTypeRepository;
    private readonly IContentRepository _contentRepository;
    private readonly ILanguageBranchRepository _languageBranchRepository;
    private readonly ContentAssetHelper _contentAssetHelper;
    private readonly IBlobFactory _blobFactory;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IPrincipalAccessor _principalAccessor;
    private readonly PropertyTypeHandlerRegistry _handlers;
    private readonly ILogger<ContentImporterService> _logger;

    // System properties to exclude from mapping
    private static readonly HashSet<string> SystemPropertyNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "PageName", "PageLink", "PageParentLink", "PageGuid",
        "PageTypeID", "PageTypeName", "PageCreated", "PageChanged",
        "PageSaved", "PageLanguageBranch", "PageMasterLanguageBranch",
        "PageWorkStatus", "PageDeleted",
        "PageDeletedBy", "PageDeletedDate", "PageShortcutType",
        "PageShortcutLink", "PageTargetFrame",
        "PageExternalURL", "PagePendingPublish", "PageChangedOnPublish",
        "PageCategory", "PageArchiveLink", "PageFolderID",
        "PagePeerOrder", "PageChildOrderRule",
        "icontent_providerdefinitionid"
    };

    // Built-in properties that are useful for import and should be shown
    private static readonly HashSet<string> ImportableBuiltInProperties = new(StringComparer.OrdinalIgnoreCase)
    {
        "PageURLSegment", "PageStartPublish", "PageStopPublish",
        "PageCreatedBy", "PageChangedBy", "PageVisibleInMenu"
    };

    public ContentImporterService(
        ImportSessionStore sessionStore,
        IEnumerable<IFileParser> parsers,
        IContentTypeRepository contentTypeRepository,
        IContentRepository contentRepository,
        ILanguageBranchRepository languageBranchRepository,
        ContentAssetHelper contentAssetHelper,
        IBlobFactory blobFactory,
        IHttpClientFactory httpClientFactory,
        IPrincipalAccessor principalAccessor,
        PropertyTypeHandlerRegistry handlers,
        ILogger<ContentImporterService> logger)
    {
        _sessionStore = sessionStore;
        _parsers = parsers;
        _contentTypeRepository = contentTypeRepository;
        _contentRepository = contentRepository;
        _languageBranchRepository = languageBranchRepository;
        _contentAssetHelper = contentAssetHelper;
        _blobFactory = blobFactory;
        _httpClientFactory = httpClientFactory;
        _principalAccessor = principalAccessor;
        _handlers = handlers;
        _logger = logger;
    }

    public FileUploadResponse UploadAndParse(Stream stream, string fileName)
    {
        var ext = Path.GetExtension(fileName);
        var parser = _parsers.FirstOrDefault(p => p.CanParse(ext))
            ?? throw new InvalidOperationException($"Unsupported file type: {ext}");

        var result = parser.Parse(stream, fileName);
        var session = _sessionStore.Create();
        session.FileName = fileName;
        session.FileType = ext.TrimStart('.').ToUpperInvariant();
        session.Columns = result.Columns;
        session.Rows = result.Rows;

        const int sampleSize = 10;
        return new FileUploadResponse
        {
            SessionId = session.SessionId,
            FileName = fileName,
            FileType = session.FileType,
            Columns = result.Columns.Select(c => new ColumnInfo
            {
                Name = c,
                SampleValues = result.Rows.Take(sampleSize)
                    .Select(r => r.TryGetValue(c, out var v) ? v : "")
                    .ToList()
            }).ToList(),
            SampleRows = result.Rows.Take(sampleSize).ToList(),
            TotalRowCount = result.Rows.Count
        };
    }

    public List<ImportContentTypeInfo> GetContentTypes(string? filter = null)
    {
        var types = _contentTypeRepository.List()
            .Where(ct => ct.ModelType != null)
            .Select(ct =>
            {
                var baseType = "Other";
                if (typeof(PageData).IsAssignableFrom(ct.ModelType)) baseType = "Page";
                else if (typeof(BlockData).IsAssignableFrom(ct.ModelType)) baseType = "Block";
                else if (typeof(MediaData).IsAssignableFrom(ct.ModelType)) baseType = "Media";

                return new ImportContentTypeInfo
                {
                    Id = ct.ID,
                    Name = ct.Name,
                    DisplayName = ct.DisplayName ?? ct.Name,
                    GroupName = ct.GroupName,
                    BaseType = baseType
                };
            })
            .OrderBy(ct => ct.BaseType)
            .ThenBy(ct => ct.DisplayName);

        if (!string.IsNullOrEmpty(filter))
            types = types.Where(ct => ct.BaseType.Equals(filter, StringComparison.OrdinalIgnoreCase))
                .OrderBy(ct => ct.DisplayName);

        return types.ToList();
    }

    public ImportContentTypeInfo? GetContentTypeWithProperties(int contentTypeId)
    {
        var ct = _contentTypeRepository.Load(contentTypeId);
        if (ct?.ModelType == null) return null;

        var baseType = "Other";
        if (typeof(PageData).IsAssignableFrom(ct.ModelType)) baseType = "Page";
        else if (typeof(BlockData).IsAssignableFrom(ct.ModelType)) baseType = "Block";
        else if (typeof(MediaData).IsAssignableFrom(ct.ModelType)) baseType = "Media";

        var info = new ImportContentTypeInfo
        {
            Id = ct.ID,
            Name = ct.Name,
            DisplayName = ct.DisplayName ?? ct.Name,
            GroupName = ct.GroupName,
            BaseType = baseType
        };

        foreach (var propDef in ct.PropertyDefinitions)
        {
            var isBuiltIn = ImportableBuiltInProperties.Contains(propDef.Name);
            if (SystemPropertyNames.Contains(propDef.Name) && !isBuiltIn) continue;

            var typeName = propDef.Type?.DataType.ToString() ?? "String";
            var propTypeName = propDef.Type?.Name ?? typeName;

            info.Properties.Add(new ImportPropertyInfo
            {
                Name = propDef.Name,
                DisplayName = propDef.EditCaption ?? propDef.Name,
                TypeName = propTypeName,
                IsRequired = propDef.Required,
                IsContentArea = propTypeName.Contains("ContentArea", StringComparison.OrdinalIgnoreCase),
                IsXhtmlString = propTypeName.Contains("XhtmlString", StringComparison.OrdinalIgnoreCase),
                IsContentReference = propTypeName.Contains("ContentReference", StringComparison.OrdinalIgnoreCase)
                    || propTypeName.Contains("PageReference", StringComparison.OrdinalIgnoreCase),
                IsBoolean = propTypeName.Contains("Boolean", StringComparison.OrdinalIgnoreCase),
                IsBuiltIn = isBuiltIn
            });
        }

        return info;
    }

    public List<string> GetLanguages()
    {
        return _languageBranchRepository.ListEnabled()
            .Select(lb => lb.LanguageID)
            .ToList();
    }

    public DryRunResponse DryRun(ImportMappingRequest request)
    {
        var session = _sessionStore.Get(request.SessionId)
            ?? throw new InvalidOperationException("Session not found");

        var ct = _contentTypeRepository.Load(request.TargetContentTypeId)
            ?? throw new InvalidOperationException("Content type not found");

        var filteredRows = ApplyRowFilters(session.Rows, request.RowFilters).ToList();

        var response = new DryRunResponse
        {
            SessionId = request.SessionId,
            TotalCount = filteredRows.Count
        };

        var previewCount = Math.Min(filteredRows.Count, 20);
        for (var i = 0; i < previewCount; i++)
        {
            var row = filteredRows[i];
            var preview = new PreviewItem { RowIndex = i + 1, Warnings = new() };

            // Resolve name
            if (!string.IsNullOrEmpty(request.NameSourceColumn))
            {
                var resolvedName = ResolveTemplate(request.NameSourceColumn, row);
                preview.Name = string.IsNullOrWhiteSpace(resolvedName) ? $"Import-{i + 1}" : resolvedName;
            }
            else
            {
                preview.Name = $"Import-{i + 1}";
                preview.Warnings.Add("No name column mapped");
            }

            // Preview mapped properties
            foreach (var mapping in request.Mappings.Where(m => m.MappingType != "skip"))
            {
                var blockCount = mapping.InlineBlocks?.Count ?? (mapping.InlineBlock != null ? 1 : 0);
                var value = mapping.MappingType switch
                {
                    "column" => row.TryGetValue(mapping.SourceColumn ?? "", out var v) ? v : "[missing]",
                    "hardcoded" => ResolveTemplate(mapping.HardcodedValue, row) ?? "",
                    "inline-block" => $"[{blockCount} inline block(s)]",
                    _ => ""
                };
                preview.Properties[mapping.TargetProperty] = value;
            }

            response.PreviewItems.Add(preview);
        }

        // Store mapping for execution
        session.Mapping = request;
        return response;
    }

    public Guid StartImport(Guid sessionId)
    {
        var session = _sessionStore.Get(sessionId)
            ?? throw new InvalidOperationException("Session not found");

        if (session.Mapping == null)
            throw new InvalidOperationException("No mapping configured. Run dry-run first.");

        session.Progress = new ImportProgress
        {
            SessionId = sessionId,
            Status = "running",
            Total = session.Rows.Count
        };

        // Capture the requesting user so the background task enforces *their* content
        // ACLs. A detached Task in CMS 12+ runs with an unauthenticated principal by
        // default, so we flow the request principal in and restore it on the worker.
        var principal = _principalAccessor.Principal;

        // Run import in background
        Task.Run(() => ExecuteImportAsync(session, principal));

        return sessionId;
    }

    public ImportProgress? GetProgress(Guid sessionId)
    {
        return _sessionStore.Get(sessionId)?.Progress;
    }

    private async Task ExecuteImportAsync(ImportSession session, IPrincipal principal)
    {
        var mapping = session.Mapping!;
        var progress = session.Progress!;

        // Restore the requesting user's principal on this background thread so that
        // IContentRepository.Save enforces the caller's per-node access rights.
        // (PrincipalInfo.CurrentPrincipal is read-only in CMS 12+; set via the accessor.)
        _principalAccessor.Principal = principal;

        try
        {
            var ct = _contentTypeRepository.Load(mapping.TargetContentTypeId);
            if (ct == null)
            {
                progress.Status = "failed";
                progress.Errors.Add(new ImportError { RowIndex = 0, Message = "Content type not found" });
                return;
            }

            var filteredRows = ApplyRowFilters(session.Rows, mapping.RowFilters).ToList();
            progress.Total = filteredRows.Count;

            var parentRef = new ContentReference(mapping.ParentContentId);
            var culture = new CultureInfo(mapping.Language);

            for (var i = 0; i < filteredRows.Count; i++)
            {
                try
                {
                    var row = filteredRows[i];
                    var rowWarnings = new List<string>();
                    var contentId = CreateContentFromRow(ct, parentRef, culture, row, mapping, i, rowWarnings);
                    if (contentId > 0)
                        progress.CreatedContentIds.Add(contentId);
                    foreach (var w in rowWarnings)
                        progress.Warnings.Add(new ImportWarning { RowIndex = i + 1, Message = w });
                }
                catch (EPiServer.Core.AccessDeniedException ex)
                {
                    progress.Errors.Add(new ImportError
                    {
                        RowIndex = i + 1,
                        Message = "Access denied: you do not have permission to publish content at the target location."
                    });
                    _logger.LogWarning(ex, "Access denied saving row {RowIndex}", i + 1);
                }
                catch (Exception ex)
                {
                    progress.Errors.Add(new ImportError
                    {
                        RowIndex = i + 1,
                        Message = "Failed to import this row. Check server logs for details."
                    });
                    _logger.LogWarning(ex, "Failed to import row {RowIndex}", i + 1);
                }

                progress.Processed = i + 1;
            }

            progress.Status = "completed";
        }
        catch (Exception ex)
        {
            progress.Status = "failed";
            progress.Errors.Add(new ImportError { RowIndex = 0, Message = "Import failed. Check server logs for details." });
            _logger.LogError(ex, "Import failed for session {SessionId}", session.SessionId);
        }
    }

    private int CreateContentFromRow(
        ContentType contentType,
        ContentReference parentRef,
        CultureInfo culture,
        Dictionary<string, string> row,
        ImportMappingRequest mapping,
        int rowIndex,
        List<string> rowWarnings)
    {
        var content = _contentRepository.GetDefault<IContent>(parentRef, contentType.ID, culture);

        // Set name (supports {Column} templates)
        if (!string.IsNullOrEmpty(mapping.NameSourceColumn))
        {
            var resolvedName = ResolveTemplate(mapping.NameSourceColumn, row);
            content.Name = string.IsNullOrWhiteSpace(resolvedName) ? $"Import-{rowIndex + 1}" : resolvedName;
        }
        else
        {
            content.Name = $"Import-{rowIndex + 1}";
        }

        // Apply built-in properties via interfaces (not PropertyData)
        ApplyBuiltInProperties(content, mapping.Mappings, row);

        // Collect deferred mappings that need the content saved first (for asset folder)
        var deferredImageMappings = new List<(PropertyMapping mapping, string value)>();
        var deferredBlockMappings = new List<(PropertyMapping mapping, List<InlineBlockMapping> blocks)>();

        // Apply regular property mappings
        foreach (var propMapping in mapping.Mappings.Where(m => m.MappingType != "skip"))
        {
            // Skip built-in properties handled above
            if (ImportableBuiltInProperties.Contains(propMapping.TargetProperty)) continue;

            var prop = content.Property[propMapping.TargetProperty];
            if (prop == null) continue;

            try
            {
                switch (propMapping.MappingType)
                {
                    case "column":
                        if (row.TryGetValue(propMapping.SourceColumn ?? "", out var value))
                        {
                            if (IsImageProperty(prop) && IsUrl(value))
                                deferredImageMappings.Add((propMapping, value));
                            else
                                SetPropertyValue(prop, value, contentType);
                        }
                        break;
                    case "hardcoded":
                        var resolved = ResolveTemplate(propMapping.HardcodedValue, row);
                        if (IsImageProperty(prop) && IsUrl(resolved))
                            deferredImageMappings.Add((propMapping, resolved!));
                        else
                            SetPropertyValue(prop, resolved, contentType);
                        break;
                    case "inline-block":
                        var blocks = propMapping.InlineBlocks ?? (propMapping.InlineBlock != null
                            ? new List<InlineBlockMapping> { propMapping.InlineBlock }
                            : null);
                        if (blocks != null)
                            deferredBlockMappings.Add((propMapping, blocks));
                        break;
                }
            }
            catch (Exception ex)
            {
                rowWarnings.Add($"Could not set property '{propMapping.TargetProperty}'");
                _logger.LogWarning(ex, "Failed to set property {Property} on row {Row}",
                    propMapping.TargetProperty, rowIndex + 1);
            }
        }

        // Save content first as draft to get a content link for the asset folder
        var hasDeferred = deferredImageMappings.Count > 0 || deferredBlockMappings.Count > 0;
        var initialSaveAction = hasDeferred ? SaveAction.Save : (mapping.PublishAfterImport ? SaveAction.Publish : SaveAction.Save);
        var saved = _contentRepository.Save(content, initialSaveAction, RequiredAccess(initialSaveAction));

        // Handle deferred mappings (images + inline blocks) — content exists now
        if (hasDeferred)
        {
            var loaded = _contentRepository.Get<IContent>(saved);
            var writableContent = (IContent)((ContentData)loaded).CreateWritableClone();

            // Create inline blocks in the "for this content" asset folder
            foreach (var (blockMapping, blocks) in deferredBlockMappings)
            {
                try
                {
                    var prop = writableContent.Property[blockMapping.TargetProperty];
                    if (prop != null)
                        SetContentAreaFromInlineBlocks(saved, prop, blocks, row);
                }
                catch (Exception ex)
                {
                    rowWarnings.Add($"Could not create inline blocks for property '{blockMapping.TargetProperty}'");
                    _logger.LogWarning(ex, "Failed to create inline blocks for {Property} on row {Row}",
                        blockMapping.TargetProperty, rowIndex + 1);
                }
            }

            // Download images into asset folder
            foreach (var (imgMapping, imgUrl) in deferredImageMappings)
            {
                try
                {
                    var imageRef = DownloadAndCreateImage(saved, imgUrl, content.Name, imgMapping.TargetProperty);
                    if (!ContentReference.IsNullOrEmpty(imageRef))
                    {
                        var prop = writableContent.Property[imgMapping.TargetProperty];
                        if (prop != null)
                            prop.Value = imageRef;
                    }
                }
                catch (Exception ex)
                {
                    rowWarnings.Add($"Could not download image for property '{imgMapping.TargetProperty}' from '{imgUrl}'");
                    _logger.LogWarning(ex, "Failed to download image for {Property} from {Url} on row {Row}",
                        imgMapping.TargetProperty, imgUrl, rowIndex + 1);
                }
            }

            var finalAction = mapping.PublishAfterImport ? SaveAction.Publish : SaveAction.Save;
            saved = _contentRepository.Save(writableContent, finalAction | SaveAction.ForceCurrentVersion, RequiredAccess(finalAction));
        }

        return saved.ID;
    }

    private static bool IsImageProperty(PropertyData prop)
    {
        var typeName = prop.Type.ToString();
        return typeName.Contains("ContentReference", StringComparison.OrdinalIgnoreCase)
            || typeName.Contains("PageReference", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Maps a save action to the access level the repository must enforce against the
    /// current principal. Publishing requires Publish rights; any other save requires Edit.
    /// Never use AccessLevel.NoAccess here — it bypasses per-node ACL checks.
    /// </summary>
    private static AccessLevel RequiredAccess(SaveAction action) =>
        (action & SaveAction.Publish) == SaveAction.Publish ? AccessLevel.Publish : AccessLevel.Edit;

    private static bool IsUrl(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;
        return value.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("https://", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Returns true only if <paramref name="url"/> is an absolute http/https URL whose host
    /// resolves exclusively to publicly routable addresses. Used to gate server-side image
    /// fetches against SSRF (internal services, cloud metadata).
    /// </summary>
    private static bool IsPubliclyRoutable(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return false;
        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
            return false;

        IPAddress[] addresses;
        if (IPAddress.TryParse(uri.Host, out var literal))
        {
            addresses = [literal];
        }
        else
        {
            try { addresses = Dns.GetHostAddresses(uri.Host); }
            catch { return false; }
        }

        return addresses.Length > 0 && addresses.All(IsPublicAddress);
    }

    private static bool IsPublicAddress(IPAddress ip)
    {
        if (ip.IsIPv4MappedToIPv6)
            ip = ip.MapToIPv4();

        if (IPAddress.IsLoopback(ip))
            return false;

        if (ip.AddressFamily == AddressFamily.InterNetwork)
        {
            var b = ip.GetAddressBytes();
            return b[0] switch
            {
                0 => false,                                   // 0.0.0.0/8
                10 => false,                                  // 10.0.0.0/8 (private)
                100 when b[1] is >= 64 and <= 127 => false,   // 100.64.0.0/10 (CGNAT)
                169 when b[1] == 254 => false,                // 169.254.0.0/16 (link-local + metadata)
                172 when b[1] is >= 16 and <= 31 => false,    // 172.16.0.0/12 (private)
                192 when b[1] == 168 => false,                // 192.168.0.0/16 (private)
                _ => true
            };
        }

        if (ip.AddressFamily == AddressFamily.InterNetworkV6)
        {
            return !ip.IsIPv6LinkLocal
                && !ip.IsIPv6SiteLocal
                && !ip.IsIPv6UniqueLocal
                && !ip.Equals(IPAddress.IPv6Any);
        }

        return false;
    }

    /// <summary>Named <see cref="HttpClient"/> used for image downloads (auto-redirect disabled).</summary>
    public const string ImageDownloadClientName = "EptImageDownload";

    private ContentReference DownloadAndCreateImage(ContentReference ownerContentLink, string imageUrl, string contentName, string propertyName)
    {
        // SSRF guard: only fetch URLs that resolve to a publicly routable address. This blocks
        // loopback, private (RFC1918), CGNAT, link-local and the cloud metadata endpoint
        // (169.254.169.254) even though the importing user is authenticated.
        if (!IsPubliclyRoutable(imageUrl))
            throw new InvalidOperationException("Image URL is not allowed.");

        using var httpClient = _httpClientFactory.CreateClient(ImageDownloadClientName);
        httpClient.Timeout = TimeSpan.FromSeconds(30);

        using var response = httpClient.GetAsync(imageUrl).GetAwaiter().GetResult();
        response.EnsureSuccessStatusCode();

        var contentType = response.Content.Headers.ContentType?.MediaType ?? "image/jpeg";
        var uri = new Uri(imageUrl);
        var fileName = Path.GetFileName(uri.LocalPath);
        if (string.IsNullOrWhiteSpace(fileName) || !fileName.Contains('.'))
        {
            var ext = contentType switch
            {
                "image/png" => ".png",
                "image/gif" => ".gif",
                "image/webp" => ".webp",
                "image/svg+xml" => ".svg",
                _ => ".jpg"
            };
            fileName = $"{contentName}-{propertyName}{ext}";
        }

        // Get or create the "For this content" asset folder
        var assetFolder = _contentAssetHelper.GetOrCreateAssetFolder(ownerContentLink);

        // Create the image media content
        var imageMedia = _contentRepository.GetDefault<ImageData>(assetFolder.ContentLink);
        imageMedia.Name = fileName;

        // Write blob data
        var blob = _blobFactory.CreateBlob(imageMedia.BinaryDataContainer, Path.GetExtension(fileName));
        using (var blobStream = blob.OpenWrite())
        {
            response.Content.ReadAsStream().CopyTo(blobStream);
        }
        imageMedia.BinaryData = blob;

        return _contentRepository.Save(imageMedia, SaveAction.Publish, AccessLevel.Edit);
    }

    private void ApplyBuiltInProperties(IContent content, List<PropertyMapping> mappings, Dictionary<string, string> row)
    {
        foreach (var m in mappings.Where(m => m.MappingType != "skip" && ImportableBuiltInProperties.Contains(m.TargetProperty)))
        {
            var rawValue = m.MappingType switch
            {
                "column" => row.TryGetValue(m.SourceColumn ?? "", out var v) ? v : null,
                "hardcoded" => ResolveTemplate(m.HardcodedValue, row),
                _ => null
            };
            if (string.IsNullOrWhiteSpace(rawValue)) continue;

            try
            {
                switch (m.TargetProperty)
                {
                    case "PageURLSegment":
                        if (content is PageData page)
                            page.URLSegment = rawValue;
                        break;

                    case "PageStartPublish":
                        if (content is IVersionable versionableStart &&
                            DateTime.TryParse(rawValue, CultureInfo.InvariantCulture, DateTimeStyles.None, out var startDate))
                            versionableStart.StartPublish = startDate;
                        break;

                    case "PageStopPublish":
                        if (content is IVersionable versionableStop &&
                            DateTime.TryParse(rawValue, CultureInfo.InvariantCulture, DateTimeStyles.None, out var stopDate))
                            versionableStop.StopPublish = stopDate;
                        break;

                    case "PageCreatedBy":
                        if (content is IChangeTrackable trackableCreated)
                            trackableCreated.CreatedBy = rawValue;
                        break;

                    case "PageChangedBy":
                        if (content is IChangeTrackable trackableChanged)
                            trackableChanged.ChangedBy = rawValue;
                        break;

                    case "PageVisibleInMenu":
                        if (content is PageData menuPage &&
                            bool.TryParse(rawValue, out var visible))
                            menuPage.VisibleInMenu = visible;
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to set built-in property {Property}", m.TargetProperty);
            }
        }
    }

    private void SetPropertyValue(PropertyData prop, string? value, ContentType? contentType = null)
    {
        if (value == null) return;

        var pd = contentType?.PropertyDefinitions.FirstOrDefault(d => d.Name == prop.Name);
        var ctx = pd != null
            ? PropertyHandlerContext.ForProperty(prop, pd, contentType!.ModelType)
            : PropertyHandlerContext.ForProperty(prop);
        var handler = _handlers.Resolve(ctx);
        if (!handler.TryParse(value, ctx, out var parsed))
            throw new InvalidOperationException($"No property-type handler could parse a value for '{prop.Name}'.");
        prop.Value = parsed;
    }

    private static IEnumerable<Dictionary<string, string>> ApplyRowFilters(
        IEnumerable<Dictionary<string, string>> rows,
        List<RowFilterItem>? filters)
    {
        if (filters == null || filters.Count == 0) return rows;
        return rows.Where(row => filters.All(f => RowMatchesFilter(row, f)));
    }

    private static bool RowMatchesFilter(Dictionary<string, string> row, RowFilterItem filter)
    {
        row.TryGetValue(filter.Column, out var value);
        value ??= string.Empty;

        return filter.Operator switch
        {
            "equals" => string.Equals(value, filter.Value, StringComparison.OrdinalIgnoreCase),
            "not_equals" => !string.Equals(value, filter.Value, StringComparison.OrdinalIgnoreCase),
            "contains" => value.Contains(filter.Value, StringComparison.OrdinalIgnoreCase),
            "not_empty" => !string.IsNullOrWhiteSpace(value),
            "is_empty" => string.IsNullOrWhiteSpace(value),
            _ => true
        };
    }

    private void SetContentAreaFromInlineBlocks(
        ContentReference ownerContentLink,
        PropertyData prop,
        List<InlineBlockMapping> blockMappings,
        Dictionary<string, string> row)
    {
        var contentArea = prop.Value as ContentArea ?? new ContentArea();

        // Get or create the "for this content" asset folder
        var assetFolder = _contentAssetHelper.GetOrCreateAssetFolder(ownerContentLink);

        foreach (var blockMapping in blockMappings)
        {
            var blockType = _contentTypeRepository.Load(blockMapping.BlockTypeId);
            if (blockType == null) continue;

            // Create block in the content's asset folder (inline/local block)
            var block = _contentRepository.GetDefault<IContent>(
                assetFolder.ContentLink, blockType.ID);

            block.Name = $"{blockType.DisplayName ?? blockType.Name}";

            foreach (var bm in blockMapping.Mappings.Where(m => m.MappingType != "skip"))
            {
                var blockProp = block.Property[bm.TargetProperty];
                if (blockProp == null) continue;

                switch (bm.MappingType)
                {
                    case "column":
                        if (row.TryGetValue(bm.SourceColumn ?? "", out var val))
                            SetPropertyValue(blockProp, val, blockType);
                        break;
                    case "hardcoded":
                        var resolved = ResolveTemplate(bm.HardcodedValue, row);
                        SetPropertyValue(blockProp, resolved, blockType);
                        break;
                }
            }

            var savedBlock = _contentRepository.Save(block, SaveAction.Save, AccessLevel.Edit);
            contentArea.Items.Add(new ContentAreaItem { ContentLink = savedBlock });
        }

        prop.Value = contentArea;
    }

    /// <summary>
    /// Resolves {ColumnName} placeholders in a template string using row data.
    /// If the value has no braces, it's returned as-is (plain column name lookup for name field).
    /// </summary>
    private static string? ResolveTemplate(string? template, Dictionary<string, string> row)
    {
        if (string.IsNullOrEmpty(template)) return template;

        // If no braces, try as a direct column lookup (for backwards compat with name field)
        if (!template.Contains('{'))
        {
            return row.TryGetValue(template, out var direct) ? direct : template;
        }

        // Replace {ColumnName} placeholders
        return System.Text.RegularExpressions.Regex.Replace(template, @"\{([^}]+)\}", match =>
        {
            var colName = match.Groups[1].Value;
            return row.TryGetValue(colName, out var val) ? val : match.Value;
        });
    }
}
