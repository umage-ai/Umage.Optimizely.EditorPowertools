using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using UmageAI.Optimizely.EditorPowerTools.Forms.Configuration;
using UmageAI.Optimizely.EditorPowerTools.Forms.Permissions;
using UmageAI.Optimizely.EditorPowerTools.Forms.Services;
using UmageAI.Optimizely.EditorPowerTools.Forms.Tools.FormsOverview.Models;
using UmageAI.Optimizely.EditorPowerTools.Infrastructure;

namespace UmageAI.Optimizely.EditorPowerTools.Forms.Tools.FormsOverview;

/// <summary>
/// JSON API for the Forms Overview and Submissions Timeline tools.
/// </summary>
[Authorize(Policy = "codeart:editorpowertools")]
[RequireAjax]
public class FormsApiController : Controller
{
    private readonly FormsAggregationService _service;
    private readonly FormsFeatureAccessChecker _accessChecker;
    private readonly SubmissionsBroadcaster _broadcaster;

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public FormsApiController(
        FormsAggregationService service,
        FormsFeatureAccessChecker accessChecker,
        SubmissionsBroadcaster broadcaster)
    {
        _service = service;
        _accessChecker = accessChecker;
        _broadcaster = broadcaster;
    }

    [HttpGet]
    public IActionResult GetForms()
    {
        if (!_accessChecker.HasAccess(HttpContext,
            nameof(FormsFeatureToggles.FormsOverview),
            EditorPowertoolsFormsPermissions.FormsOverview))
            return Forbid();

        var forms = _service.GetForms();
        return Ok(forms);
    }

    [HttpGet]
    public IActionResult GetSubmissionsTimeline(int top = 100, int days = 30, Guid? formGuid = null, bool includeData = false)
    {
        if (!_accessChecker.HasAccess(HttpContext,
            nameof(FormsFeatureToggles.SubmissionsTimeline),
            EditorPowertoolsFormsPermissions.SubmissionsTimeline))
            return Forbid();

        var events = _service.GetSubmissionsTimeline(top, days, formGuid, includeData);
        return Ok(events);
    }

    [HttpGet]
    public IActionResult GetFormChoices()
    {
        if (!_accessChecker.HasAccess(HttpContext,
            nameof(FormsFeatureToggles.SubmissionsTimeline),
            EditorPowertoolsFormsPermissions.SubmissionsTimeline))
            return Forbid();

        var choices = _service.GetFormChoices();
        return Ok(choices);
    }

    /// <summary>
    /// Server-Sent Events stream of new submissions. Browsers consume this
    /// directly via the native <c>EventSource</c>. Each new submission is sent
    /// as a single <c>data:</c> line with the SubmissionEventDto serialized as
    /// JSON. The connection stays open until the client disconnects or the
    /// app shuts down.
    /// </summary>
    [HttpGet]
    public async Task SubmissionsStream(CancellationToken ct)
    {
        if (!_accessChecker.HasAccess(HttpContext,
            nameof(FormsFeatureToggles.SubmissionsTimeline),
            EditorPowertoolsFormsPermissions.SubmissionsTimeline))
        {
            HttpContext.Response.StatusCode = StatusCodes.Status403Forbidden;
            return;
        }

        var resp = HttpContext.Response;
        resp.Headers["Content-Type"] = "text/event-stream";
        resp.Headers["Cache-Control"] = "no-cache";
        resp.Headers["X-Accel-Buffering"] = "no"; // tell reverse proxies not to buffer
        // Initial flush so the browser's onopen fires immediately.
        await resp.WriteAsync(": connected\n\n", ct);
        await resp.Body.FlushAsync(ct);

        var (reader, sub) = _broadcaster.Subscribe();
        try
        {
            // Send a comment heartbeat every ~25s so intermediaries don't drop
            // an idle connection. Run the heartbeat alongside the read loop.
            using var heartbeat = new CancellationTokenSource();
            var combined = CancellationTokenSource.CreateLinkedTokenSource(ct, heartbeat.Token);
            var heartbeatTask = SendHeartbeat(resp, combined.Token);

            await foreach (var ev in reader.ReadAllAsync(ct))
            {
                var json = JsonSerializer.Serialize(ev, _jsonOptions);
                await resp.WriteAsync($"event: submission\ndata: {json}\n\n", ct);
                await resp.Body.FlushAsync(ct);
            }

            heartbeat.Cancel();
            try { await heartbeatTask; } catch { }
        }
        catch (OperationCanceledException) { /* client disconnected — expected */ }
        finally
        {
            sub.Dispose();
        }
    }

    private static async Task SendHeartbeat(HttpResponse resp, CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromSeconds(25), ct);
                await resp.WriteAsync(": ping\n\n", ct);
                await resp.Body.FlushAsync(ct);
            }
        }
        catch { /* connection closed */ }
    }
}
