using AssistantApi.Models.Domain;

namespace AssistantApi.Services.Interfaces;

public interface IChatSessionStore
{
    ChatSessionState CreateSession(string? title = null);
    ChatSessionState GetOrCreate(string sessionId);
    bool TryGet(string sessionId, out ChatSessionState? session);
    IReadOnlyList<ChatSessionState> ListSessions();
    bool RemoveSession(string sessionId);
    ChatSessionState Upsert(string sessionId, Action<ChatSessionState> update);
}
