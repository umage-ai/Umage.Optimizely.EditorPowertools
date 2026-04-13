using UmageAI.Optimizely.EditorPowerTools.Infrastructure;
using UmageAI.Optimizely.EditorPowerTools.Permissions;
using UmageAI.Optimizely.EditorPowerTools.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace UmageAI.Optimizely.EditorPowerTools.Components;

/// <summary>
/// API endpoints for per-user tool preferences. Shared across all tools.
/// </summary>
[Authorize(Policy = "codeart:editorpowertools")]
[RequireAjax]
public class PreferencesApiController : Controller
{
    private readonly UserPreferencesService _preferencesService;
    private readonly FeatureAccessChecker _accessChecker;

    public PreferencesApiController(UserPreferencesService preferencesService, FeatureAccessChecker accessChecker)
    {
        _preferencesService = preferencesService;
        _accessChecker = accessChecker;
    }

    [HttpGet]
    public IActionResult Get([FromQuery] string id)
    {
        if (!_accessChecker.IsFeatureEnabled(id))
            return Forbid();

        var username = HttpContext.User.Identity?.Name;
        if (string.IsNullOrEmpty(username))
            return Unauthorized();

        var json = _preferencesService.Get(username, id);
        if (json == null)
            return Ok(new { });

        return Content(json, "application/json");
    }

    [HttpPost]
    public async Task<IActionResult> Save([FromQuery] string id)
    {
        if (!_accessChecker.IsFeatureEnabled(id))
            return Forbid();

        var username = HttpContext.User.Identity?.Name;
        if (string.IsNullOrEmpty(username))
            return Unauthorized();

        using var reader = new StreamReader(Request.Body);
        var json = await reader.ReadToEndAsync();

        _preferencesService.Save(username, id, json);
        return Ok(new { success = true });
    }
}
