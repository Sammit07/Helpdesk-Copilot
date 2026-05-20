using HelpdeskCopilot.Api.Models;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace HelpdeskCopilot.Web.Services;

public class ApiClient(HttpClient http)
{
    private static readonly JsonSerializerOptions _json = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    // ── Alerts ─────────────────────────────────────────────────────────────
    public Task<List<Alert>?> GetAlertsAsync(AlertStatus? status = null, int limit = 50)
    {
        var url = $"api/alerts?limit={limit}";
        if (status.HasValue) url += $"&status={status}";
        return http.GetFromJsonAsync<List<Alert>>(url, _json);
    }

    public Task<Alert?> GetAlertAsync(Guid id) =>
        http.GetFromJsonAsync<Alert>($"api/alerts/{id}", _json);

    public async Task<Alert?> IngestAlertAsync(Alert alert)
    {
        var resp = await http.PostAsJsonAsync("api/alerts", alert, _json);
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<Alert>(_json);
    }

    public async Task<Alert?> GenerateMockAlertAsync(AlertType type)
    {
        var resp = await http.PostAsJsonAsync("api/alerts/mock", new { type }, _json);
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<Alert>(_json);
    }

    public async Task<object?> AnalyzeAlertAsync(Guid id)
    {
        var resp = await http.PostAsync($"api/alerts/{id}/analyze", null);
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<object>(_json);
    }

    public async Task<Ticket?> CreateTicketFromAlertAsync(Guid alertId)
    {
        var resp = await http.PostAsync($"api/alerts/{alertId}/create-ticket", null);
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<Ticket>(_json);
    }

    public async Task<LogAnalysisResult?> GetAlertLogsAsync(Guid alertId)
    {
        var resp = await http.GetAsync($"api/alerts/{alertId}/logs");
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<LogAnalysisResult>(_json);
    }

    public async Task UpdateAlertStatusAsync(Guid id, AlertStatus status)
    {
        var resp = await http.PutAsJsonAsync($"api/alerts/{id}/status", new { status }, _json);
        resp.EnsureSuccessStatusCode();
    }

    // ── Tickets ────────────────────────────────────────────────────────────
    public Task<List<Ticket>?> GetTicketsAsync(TicketStatus? status = null, int limit = 50)
    {
        var url = $"api/tickets?limit={limit}";
        if (status.HasValue) url += $"&status={status}";
        return http.GetFromJsonAsync<List<Ticket>>(url, _json);
    }

    public Task<Ticket?> GetTicketAsync(Guid id) =>
        http.GetFromJsonAsync<Ticket>($"api/tickets/{id}", _json);

    public async Task<Ticket?> UpdateTicketAsync(Guid id, Ticket ticket)
    {
        var resp = await http.PutAsJsonAsync($"api/tickets/{id}", ticket, _json);
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<Ticket>(_json);
    }

    public async Task<Ticket?> AddCommentAsync(Guid ticketId, string author, string content)
    {
        var resp = await http.PostAsJsonAsync($"api/tickets/{ticketId}/comments",
            new { author, content }, _json);
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<Ticket>(_json);
    }

    public async Task<Ticket?> ResolveTicketAsync(Guid id)
    {
        var resp = await http.PutAsync($"api/tickets/{id}/resolve", null);
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<Ticket>(_json);
    }

    // ── Chat ───────────────────────────────────────────────────────────────
    public async Task<ChatResponse?> SendMessageAsync(ChatRequest request)
    {
        var resp = await http.PostAsJsonAsync("api/chat/message", request, _json);
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<ChatResponse>(_json);
    }

    public async Task<ChatSession?> CreateChatSessionAsync(Guid? alertId = null, string? title = null)
    {
        var resp = await http.PostAsJsonAsync("api/chat/sessions",
            new { alertId, title }, _json);
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<ChatSession>(_json);
    }

    public Task<List<ChatSession>?> GetChatSessionsAsync(int limit = 10) =>
        http.GetFromJsonAsync<List<ChatSession>>($"api/chat/sessions?limit={limit}", _json);

    public Task<ChatSession?> GetChatSessionAsync(string sessionId) =>
        http.GetFromJsonAsync<ChatSession>($"api/chat/sessions/{sessionId}", _json);

    // ── Notifications ──────────────────────────────────────────────────────
    public Task<List<Notification>?> GetNotificationsAsync(bool unreadOnly = false, int limit = 20) =>
        http.GetFromJsonAsync<List<Notification>>($"api/notifications?unreadOnly={unreadOnly}&limit={limit}", _json);

    public async Task MarkNotificationReadAsync(Guid id)
    {
        await http.PutAsync($"api/notifications/{id}/read", null);
    }

    public async Task MarkAllNotificationsReadAsync()
    {
        await http.PutAsync("api/notifications/read-all", null);
    }

    // ── Knowledge ──────────────────────────────────────────────────────────
    public Task<List<KnowledgeDocument>?> GetKnowledgeDocumentsAsync() =>
        http.GetFromJsonAsync<List<KnowledgeDocument>>("api/knowledge", _json);

    public async Task<List<KnowledgeDocument>?> SearchKnowledgeAsync(string query, int topK = 3)
    {
        var resp = await http.PostAsJsonAsync("api/knowledge/search",
            new { query, topK }, _json);
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<List<KnowledgeDocument>>(_json);
    }

    // ── Logs ───────────────────────────────────────────────────────────────
    public Task<LogAnalysisResult?> GetServiceLogsAsync(string serviceName, int minutes = 30) =>
        http.GetFromJsonAsync<LogAnalysisResult>($"api/logs/service/{serviceName}?minutes={minutes}", _json);
}
