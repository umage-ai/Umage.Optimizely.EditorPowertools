using System.Text.Json;
using EditorPowertools.Permissions;
using EditorPowertools.Tools.BulkPropertyEditor.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace EditorPowertools.Tools.BulkPropertyEditor;

/// <summary>
/// API controller for Bulk Property Editor operations.
/// The page view is served by EditorPowertoolsController.BulkPropertyEditor().
/// </summary>
[Authorize(Policy = "codeart:editorpowertools")]
[Route("editorpowertools/api/bulk-editor")]
public class BulkPropertyEditorApiController : Controller
{
    private readonly BulkPropertyEditorService _service;
    private readonly FeatureAccessChecker _accessChecker;
    private readonly ILogger<BulkPropertyEditorApiController> _logger;

    public BulkPropertyEditorApiController(
        BulkPropertyEditorService service,
        FeatureAccessChecker accessChecker,
        ILogger<BulkPropertyEditorApiController> logger)
    {
        _service = service;
        _accessChecker = accessChecker;
        _logger = logger;
    }

    [HttpGet("content-types")]
    public IActionResult GetContentTypes()
    {
        if (!_accessChecker.HasAccess(HttpContext,
            nameof(Configuration.FeatureToggles.BulkPropertyEditor),
            EditorPowertoolsPermissions.BulkPropertyEditor))
            return Forbid();

        try
        {
            List<ContentTypeListItem> contentTypes = _service.GetContentTypes();
            return Ok(new { success = true, contentTypes });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get content types");
            return StatusCode(500, new { success = false, message = "Failed to get content types." });
        }
    }

    [HttpGet("languages")]
    public IActionResult GetLanguages()
    {
        if (!_accessChecker.HasAccess(HttpContext,
            nameof(Configuration.FeatureToggles.BulkPropertyEditor),
            EditorPowertoolsPermissions.BulkPropertyEditor))
            return Forbid();

        try
        {
            List<LanguageInfo> languages = _service.GetLanguages();
            return Ok(new { success = true, languages });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get languages");
            return StatusCode(500, new { success = false, message = "Failed to get languages." });
        }
    }

    [HttpGet("properties/{contentTypeId}")]
    public IActionResult GetProperties(int contentTypeId)
    {
        if (!_accessChecker.HasAccess(HttpContext,
            nameof(Configuration.FeatureToggles.BulkPropertyEditor),
            EditorPowertoolsPermissions.BulkPropertyEditor))
            return Forbid();

        try
        {
            List<PropertyColumnInfo> properties = _service.GetProperties(contentTypeId);
            return Ok(new { success = true, properties });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get properties for content type {ContentTypeId}", contentTypeId);
            return StatusCode(500, new { success = false, message = "Failed to get properties." });
        }
    }

    [HttpGet("content")]
    public async Task<IActionResult> GetContent(
        [FromQuery] int contentTypeId,
        [FromQuery] string language = "en",
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        [FromQuery] string? sortBy = null,
        [FromQuery] string sortDirection = "asc",
        [FromQuery] string? filters = null,
        [FromQuery] string? columns = null,
        [FromQuery] bool includeReferences = false)
    {
        if (!_accessChecker.HasAccess(HttpContext,
            nameof(Configuration.FeatureToggles.BulkPropertyEditor),
            EditorPowertoolsPermissions.BulkPropertyEditor))
            return Forbid();

        try
        {
            List<PropertyFilter>? parsedFilters = null;
            if (!string.IsNullOrEmpty(filters))
            {
                parsedFilters = JsonSerializer.Deserialize<List<PropertyFilter>>(filters, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
            }

            List<string>? parsedColumns = null;
            if (!string.IsNullOrEmpty(columns))
            {
                parsedColumns = columns.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
            }

            var request = new ContentFilterRequest
            {
                ContentTypeId = contentTypeId,
                Language = language,
                Page = page,
                PageSize = pageSize,
                SortBy = sortBy,
                SortDirection = sortDirection,
                Filters = parsedFilters,
                Columns = parsedColumns,
                IncludeReferences = includeReferences
            };

            ContentFilterResponse response = await _service.GetContentAsync(request);
            return Ok(new { success = true, data = response });
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Invalid filters JSON: {Filters}", filters);
            return BadRequest(new { success = false, message = "Invalid filters format." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get content for content type {ContentTypeId}", contentTypeId);
            return StatusCode(500, new { success = false, message = "Failed to get content." });
        }
    }

    [HttpGet("references/{contentId}")]
    public async Task<IActionResult> GetReferences(int contentId)
    {
        if (!_accessChecker.HasAccess(HttpContext,
            nameof(Configuration.FeatureToggles.BulkPropertyEditor),
            EditorPowertoolsPermissions.BulkPropertyEditor))
            return Forbid();

        try
        {
            List<ContentReferenceInfo> references = await _service.GetReferencesAsync(contentId);
            return Ok(new { success = true, references });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get references for content {ContentId}", contentId);
            return StatusCode(500, new { success = false, message = "Failed to get references." });
        }
    }

    [HttpPost("save")]
    public async Task<IActionResult> Save([FromBody] InlineEditRequest request)
    {
        if (!_accessChecker.HasAccess(HttpContext,
            nameof(Configuration.FeatureToggles.BulkPropertyEditor),
            EditorPowertoolsPermissions.BulkPropertyEditor))
            return Forbid();

        try
        {
            await _service.SaveAsync(request);
            return Ok(new { success = true, message = "Content saved." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save content {ContentId}, property {PropertyName}",
                request.ContentId, request.PropertyName);
            return StatusCode(500, new { success = false, message = "Failed to save content." });
        }
    }

    [HttpPost("publish/{contentId}")]
    public async Task<IActionResult> Publish(int contentId, [FromQuery] string language = "en")
    {
        if (!_accessChecker.HasAccess(HttpContext,
            nameof(Configuration.FeatureToggles.BulkPropertyEditor),
            EditorPowertoolsPermissions.BulkPropertyEditor))
            return Forbid();

        try
        {
            await _service.PublishAsync(contentId, language);
            return Ok(new { success = true, message = "Content published." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish content {ContentId} for language {Language}",
                contentId, language);
            return StatusCode(500, new { success = false, message = "Failed to publish content." });
        }
    }

    [HttpPost("bulk-save")]
    public async Task<IActionResult> BulkSave([FromBody] BulkSaveRequest request)
    {
        if (!_accessChecker.HasAccess(HttpContext,
            nameof(Configuration.FeatureToggles.BulkPropertyEditor),
            EditorPowertoolsPermissions.BulkPropertyEditor))
            return Forbid();

        try
        {
            await _service.BulkSaveAsync(request);
            return Ok(new { success = true, message = $"Bulk {request.Action} completed for {request.Items.Count} items." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to bulk save {ItemCount} items with action {Action}",
                request.Items.Count, request.Action);
            return StatusCode(500, new { success = false, message = ex.Message });
        }
    }
}
