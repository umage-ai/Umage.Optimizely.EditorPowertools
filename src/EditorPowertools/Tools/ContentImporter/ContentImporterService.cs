using System.Globalization;
using EPiServer;
using EPiServer.Core;
using EPiServer.DataAbstraction;
using EPiServer.DataAccess;
using EPiServer.Security;
using EPiServer.SpecializedProperties;
using EditorPowertools.Tools.ContentImporter.Models;
using EditorPowertools.Tools.ContentImporter.Parsers;
using Microsoft.Extensions.Logging;

namespace EditorPowertools.Tools.ContentImporter;

public class ContentImporterService
{
    private readonly ImportSessionStore _sessionStore;
    private readonly IEnumerable<IFileParser> _parsers;
    private readonly IContentTypeRepository _contentTypeRepository;
    private readonly IContentRepository _contentRepository;
    private readonly ILanguageBranchRepository _languageBranchRepository;
    private readonly ILogger<ContentImporterService> _logger;

    // System properties to exclude from mapping
    private static readonly HashSet<string> SystemPropertyNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "PageName", "PageLink", "PageParentLink", "PageGuid",
        "PageTypeID", "PageTypeName", "PageCreated", "PageChanged",
        "PageSaved", "PageLanguageBranch", "PageMasterLanguageBranch",
        "PageWorkStatus", "PageStartPublish", "PageStopPublish",
        "PageDeleted", "PageCreatedBy", "PageChangedBy",
        "PageDeletedBy", "PageDeletedDate", "PageShortcutType",
        "PageShortcutLink", "PageTargetFrame", "PageURLSegment",
        "PageExternalURL", "PagePendingPublish", "PageChangedOnPublish",
        "PageCategory", "PageArchiveLink", "PageFolderID",
        "PagePeerOrder", "PageChildOrderRule", "PageVisibleInMenu",
        "icontent_providerdefinitionid"
    };

    public ContentImporterService(
        ImportSessionStore sessionStore,
        IEnumerable<IFileParser> parsers,
        IContentTypeRepository contentTypeRepository,
        IContentRepository contentRepository,
        ILanguageBranchRepository languageBranchRepository,
        ILogger<ContentImporterService> logger)
    {
        _sessionStore = sessionStore;
        _parsers = parsers;
        _contentTypeRepository = contentTypeRepository;
        _contentRepository = contentRepository;
        _languageBranchRepository = languageBranchRepository;
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
            if (SystemPropertyNames.Contains(propDef.Name)) continue;

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
                    || propTypeName.Contains("PageReference", StringComparison.OrdinalIgnoreCase)
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

        var response = new DryRunResponse
        {
            SessionId = request.SessionId,
            TotalCount = session.Rows.Count
        };

        var previewCount = Math.Min(session.Rows.Count, 20);
        for (var i = 0; i < previewCount; i++)
        {
            var row = session.Rows[i];
            var preview = new PreviewItem { RowIndex = i + 1, Warnings = new() };

            // Resolve name
            if (!string.IsNullOrEmpty(request.NameSourceColumn) &&
                row.TryGetValue(request.NameSourceColumn, out var name))
            {
                preview.Name = name;
            }
            else
            {
                preview.Name = $"Import-{i + 1}";
                preview.Warnings.Add("No name column mapped");
            }

            // Preview mapped properties
            foreach (var mapping in request.Mappings.Where(m => m.MappingType != "skip"))
            {
                var value = mapping.MappingType switch
                {
                    "column" => row.TryGetValue(mapping.SourceColumn ?? "", out var v) ? v : "[missing]",
                    "hardcoded" => mapping.HardcodedValue ?? "",
                    "inline-block" => "[inline block]",
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

        // Run import in background
        Task.Run(() => ExecuteImportAsync(session));

        return sessionId;
    }

    public ImportProgress? GetProgress(Guid sessionId)
    {
        return _sessionStore.Get(sessionId)?.Progress;
    }

    private async Task ExecuteImportAsync(ImportSession session)
    {
        var mapping = session.Mapping!;
        var progress = session.Progress!;

        try
        {
            var ct = _contentTypeRepository.Load(mapping.TargetContentTypeId);
            if (ct == null)
            {
                progress.Status = "failed";
                progress.Errors.Add(new ImportError { RowIndex = 0, Message = "Content type not found" });
                return;
            }

            var parentRef = new ContentReference(mapping.ParentContentId);
            var culture = new CultureInfo(mapping.Language);

            for (var i = 0; i < session.Rows.Count; i++)
            {
                try
                {
                    var row = session.Rows[i];
                    var contentId = CreateContentFromRow(ct, parentRef, culture, row, mapping, i);
                    if (contentId > 0)
                        progress.CreatedContentIds.Add(contentId);
                }
                catch (Exception ex)
                {
                    progress.Errors.Add(new ImportError
                    {
                        RowIndex = i + 1,
                        Message = ex.Message
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
            progress.Errors.Add(new ImportError { RowIndex = 0, Message = ex.Message });
            _logger.LogError(ex, "Import failed for session {SessionId}", session.SessionId);
        }
    }

    private int CreateContentFromRow(
        ContentType contentType,
        ContentReference parentRef,
        CultureInfo culture,
        Dictionary<string, string> row,
        ImportMappingRequest mapping,
        int rowIndex)
    {
        var content = _contentRepository.GetDefault<IContent>(parentRef, contentType.ID, culture);

        // Set name
        if (!string.IsNullOrEmpty(mapping.NameSourceColumn) &&
            row.TryGetValue(mapping.NameSourceColumn, out var name) &&
            !string.IsNullOrWhiteSpace(name))
        {
            content.Name = name;
        }
        else
        {
            content.Name = $"Import-{rowIndex + 1}";
        }

        // Apply property mappings
        foreach (var propMapping in mapping.Mappings.Where(m => m.MappingType != "skip"))
        {
            var prop = content.Property[propMapping.TargetProperty];
            if (prop == null) continue;

            try
            {
                switch (propMapping.MappingType)
                {
                    case "column":
                        if (row.TryGetValue(propMapping.SourceColumn ?? "", out var value))
                            SetPropertyValue(prop, value);
                        break;
                    case "hardcoded":
                        SetPropertyValue(prop, propMapping.HardcodedValue);
                        break;
                    case "inline-block":
                        if (propMapping.InlineBlock != null)
                            SetContentAreaFromInlineBlock(content, prop, propMapping.InlineBlock, row);
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to set property {Property} on row {Row}",
                    propMapping.TargetProperty, rowIndex + 1);
            }
        }

        var saveAction = mapping.PublishAfterImport ? SaveAction.Publish : SaveAction.Save;
        var saved = _contentRepository.Save(content, saveAction, AccessLevel.NoAccess);
        return saved.ID;
    }

    private void SetPropertyValue(PropertyData prop, string? value)
    {
        if (value == null) return;

        var typeName = prop.Type.ToString();

        if (typeName.Contains("XhtmlString", StringComparison.OrdinalIgnoreCase))
        {
            prop.Value = new XhtmlString(value);
            return;
        }

        prop.Value = ConvertValue(value, typeName);
    }

    private static object? ConvertValue(string value, string typeName)
    {
        if (typeName.Contains("String", StringComparison.OrdinalIgnoreCase))
            return value;

        if (typeName.Contains("Number", StringComparison.OrdinalIgnoreCase)
            && !typeName.Contains("Float", StringComparison.OrdinalIgnoreCase))
            return int.TryParse(value, CultureInfo.InvariantCulture, out var intResult) ? intResult : null;

        if (typeName.Contains("Float", StringComparison.OrdinalIgnoreCase))
            return double.TryParse(value, CultureInfo.InvariantCulture, out var doubleResult) ? doubleResult : null;

        if (typeName.Contains("Boolean", StringComparison.OrdinalIgnoreCase))
            return bool.TryParse(value, out var boolResult) ? boolResult : null;

        if (typeName.Contains("Date", StringComparison.OrdinalIgnoreCase))
            return DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dateResult)
                ? dateResult : null;

        if (typeName.Contains("Url", StringComparison.OrdinalIgnoreCase))
            return string.IsNullOrWhiteSpace(value) ? null : new EPiServer.Url(value);

        if (typeName.Contains("ContentReference", StringComparison.OrdinalIgnoreCase)
            || typeName.Contains("PageReference", StringComparison.OrdinalIgnoreCase))
            return int.TryParse(value, CultureInfo.InvariantCulture, out var refId)
                ? new ContentReference(refId) : ContentReference.EmptyReference;

        return value;
    }

    private void SetContentAreaFromInlineBlock(
        IContent parentContent,
        PropertyData prop,
        InlineBlockMapping blockMapping,
        Dictionary<string, string> row)
    {
        var blockType = _contentTypeRepository.Load(blockMapping.BlockTypeId);
        if (blockType == null) return;

        // Create the block as a shared block under the parent
        var block = _contentRepository.GetDefault<IContent>(
            ContentReference.GlobalBlockFolder, blockType.ID);

        block.Name = $"{parentContent.Name} - {prop.Name}";

        // Map block properties
        foreach (var bm in blockMapping.Mappings.Where(m => m.MappingType != "skip"))
        {
            var blockProp = block.Property[bm.TargetProperty];
            if (blockProp == null) continue;

            switch (bm.MappingType)
            {
                case "column":
                    if (row.TryGetValue(bm.SourceColumn ?? "", out var val))
                        SetPropertyValue(blockProp, val);
                    break;
                case "hardcoded":
                    SetPropertyValue(blockProp, bm.HardcodedValue);
                    break;
            }
        }

        var savedBlock = _contentRepository.Save(block, SaveAction.Publish, AccessLevel.NoAccess);

        // Add to content area
        var contentArea = prop.Value as ContentArea ?? new ContentArea();
        contentArea.Items.Add(new ContentAreaItem { ContentLink = savedBlock });
        prop.Value = contentArea;
    }
}
