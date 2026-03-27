using System.Collections.Concurrent;
using EditorPowertools.Tools.ContentImporter.Models;

namespace EditorPowertools.Tools.ContentImporter;

public class ImportSession
{
    public Guid SessionId { get; set; } = Guid.NewGuid();
    public string FileName { get; set; } = string.Empty;
    public string FileType { get; set; } = string.Empty;
    public List<string> Columns { get; set; } = new();
    public List<Dictionary<string, string>> Rows { get; set; } = new();
    public ImportMappingRequest? Mapping { get; set; }
    public ImportProgress? Progress { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class ImportSessionStore
{
    private readonly ConcurrentDictionary<Guid, ImportSession> _sessions = new();
    private const int MaxSessionAgeMinutes = 30;
    private const int MaxSessions = 10;

    public ImportSession Create()
    {
        Cleanup();
        var session = new ImportSession();
        _sessions[session.SessionId] = session;
        return session;
    }

    public ImportSession? Get(Guid sessionId)
    {
        return _sessions.TryGetValue(sessionId, out var session) ? session : null;
    }

    public bool Remove(Guid sessionId)
    {
        return _sessions.TryRemove(sessionId, out _);
    }

    private void Cleanup()
    {
        var cutoff = DateTime.UtcNow.AddMinutes(-MaxSessionAgeMinutes);
        foreach (var kvp in _sessions)
        {
            if (kvp.Value.CreatedAt < cutoff)
                _sessions.TryRemove(kvp.Key, out _);
        }

        // If still too many, remove oldest
        while (_sessions.Count >= MaxSessions)
        {
            var oldest = _sessions.OrderBy(x => x.Value.CreatedAt).FirstOrDefault();
            if (oldest.Key != Guid.Empty)
                _sessions.TryRemove(oldest.Key, out _);
        }
    }
}
