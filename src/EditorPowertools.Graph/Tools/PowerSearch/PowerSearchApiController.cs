using System.Globalization;
using EPiServer.Framework.Localization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using UmageAI.Optimizely.EditorPowerTools.Graph.Abstractions;
using UmageAI.Optimizely.EditorPowerTools.Graph.Configuration;
using UmageAI.Optimizely.EditorPowerTools.Graph.Permissions;
using UmageAI.Optimizely.EditorPowerTools.Infrastructure;

namespace UmageAI.Optimizely.EditorPowerTools.Graph.Tools.PowerSearch;

/// <summary>JSON API behind the PowerSearch page.</summary>
[Authorize(Policy = "codeart:editorpowertools")]
[RequireAjax]
public class PowerSearchApiController : Controller
{
    private const int MaxLanguages = 20;

    private readonly IPowerSearchService _service;
    private readonly IGraphSearchService _graph;
    private readonly GraphFeatureAccessChecker _accessChecker;
    private readonly LocalizationService _localization;
    private readonly ILogger<PowerSearchApiController> _logger;

    public PowerSearchApiController(
        IPowerSearchService service,
        IGraphSearchService graph,
        GraphFeatureAccessChecker accessChecker,
        LocalizationService localization,
        ILogger<PowerSearchApiController> logger)
    {
        _service = service;
        _graph = graph;
        _accessChecker = accessChecker;
        _localization = localization;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Search(
        string? q,
        PowerSearchTab tab = PowerSearchTab.Pages,
        int take = 0,
        int skip = 0,
        [FromQuery(Name = "type")] string[]? type = null,
        [FromQuery(Name = "lang")] string[]? lang = null,
        [FromQuery(Name = "section")] string[]? section = null,
        CancellationToken ct = default)
    {
        if (!_accessChecker.HasAccess(HttpContext,
            nameof(GraphFeatureToggles.PowerSearch),
            EditorPowertoolsGraphPermissions.PowerSearch))
            return Forbid();

        try
        {
            var sections = (section ?? Array.Empty<string>())
                .Select(s => Guid.TryParse(s, out var g) ? g : Guid.Empty)
                .Where(g => g != Guid.Empty)
                .Distinct()
                .ToList();

            var types = _graph.ValidateContentTypes(type);
            var languages = ValidLanguages(lang);

            var result = await _service.SearchAsync(
                new PowerSearchQuery(q, tab, take, skip, types, languages, sections), ct);
            return Ok(result);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "PowerSearch request failed.");
            return StatusCode(500, new
            {
                error = _localization.GetStringByCulture(
                    "/editorpowertools/graph/powersearch/error", "Search failed.", CultureInfo.CurrentUICulture)
            });
        }
    }

    private static IReadOnlyList<string> ValidLanguages(IEnumerable<string>? names)
    {
        var list = new List<string>();
        foreach (var name in names ?? Array.Empty<string>())
        {
            if (list.Count >= MaxLanguages) break;
            if (string.IsNullOrWhiteSpace(name)) continue;
            try
            {
                CultureInfo.GetCultureInfo(name);
            }
            catch (CultureNotFoundException)
            {
                continue;
            }

            if (!list.Contains(name, StringComparer.OrdinalIgnoreCase)) list.Add(name);
        }

        return list;
    }
}
