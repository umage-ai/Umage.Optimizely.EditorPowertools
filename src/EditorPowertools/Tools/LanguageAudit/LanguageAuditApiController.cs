using UmageAI.Optimizely.EditorPowerTools.Infrastructure;
using UmageAI.Optimizely.EditorPowerTools.Permissions;
using UmageAI.Optimizely.EditorPowerTools.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace UmageAI.Optimizely.EditorPowerTools.Tools.LanguageAudit;

/// <summary>
/// API-only controller for Language Audit data endpoints.
/// The page view is served by EditorPowertoolsController.LanguageAudit().
/// </summary>
[Authorize(Policy = "codeart:editorpowertools")]
[RequireAjax]
public class LanguageAuditApiController : Controller
{
    private readonly LanguageAuditService _service;
    private readonly FeatureAccessChecker _accessChecker;
    private readonly AggregationJobStatusService _aggregationJobService;

    public LanguageAuditApiController(
        LanguageAuditService service,
        FeatureAccessChecker accessChecker,
        AggregationJobStatusService aggregationJobService)
    {
        _service = service;
        _accessChecker = accessChecker;
        _aggregationJobService = aggregationJobService;
    }

    [HttpGet]
    public IActionResult GetOverview()
    {
        if (!HasAccess()) return Forbid();

        var overview = _service.GetOverview();
        return Ok(overview);
    }

    [HttpGet]
    public IActionResult GetMissingTranslations([FromQuery] string language, [FromQuery] int? parentId = null)
    {
        if (!HasAccess()) return Forbid();

        if (string.IsNullOrEmpty(language))
            return BadRequest(new { error = "language parameter is required" });

        var missing = _service.GetMissingTranslations(language, parentId);
        return Ok(missing);
    }

    [HttpGet]
    public IActionResult GetCoverageTree([FromQuery] string language)
    {
        if (!HasAccess()) return Forbid();

        if (string.IsNullOrEmpty(language))
            return BadRequest(new { error = "language parameter is required" });

        var tree = _service.GetCoverageTree(language);
        return Ok(tree);
    }

    [HttpGet]
    public IActionResult GetStaleTranslations([FromQuery] int thresholdDays = 30, [FromQuery] string? language = null)
    {
        if (!HasAccess()) return Forbid();

        var stale = _service.GetStaleTranslations(thresholdDays, language);
        return Ok(stale);
    }

    [HttpGet]
    public IActionResult GetTranslationQueue(
        [FromQuery] string targetLanguage,
        [FromQuery] string? contentType = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        if (!HasAccess()) return Forbid();

        if (string.IsNullOrEmpty(targetLanguage))
            return BadRequest(new { error = "targetLanguage parameter is required" });

        var queue = _service.GetTranslationQueue(targetLanguage, contentType, page, pageSize);
        return Ok(queue);
    }

    [HttpGet]
    public IActionResult ExportTranslationQueue([FromQuery] string targetLanguage)
    {
        if (!HasAccess()) return Forbid();

        if (string.IsNullOrEmpty(targetLanguage))
            return BadRequest(new { error = "targetLanguage parameter is required" });

        var items = _service.ExportTranslationQueue(targetLanguage);
        return Ok(items);
    }

    [HttpPost]
    public async Task<IActionResult> StartAggregationJob()
    {
        if (!HasAccess()) return Forbid();

        var result = await _aggregationJobService.StartJobAsync();
        if (!result.Started)
        {
            return result.Reason == "already_running"
                ? Conflict(new { success = false, message = "Job is already running." })
                : StatusCode(503, new { success = false, message = "Job not found in scheduler." });
        }
        return Ok(new { success = true, started = true });
    }

    private bool HasAccess()
    {
        return _accessChecker.HasAccess(HttpContext,
            nameof(Configuration.FeatureToggles.LanguageAudit),
            EditorPowertoolsPermissions.LanguageAudit);
    }
}
