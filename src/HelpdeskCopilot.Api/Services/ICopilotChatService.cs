using HelpdeskCopilot.Api.Models;

namespace HelpdeskCopilot.Api.Services;

public interface ICopilotChatService
{
    Task<ChatResponse> ChatAsync(ChatRequest request);
    Task<ChatSession?> GetSessionAsync(string sessionId);
    Task<ChatSession> CreateSessionAsync(Guid? alertId = null, string? title = null);
    Task<List<ChatSession>> GetRecentSessionsAsync(int limit = 10);
}
