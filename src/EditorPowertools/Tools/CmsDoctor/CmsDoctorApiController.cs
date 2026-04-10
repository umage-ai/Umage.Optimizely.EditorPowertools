using UmageAI.Optimizely.EditorPowerTools.Infrastructure;
using UmageAI.Optimizely.EditorPowerTools.Permissions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace UmageAI.Optimizely.EditorPowerTools.Tools.CmsDoctor;

[Authorize(Policy = "codeart:editorpowertools")]
[RequireAjax]
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

    [HttpGet]
    public IActionResult GetDashboard()
    {
        if (!HasAccess()) return Forbid();
        return Ok(_service.GetDashboard());
    }

    [HttpPost]
    public IActionResult RunAll()
    {
        if (!HasAccess()) return Forbid();
        var results = _service.RunAll();
        return Ok(_service.GetDashboard());
    }

    [HttpPost]
    public IActionResult RunCheck([FromQuery] string id)
    {
        if (!HasAccess()) return Forbid();
        var result = _service.RunCheck(Uri.UnescapeDataString(id));
        return result == null ? NotFound() : Ok(result);
    }

    [HttpPost]
    public IActionResult FixCheck([FromQuery] string id)
    {
        if (!HasAccess()) return Forbid();
        var result = _service.FixCheck(Uri.UnescapeDataString(id));
        return result == null ? NotFound() : Ok(result);
    }

    [HttpPost]
    public IActionResult DismissCheck([FromQuery] string id)
    {
        if (!HasAccess()) return Forbid();
        _service.DismissCheck(Uri.UnescapeDataString(id));
        return Ok(new { dismissed = true });
    }

    [HttpPost]
    public IActionResult RestoreCheck([FromQuery] string id)
    {
        if (!HasAccess()) return Forbid();
        _service.RestoreCheck(Uri.UnescapeDataString(id));
        return Ok(new { restored = true });
    }

    [HttpGet]
    public IActionResult GetTags()
    {
        if (!HasAccess()) return Forbid();
        return Ok(_service.GetAllTags());
    }
}
