using UmageAI.Optimizely.EditorPowerTools.Infrastructure;
using UmageAI.Optimizely.EditorPowerTools.Permissions;
using UmageAI.Optimizely.EditorPowerTools.Tools.ContentImporter.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace UmageAI.Optimizely.EditorPowerTools.Tools.ContentImporter;

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
    public IActionResult GetContentTypes([FromQuery] string? filter = null)
    {
        if (!HasAccess()) return Forbid();
        return Ok(_service.GetContentTypes(filter));
    }

    [HttpGet]
    public IActionResult GetContentType(int id)
    {
        if (!HasAccess()) return Forbid();
        var result = _service.GetContentTypeWithProperties(id);
        return result == null ? NotFound() : Ok(result);
    }

    [HttpGet]
    public IActionResult GetBlockTypes()
    {
        if (!HasAccess()) return Forbid();
        return Ok(_service.GetContentTypes("Block"));
    }

    [HttpGet]
    public IActionResult GetLanguages()
    {
        if (!HasAccess()) return Forbid();
        return Ok(_service.GetLanguages());
    }

    [HttpPost]
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
    public IActionResult GetProgress([FromQuery] Guid id)
    {
        if (!HasAccess()) return Forbid();
        var progress = _service.GetProgress(id);
        return progress == null ? NotFound() : Ok(progress);
    }
}

public class ImportExecuteRequest
{
    public Guid SessionId { get; set; }
}
