using System.Threading.Channels;
using EPiServer.Core;
using EPiServer.Forms;
using EPiServer.Forms.Core.Events;
using EPiServer.Forms.Core.Models;
using EPiServer.Forms.Implementation.Elements;
using EPiServer.Shell;
using Microsoft.Extensions.Logging;
using UmageAI.Optimizely.EditorPowerTools.Forms.Tools.FormsOverview.Models;

namespace UmageAI.Optimizely.EditorPowerTools.Forms.Services;

/// <summary>
/// Subscribes to <see cref="FormsEvents"/> at startup and re-broadcasts new
/// submissions to anyone holding an open SSE channel — used by the live
/// Submissions Timeline view. One-way; SSE is the perfect fit for this
/// (clients can use the browser-native <c>EventSource</c>, no client lib).
/// </summary>
public sealed class SubmissionsBroadcaster : IDisposable
{
    private readonly ILogger<SubmissionsBroadcaster> _logger;
    private readonly object _lock = new();
    private readonly List<ChannelWriter<SubmissionEventDto>> _subscribers = new();
    private readonly EventHandler<FormsEventArgs> _stepHandler;
    private readonly EventHandler<FormsEventArgs> _finalizedHandler;
    private bool _hooked;

    public SubmissionsBroadcaster(ILogger<SubmissionsBroadcaster> logger)
    {
        _logger = logger;
        _stepHandler = (_, args) => OnSubmission(args, finalized: false);
        _finalizedHandler = (_, args) => OnSubmission(args, finalized: true);
        TryHook();
    }

    /// <summary>
    /// Returns a channel reader of submission events. The corresponding writer
    /// is registered with this broadcaster until the caller disposes the
    /// returned subscription, at which point it is removed and the channel
    /// is completed.
    /// </summary>
    public (ChannelReader<SubmissionEventDto> Reader, IDisposable Subscription) Subscribe()
    {
        var channel = Channel.CreateBounded<SubmissionEventDto>(new BoundedChannelOptions(64)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = false
        });
        lock (_lock) _subscribers.Add(channel.Writer);
        var sub = new Subscription(this, channel.Writer);
        return (channel.Reader, sub);
    }

    private void TryHook()
    {
        if (_hooked) return;
        try
        {
            FormsEvents.Instance.FormsStepSubmitted += _stepHandler;
            FormsEvents.Instance.FormsSubmissionFinalized += _finalizedHandler;
            _hooked = true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not subscribe to FormsEvents — live timeline will not receive updates.");
        }
    }

    private void OnSubmission(FormsEventArgs args, bool finalized)
    {
        try
        {
            if (args?.FormsContent is not IContent formContent) return;
            // EPiServer.Forms raises FormsSubmittedEventArgs (the derived type)
            // and puts the submission on its `SubmissionData` property — the
            // base class's `Data` property is never populated, so casting to
            // the base type and reading `Data` (as we previously did) silently
            // returned null and the broadcaster never published anything.
            var submission = args is EPiServer.Forms.Core.Events.FormsSubmittedEventArgs submitted
                ? submitted.SubmissionData
                : args.Data as Submission;
            if (submission == null) return;

            var dto = Map(formContent, submission, finalized);
            if (dto == null) return;

            ChannelWriter<SubmissionEventDto>[] copy;
            lock (_lock) copy = _subscribers.ToArray();

            foreach (var writer in copy)
            {
                try { writer.TryWrite(dto); }
                catch { /* dropping is fine — bounded channel will recycle */ }
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Submissions broadcaster: failed to publish event.");
        }
    }

    private static SubmissionEventDto? Map(IContent formContent, Submission s, bool finalized)
    {
        DateTime submitted;
        if (s.Data == null) return null;
        if (s.Data.TryGetValue(Constants.SYSTEMCOLUMN_SubmitTime, out var ts) && ts is DateTime dt)
        {
            submitted = dt.ToUniversalTime();
        }
        else
        {
            submitted = DateTime.UtcNow;
        }

        string? user = null;
        if (s.Data.TryGetValue(Constants.SYSTEMCOLUMN_SubmitUser, out var u)) user = u?.ToString();
        string? hosted = null;
        if (s.Data.TryGetValue(Constants.SYSTEMCOLUMN_HostedPage, out var h)) hosted = h?.ToString();
        string? lang = (formContent as ILocalizable)?.Language?.Name;
        if (s.Data.TryGetValue(Constants.SYSTEMCOLUMN_Language, out var l)) lang = l?.ToString() ?? lang;
        // Override finalized flag from event args — server told us authoritatively.
        var langSegment = string.IsNullOrEmpty(lang) ? string.Empty : $"&viewsetting=viewlanguage:///{lang}";

        return new SubmissionEventDto
        {
            SubmissionId = s.Id ?? string.Empty,
            FormGuid = formContent.ContentGuid,
            FormContentId = formContent.ContentLink?.ID ?? 0,
            FormName = formContent.Name ?? string.Empty,
            FormEditUrl = $"{Paths.ToResource("CMS", "")}#context=epi.cms.contentdata:///{formContent.ContentLink?.ID}{langSegment}",
            SubmissionViewUrl = $"{Paths.ToResource("CMS", "")}#context=epi.cms.contentdata:///{formContent.ContentLink?.ID}{langSegment}&viewsetting=viewname:///formsdataview",
            SubmittedUtc = submitted,
            SubmittedBy = user,
            HostedPageUrl = hosted,
            Language = lang,
            Finalized = finalized
        };
    }

    public void Dispose()
    {
        if (!_hooked) return;
        try
        {
            FormsEvents.Instance.FormsStepSubmitted -= _stepHandler;
            FormsEvents.Instance.FormsSubmissionFinalized -= _finalizedHandler;
        }
        catch { }
        ChannelWriter<SubmissionEventDto>[] copy;
        lock (_lock) { copy = _subscribers.ToArray(); _subscribers.Clear(); }
        foreach (var w in copy)
        {
            try { w.TryComplete(); } catch { }
        }
    }

    private sealed class Subscription : IDisposable
    {
        private readonly SubmissionsBroadcaster _owner;
        private ChannelWriter<SubmissionEventDto>? _writer;
        public Subscription(SubmissionsBroadcaster owner, ChannelWriter<SubmissionEventDto> writer)
        {
            _owner = owner;
            _writer = writer;
        }
        public void Dispose()
        {
            var w = Interlocked.Exchange(ref _writer, null);
            if (w == null) return;
            lock (_owner._lock) _owner._subscribers.Remove(w);
            try { w.TryComplete(); } catch { }
        }
    }
}
