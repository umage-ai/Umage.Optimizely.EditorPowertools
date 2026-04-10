using UmageAI.Optimizely.EditorPowerTools.Infrastructure;
using UmageAI.Optimizely.EditorPowerTools.Permissions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace UmageAI.Optimizely.EditorPowerTools.Tools.CmsDoctor;

[Authorize(Policy = "codeart:editorpowertools")]
[RequireAjax]
[Route("editorpowertools/api/cms-doctor")]
public class CmsDoctorApiController : Controller
{
    private readonly CmsDoctorService _service;
    private readonly FeatureAccessChecker _accessChecker;

    public CmsDoctorApiController(CmsDoctorService service, FeatureAccessChecker accessChecker)
    {
        _service = service;
        _accessChecker = accessChecker;
    }

    private bool HasAccess() => _accessChecker.HasAccess(HttpContext,
        nameof(Configuration.FeatureToggles.CmsDoctor),
        EditorPowertoolsPermissions.CmsDoctor);

    [HttpGet("dashboard")]
    public IActionResult GetDashboard()
    {
        if (!HasAccess()) return Forbid();
        return Ok(_service.GetDashboard());
    }

    [HttpPost("run-all")]
    public IActionResult RunAll()
    {
        if (!HasAccess()) return Forbid();
        var results = _service.RunAll();
        return Ok(_service.GetDashboard());
    }

    [HttpPost("run/{checkType}")]
    public IActionResult RunCheck(string checkType)
    {
        if (!HasAccess()) return Forbid();
        var result = _service.RunCheck(Uri.UnescapeDataString(checkType));
        return result == null ? NotFound() : Ok(result);
    }

    [HttpPost("fix/{checkType}")]
    public IActionResult FixCheck(string checkType)
    {
        if (!HasAccess()) return Forbid();
        var result = _service.FixCheck(Uri.UnescapeDataString(checkType));
        return result == null ? NotFound() : Ok(result);
    }

    [HttpPost("dismiss/{checkType}")]
    public IActionResult DismissCheck(string checkType)
    {
        if (!HasAccess()) return Forbid();
        _service.DismissCheck(Uri.UnescapeDataString(checkType));
        return Ok(new { dismissed = true });
    }

    [HttpPost("restore/{checkType}")]
    public IActionResult RestoreCheck(string checkType)
    {
        if (!HasAccess()) return Forbid();
        _service.RestoreCheck(Uri.UnescapeDataString(checkType));
        return Ok(new { restored = true });
    }

    [HttpGet("tags")]
    public IActionResult GetTags()
    {
        if (!HasAccess()) return Forbid();
        return Ok(_service.GetAllTags());
    }
}
