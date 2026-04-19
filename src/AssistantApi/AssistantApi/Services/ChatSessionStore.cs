using System.Collections.Concurrent;
using AssistantApi.Models.Domain;
using AssistantApi.Services.Interfaces;

namespace AssistantApi.Services;

public class ChatSessionStore : IChatSessionStore
{
    private readonly ConcurrentDictionary<string, ChatSessionState> _sessions = new(StringComparer.Ordinal);

    public ChatSessionState CreateSession(string? title = null)
    {
        var session = new ChatSessionState
        {
            SessionId = Guid.NewGuid().ToString("N"),
            Title = string.IsNullOrWhiteSpace(title) ? "Новый чат" : title.Trim(),
            Status = "active",
            UpdatedAtUtc = DateTime.UtcNow,
            IsTitleFinalized = !string.IsNullOrWhiteSpace(title)
        };

        _sessions[session.SessionId] = session;
        return session;
    }

    public ChatSessionState GetOrCreate(string sessionId)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            return CreateSession();
        }

        return _sessions.GetOrAdd(sessionId, id => new ChatSessionState
        {
            SessionId = id,
            Title = "Новый чат",
            Status = "active",
            UpdatedAtUtc = DateTime.UtcNow,
            IsTitleFinalized = false
        });
    }

    public bool TryGet(string sessionId, out ChatSessionState? session)
    {
        var found = _sessions.TryGetValue(sessionId, out var stored);
        session = stored;
        return found;
    }

    public IReadOnlyList<ChatSessionState> ListSessions()
    {
        return _sessions.Values
            .OrderByDescending(s => s.UpdatedAtUtc)
            .ToList();
    }

    public ChatSessionState Upsert(string sessionId, Action<ChatSessionState> update)
    {
        var session = GetOrCreate(sessionId);
        lock (session.SyncRoot)
        {
            update(session);
        }

        return session;
    }
}
