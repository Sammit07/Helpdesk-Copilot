using System.Collections.Concurrent;
using HelpdeskCopilot.Api.Models;

namespace HelpdeskCopilot.Api.Services;

/// <summary>
/// Singleton in-memory store for chat sessions.
/// Keeps sessions alive for the lifetime of the process; swap for a distributed cache
/// (Redis / Azure Cache) when scaling out or persisting across restarts.
/// </summary>
public class ChatSessionStore
{
    private readonly ConcurrentDictionary<string, ChatSession> _sessions = new();

    public ChatSession Create(Guid? alertId = null, string? title = null)
    {
        var session = new ChatSession
        {
            Id = Guid.NewGuid().ToString(),
            CreatedAt = DateTime.UtcNow,
            RelatedAlertId = alertId,
            Title = title ?? (alertId.HasValue ? "Alert Investigation" : "New Session")
        };
        _sessions[session.Id] = session;
        return session;
    }

    public ChatSession? Get(string sessionId) =>
        _sessions.TryGetValue(sessionId, out var s) ? s : null;

    public List<ChatSession> GetRecent(int limit) =>
        _sessions.Values
            .OrderByDescending(s => s.CreatedAt)
            .Take(limit)
            .ToList();

    public void Save(ChatSession session) =>
        _sessions[session.Id] = session;
}
