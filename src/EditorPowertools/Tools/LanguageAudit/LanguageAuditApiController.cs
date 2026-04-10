using EditorPowertools.Infrastructure;
using EditorPowertools.Permissions;
using EditorPowertools.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EditorPowertools.Tools.LanguageAudit;

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
    [Route("editorpowertools/api/language-audit/overview")]
    public IActionResult GetOverview()
    {
        if (!HasAccess()) return Forbid();

        var overview = _service.GetOverview();
        return Ok(overview);
    }

    [HttpGet]
    [Route("editorpowertools/api/language-audit/missing")]
    public IActionResult GetMissingTranslations([FromQuery] string language, [FromQuery] int? parentId = null)
    {
        if (!HasAccess()) return Forbid();

        if (string.IsNullOrEmpty(language))
            return BadRequest(new { error = "language parameter is required" });

        var missing = _service.GetMissingTranslations(language, parentId);
        return Ok(missing);
    }

    [HttpGet]
    [Route("editorpowertools/api/language-audit/coverage-tree")]
    public IActionResult GetCoverageTree([FromQuery] string language)
    {
        if (!HasAccess()) return Forbid();

        if (string.IsNullOrEmpty(language))
            return BadRequest(new { error = "language parameter is required" });

        var tree = _service.GetCoverageTree(language);
        return Ok(tree);
    }

    [HttpGet]
    [Route("editorpowertools/api/language-audit/stale")]
    public IActionResult GetStaleTranslations([FromQuery] int thresholdDays = 30, [FromQuery] string? language = null)
    {
        if (!HasAccess()) return Forbid();

        var stale = _service.GetStaleTranslations(thresholdDays, language);
        return Ok(stale);
    }

    [HttpGet]
    [Route("editorpowertools/api/language-audit/queue")]
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
    [Route("editorpowertools/api/language-audit/export")]
    public IActionResult ExportTranslationQueue([FromQuery] string targetLanguage)
    {
        if (!HasAccess()) return Forbid();

        if (string.IsNullOrEmpty(targetLanguage))
            return BadRequest(new { error = "targetLanguage parameter is required" });

        var items = _service.ExportTranslationQueue(targetLanguage);
        return Ok(items);
    }

    [HttpPost]
    [Route("editorpowertools/api/language-audit/aggregation-start")]
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
