using EditorPowertools.Configuration;
using EditorPowertools.Tools.ActiveEditors.Models;
using EPiServer.Notification;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;

namespace EditorPowertools.Tools.ActiveEditors;

[Authorize(Policy = "codeart:editorpowertools")]
public class ActiveEditorsHub : Hub
{
    private readonly ActiveEditorsService _service;
    private readonly IOptions<EditorPowertoolsOptions> _options;
    private readonly INotifier _notifier;

    public ActiveEditorsHub(
        ActiveEditorsService service,
        IOptions<EditorPowertoolsOptions> options,
        INotifier notifier)
    {
        _service = service;
        _options = options;
        _notifier = notifier;
    }

    public override async Task OnConnectedAsync()
    {
        if (!_options.Value.Features.ActiveEditors)
        {
            Context.Abort();
            return;
        }

        var username = Context.User?.Identity?.Name ?? "Unknown";
        _service.Connect(Context.ConnectionId, username, username);

        // Tell the client who they are (for self-filtering in the widget)
        await Clients.Caller.SendAsync("CurrentUser", username);

        await BroadcastPresence();
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _service.Disconnect(Context.ConnectionId);
        await BroadcastPresence();
        await base.OnDisconnectedAsync(exception);
    }

    public async Task UpdateContext(int? contentId, string? contentName, string action)
    {
        var previousEditors = contentId.HasValue
            ? _service.GetEditorsOnContent(contentId.Value, Context.ConnectionId)
            : new List<EditorPresenceDto>();

        _service.UpdateContext(Context.ConnectionId, contentId, contentName, action);

        if (contentId.HasValue && previousEditors.Count > 0)
        {
            await Clients.Caller.SendAsync("EditorsOnContent", previousEditors);
        }

        await BroadcastPresence();
    }

    public void Heartbeat()
    {
        _service.Heartbeat(Context.ConnectionId);
    }

    public async Task SendChat(string text)
    {
        if (!_options.Value.Features.ActiveEditorsChat) return;
        if (string.IsNullOrWhiteSpace(text)) return;
        if (text.Length > 500) text = text[..500];

        var username = Context.User?.Identity?.Name ?? "Unknown";
        _service.AddChatMessage(username, username, text);

        await Clients.All.SendAsync("ChatMessage", new ChatMessage
        {
            Username = username,
            DisplayName = username,
            Text = text,
            TimestampUtc = DateTime.UtcNow
        });
    }

    public List<ChatMessage> GetChatHistory()
    {
        if (!_options.Value.Features.ActiveEditorsChat)
            return new List<ChatMessage>();
        return _service.GetRecentMessages();
    }

    public List<string> GetEditorsToday()
    {
        return _service.GetEditorNamesToday();
    }

    /// <summary>
    /// Send a CMS notification to another editor via Optimizely's notification system.
    /// </summary>
    public async Task SendNotification(string recipientUsername, string message)
    {
        if (string.IsNullOrWhiteSpace(recipientUsername) || string.IsNullOrWhiteSpace(message)) return;
        if (message.Length > 500) message = message[..500];

        var senderUsername = Context.User?.Identity?.Name ?? "Unknown";

        var notification = new NotificationMessage
        {
            ChannelName = "epi.notifications.default",
            TypeName = "EditorPowertools.ActiveEditors",
            Subject = $"Message from {senderUsername}",
            Content = message,
            Sender = new NotificationUser(senderUsername),
            Recipients = new[] { new NotificationUser(recipientUsername) }
        };

        await _notifier.PostNotificationAsync(notification);
    }

    private async Task BroadcastPresence()
    {
        var editors = _service.GetAllEditors();
        await Clients.All.SendAsync("PresenceUpdate", new PresenceUpdate { Editors = editors });
    }
}
