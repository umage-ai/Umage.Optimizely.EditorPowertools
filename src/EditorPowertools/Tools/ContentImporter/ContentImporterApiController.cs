using EditorPowertools.Infrastructure;
using EditorPowertools.Permissions;
using EditorPowertools.Tools.ContentImporter.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace EditorPowertools.Tools.ContentImporter;

[Authorize(Policy = "codeart:editorpowertools")]
[RequireAjax]
public class ContentImporterApiController : Controller
{
    private readonly ContentImporterService _service;
    private readonly FeatureAccessChecker _accessChecker;

    public ContentImporterApiController(
        ContentImporterService service,
        FeatureAccessChecker accessChecker)
    {
        _service = service;
        _accessChecker = accessChecker;
    }

    private bool HasAccess() => _accessChecker.HasAccess(HttpContext,
        nameof(Configuration.FeatureToggles.ContentImporter),
        EditorPowertoolsPermissions.ContentImporter);

    [HttpPost]
    [Route("editorpowertools/api/content-importer/upload")]
    public IActionResult Upload(IFormFile file)
    {
        if (!HasAccess()) return Forbid();

        if (file == null || file.Length == 0)
            return BadRequest(new { error = "No file uploaded" });

        if (file.Length > 50 * 1024 * 1024) // 50 MB limit
            return BadRequest(new { error = "File too large (max 50 MB)" });

        using var stream = file.OpenReadStream();
        var result = _service.UploadAndParse(stream, file.FileName);
        return Ok(result);
    }

    [HttpGet]
    [Route("editorpowertools/api/content-importer/content-types")]
    public IActionResult GetContentTypes([FromQuery] string? filter = null)
    {
        if (!HasAccess()) return Forbid();
        return Ok(_service.GetContentTypes(filter));
    }

    [HttpGet]
    [Route("editorpowertools/api/content-importer/content-types/{id:int}")]
    public IActionResult GetContentType(int id)
    {
        if (!HasAccess()) return Forbid();
        var result = _service.GetContentTypeWithProperties(id);
        return result == null ? NotFound() : Ok(result);
    }

    [HttpGet]
    [Route("editorpowertools/api/content-importer/block-types")]
    public IActionResult GetBlockTypes()
    {
        if (!HasAccess()) return Forbid();
        return Ok(_service.GetContentTypes("Block"));
    }

    [HttpGet]
    [Route("editorpowertools/api/content-importer/languages")]
    public IActionResult GetLanguages()
    {
        if (!HasAccess()) return Forbid();
        return Ok(_service.GetLanguages());
    }

    [HttpPost]
    [Route("editorpowertools/api/content-importer/dry-run")]
    public IActionResult DryRun([FromBody] ImportMappingRequest request)
    {
        if (!HasAccess()) return Forbid();
        try
        {
            var result = _service.DryRun(request);
            return Ok(result);
        }
        catch (Exception)
        {
            return BadRequest(new { error = "An error occurred while processing the request." });
        }
    }

    [HttpPost]
    [Route("editorpowertools/api/content-importer/execute")]
    public IActionResult Execute([FromBody] ImportExecuteRequest request)
    {
        if (!HasAccess()) return Forbid();
        try
        {
            var sessionId = _service.StartImport(request.SessionId);
            return Ok(new { sessionId });
        }
        catch (Exception)
        {
            return BadRequest(new { error = "An error occurred while processing the request." });
        }
    }

    [HttpGet]
    [Route("editorpowertools/api/content-importer/progress/{sessionId:guid}")]
    public IActionResult GetProgress(Guid sessionId)
    {
        if (!HasAccess()) return Forbid();
        var progress = _service.GetProgress(sessionId);
        return progress == null ? NotFound() : Ok(progress);
    }
}

public class ImportExecuteRequest
{
    public Guid SessionId { get; set; }
}
