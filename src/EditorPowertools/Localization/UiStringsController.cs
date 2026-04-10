using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace UmageAI.Optimizely.EditorPowerTools.Localization;

[Authorize(Policy = "codeart:editorpowertools")]
public class UiStringsController : Controller
{
    private readonly UiStringsProvider _provider;

    public UiStringsController(UiStringsProvider provider)
    {
        _provider = provider;
    }

    [HttpGet]
    [Route("editorpowertools/api/ui-strings")]
    public IActionResult GetAll()
    {
        // Access is gated by [Authorize(Policy = "codeart:editorpowertools")].
        // No feature toggle applies here — strings are needed by all widgets.
        return Json(_provider.GetAll());
    }
}
