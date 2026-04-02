using System.Collections.Concurrent;
using EditorPowertools.Tools.ActiveEditors.Models;

namespace EditorPowertools.Tools.ActiveEditors;

public class ActiveEditorsService
{
    private readonly ConcurrentDictionary<string, EditorPresence> _editors = new();
    private readonly ConcurrentQueue<ChatMessage> _chatHistory = new();
    private readonly ConcurrentDictionary<string, byte> _editorsToday = new();
    private readonly ConcurrentDictionary<string, DateTime> _lastChatTime = new();
    private readonly ConcurrentDictionary<string, DateTime> _lastNotifyTime = new();
    private readonly ConcurrentDictionary<string, DateTime> _lastHeartbeatTime = new();
    private DateTime _todayDate = DateTime.UtcNow.Date;
    private const int MaxChatHistory = 100;
    private static readonly TimeSpan StaleTimeout = TimeSpan.FromSeconds(90);

    public void Connect(string connectionId, string username, string displayName)
    {
        var now = DateTime.UtcNow;
        ResetTodayIfNeeded(now);
        _editors[connectionId] = new EditorPresence
        {
            ConnectionId = connectionId,
            Username = username,
            DisplayName = displayName,
            LastSeen = now,
            ConnectedAt = now
        };
        _editorsToday.TryAdd(username.ToLowerInvariant(), 0);
    }

    public void Disconnect(string connectionId)
    {
        _editors.TryRemove(connectionId, out _);
        _lastChatTime.TryRemove(connectionId, out _);
        _lastNotifyTime.TryRemove(connectionId, out _);
        _lastHeartbeatTime.TryRemove(connectionId, out _);
    }

    public bool IsRateLimited(ConcurrentDictionary<string, DateTime> tracker, string connectionId, TimeSpan cooldown)
    {
        var now = DateTime.UtcNow;
        if (tracker.TryGetValue(connectionId, out var lastTime) && (now - lastTime) < cooldown)
            return true;
        tracker[connectionId] = now;
        return false;
    }

    public bool IsChatRateLimited(string connectionId) =>
        IsRateLimited(_lastChatTime, connectionId, TimeSpan.FromSeconds(2));

    public bool IsNotifyRateLimited(string connectionId) =>
        IsRateLimited(_lastNotifyTime, connectionId, TimeSpan.FromSeconds(5));

    public bool IsHeartbeatRateLimited(string connectionId) =>
        IsRateLimited(_lastHeartbeatTime, connectionId, TimeSpan.FromSeconds(10));

    public void UpdateContext(string connectionId, int? contentId, string? contentName, string action)
    {
        if (_editors.TryGetValue(connectionId, out var editor))
        {
            editor.ContentId = contentId;
            editor.ContentName = contentName;
            editor.Action = action;
            editor.LastSeen = DateTime.UtcNow;
        }
    }

    public void Heartbeat(string connectionId)
    {
        if (_editors.TryGetValue(connectionId, out var editor))
            editor.LastSeen = DateTime.UtcNow;
    }

    public List<EditorPresenceDto> GetEditorsOnContent(int contentId, string? excludeConnectionId = null)
    {
        CleanupStale();
        return _editors.Values
            .Where(e => e.ContentId == contentId && e.ConnectionId != excludeConnectionId)
            .Select(ToDto)
            .ToList();
    }

    public List<EditorPresenceDto> GetAllEditors()
    {
        CleanupStale();
        return _editors.Values.Select(ToDto).ToList();
    }

    public List<string> GetEditorNamesToday()
    {
        ResetTodayIfNeeded(DateTime.UtcNow);
        return _editorsToday.Keys.ToList();
    }

    public void AddChatMessage(string username, string displayName, string text)
    {
        _chatHistory.Enqueue(new ChatMessage
        {
            Username = username,
            DisplayName = displayName,
            Text = text,
            TimestampUtc = DateTime.UtcNow
        });
        while (_chatHistory.Count > MaxChatHistory)
            _chatHistory.TryDequeue(out _);
    }

    public List<ChatMessage> GetRecentMessages()
    {
        return _chatHistory.ToList();
    }

    private void CleanupStale()
    {
        var cutoff = DateTime.UtcNow - StaleTimeout;
        foreach (var key in _editors.Where(kvp => kvp.Value.LastSeen < cutoff).Select(kvp => kvp.Key).ToList())
            _editors.TryRemove(key, out _);
    }

    private void ResetTodayIfNeeded(DateTime now)
    {
        var today = now.Date;
        if (today > _todayDate)
        {
            _todayDate = today;
            _editorsToday.Clear();
            foreach (var editor in _editors.Values)
                _editorsToday.TryAdd(editor.Username.ToLowerInvariant(), 0);
        }
    }

    private static EditorPresenceDto ToDto(EditorPresence e) => new()
    {
        Username = e.Username,
        DisplayName = e.DisplayName,
        ContentId = e.ContentId,
        ContentName = e.ContentName,
        Action = e.Action,
        ConnectedAt = e.ConnectedAt
    };
}
