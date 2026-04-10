namespace UmageAI.Optimizely.EditorPowerTools.Tools.ActiveEditors.Models;

public class EditorPresence
{
    public string ConnectionId { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public int? ContentId { get; set; }
    public string? ContentName { get; set; }
    public string Action { get; set; } = "idle";
    public DateTime LastSeen { get; set; }
    public DateTime ConnectedAt { get; set; }
}

public class ChatMessage
{
    public string Username { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public DateTime TimestampUtc { get; set; }
}

public class PresenceUpdate
{
    public List<EditorPresenceDto> Editors { get; set; } = new();
}

public class EditorPresenceDto
{
    public string Username { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public int? ContentId { get; set; }
    public string? ContentName { get; set; }
    public string Action { get; set; } = "idle";
    public DateTime ConnectedAt { get; set; }
}
