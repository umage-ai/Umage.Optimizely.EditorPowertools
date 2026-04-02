using EditorPowertools.Permissions;
using EditorPowertools.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EditorPowertools.Components;

/// <summary>
/// API endpoints for per-user tool preferences. Shared across all tools.
/// </summary>
[Authorize(Policy = "codeart:editorpowertools")]
[Route("editorpowertools/api/preferences")]
public class PreferencesApiController : Controller
{
    private readonly UserPreferencesService _preferencesService;
    private readonly FeatureAccessChecker _accessChecker;

    public PreferencesApiController(UserPreferencesService preferencesService, FeatureAccessChecker accessChecker)
    {
        _preferencesService = preferencesService;
        _accessChecker = accessChecker;
    }

    [HttpGet("{toolName}")]
    public IActionResult Get(string toolName)
    {
        if (!_accessChecker.IsFeatureEnabled(toolName))
            return Forbid();

        var username = HttpContext.User.Identity?.Name;
        if (string.IsNullOrEmpty(username))
            return Unauthorized();

        var json = _preferencesService.Get(username, toolName);
        if (json == null)
            return Ok(new { });

        return Content(json, "application/json");
    }

    [HttpPost("{toolName}")]
    public async Task<IActionResult> Save(string toolName)
    {
        if (!_accessChecker.IsFeatureEnabled(toolName))
            return Forbid();

        var username = HttpContext.User.Identity?.Name;
        if (string.IsNullOrEmpty(username))
            return Unauthorized();

        using var reader = new StreamReader(Request.Body);
        var json = await reader.ReadToEndAsync();

        _preferencesService.Save(username, toolName, json);
        return Ok(new { success = true });
    }
}
