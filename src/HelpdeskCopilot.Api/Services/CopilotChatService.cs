using Azure.AI.OpenAI;
using HelpdeskCopilot.Api.Models;
using Microsoft.Extensions.Configuration;
using OAIChatMessage = OpenAI.Chat.ChatMessage;

namespace HelpdeskCopilot.Api.Services;

public class CopilotChatService(
    ChatSessionStore sessionStore,
    IRagService ragService,
    IAlertIngestionService alertService,
    IConfiguration config,
    ILogger<CopilotChatService> logger) : ICopilotChatService
{
    private readonly string? _endpoint = config["AzureOpenAI:Endpoint"];
    private readonly string? _apiKey = config["AzureOpenAI:ApiKey"];
    private readonly string _deployment = config["AzureOpenAI:DeploymentName"] ?? "gpt-4o";

    public Task<ChatSession> CreateSessionAsync(Guid? alertId = null, string? title = null) =>
        Task.FromResult(sessionStore.Create(alertId, title));

    public Task<ChatSession?> GetSessionAsync(string sessionId) =>
        Task.FromResult(sessionStore.Get(sessionId));

    public Task<List<ChatSession>> GetRecentSessionsAsync(int limit = 10) =>
        Task.FromResult(sessionStore.GetRecent(limit));

    public async Task<ChatResponse> ChatAsync(ChatRequest request)
    {
        if (string.IsNullOrEmpty(request.SessionId))
            request.SessionId = (await CreateSessionAsync(request.AlertId)).Id;

        var session = sessionStore.Get(request.SessionId)
            ?? throw new KeyNotFoundException($"Session {request.SessionId} not found.");

        var docs = await ragService.SearchAsync(request.Message, topK: 3);

        Alert? alert = null;
        if (request.AlertId.HasValue)
            alert = await alertService.GetAlertAsync(request.AlertId.Value);

        var sources = docs.Select(d => d.Title).ToList();

        string responseText;
        if (!string.IsNullOrEmpty(_endpoint) && !string.IsNullOrEmpty(_apiKey))
        {
            responseText = await GetAzureOpenAiResponseAsync(request.Message, session, docs, alert);
        }
        else
        {
            logger.LogDebug("Azure OpenAI not configured — using rule-based responses");
            responseText = GenerateRuleBasedResponse(request.Message, docs, alert);
        }

        var suggestedActions = ExtractSuggestedActions(request.Message, alert);

        session.Messages.Add(new ChatMessage
        {
            Id = Guid.NewGuid(),
            SessionId = session.Id,
            Role = MessageRole.User,
            Content = request.Message,
            Timestamp = DateTime.UtcNow
        });
        session.Messages.Add(new ChatMessage
        {
            Id = Guid.NewGuid(),
            SessionId = session.Id,
            Role = MessageRole.Assistant,
            Content = responseText,
            Sources = sources,
            Timestamp = DateTime.UtcNow
        });

        if (session.Title is "New Session" or "Alert Investigation" && request.Message.Length > 10)
            session.Title = request.Message[..Math.Min(48, request.Message.Length)] + "…";

        sessionStore.Save(session);

        return new ChatResponse
        {
            SessionId = session.Id,
            Response = responseText,
            Sources = sources,
            SuggestedActions = suggestedActions
        };
    }

    private async Task<string> GetAzureOpenAiResponseAsync(
        string userMessage,
        ChatSession session,
        List<KnowledgeDocument> docs,
        Alert? alert)
    {
        var client = new AzureOpenAIClient(new Uri(_endpoint!), new Azure.AzureKeyCredential(_apiKey!));
        var chatClient = client.GetChatClient(_deployment);

        var systemPrompt = BuildSystemPrompt(docs, alert);
        var messages = new List<OAIChatMessage>
        {
            new OpenAI.Chat.SystemChatMessage(systemPrompt)
        };

        foreach (var msg in session.Messages.TakeLast(10))
        {
            messages.Add(msg.Role == MessageRole.User
                ? new OpenAI.Chat.UserChatMessage(msg.Content)
                : new OpenAI.Chat.AssistantChatMessage(msg.Content));
        }

        messages.Add(new OpenAI.Chat.UserChatMessage(userMessage));

        var completion = await chatClient.CompleteChatAsync(messages);
        return completion.Value.Content[0].Text;
    }

    private static string BuildSystemPrompt(List<KnowledgeDocument> docs, Alert? alert)
    {
        var prompt = """
            You are an Azure Helpdesk Copilot — an expert cloud support assistant for a managed services team.
            You help support engineers diagnose Azure infrastructure issues, interpret monitoring alerts, and create incident tickets.
            Be concise, technical, and actionable. Use bullet points for steps. Always suggest specific Azure CLI commands or KQL queries when relevant.
            """;

        if (alert != null)
        {
            prompt += $"\n\nCURRENT ALERT CONTEXT:\nTitle: {alert.Title}\nService: {alert.AffectedService}\nSeverity: {alert.Severity}\nDescription: {alert.Description}";
            if (!string.IsNullOrEmpty(alert.AiAnalysis))
                prompt += $"\n\nPrevious AI Analysis:\n{alert.AiAnalysis}";
        }

        if (docs.Count > 0)
        {
            prompt += "\n\nRELEVANT KNOWLEDGE BASE ARTICLES:\n";
            foreach (var doc in docs)
                prompt += $"\n### {doc.Title}\n{doc.Content[..Math.Min(800, doc.Content.Length)]}\n";
        }

        return prompt;
    }

    private static string GenerateRuleBasedResponse(string message, List<KnowledgeDocument> docs, Alert? alert)
    {
        var msg = message.ToLowerInvariant();

        if (docs.Count > 0)
        {
            var topDoc = docs.First();
            return $"Based on our knowledge base, I found relevant guidance:\n\n" +
                   $"**{topDoc.Title}**\n\n" +
                   $"{topDoc.Content[..Math.Min(500, topDoc.Content.Length)]}...\n\n" +
                   $"*{docs.Count} related article(s) found. Connect Azure OpenAI in appsettings.json for full AI responses.*";
        }

        if (alert != null)
            return $"I'm analyzing the alert: **{alert.Title}** on **{alert.AffectedService}**.\n\n" +
                   $"Severity: {alert.Severity}\n\n" +
                   $"{(alert.AiAnalysis ?? "Run AI analysis on this alert for detailed recommendations.")}";

        if (msg.Contains("ticket"))
            return "I can help create an incident ticket. Use the **Create Ticket** button on the Alerts page, or provide the alert ID and I'll generate a ticket summary.";

        if (msg.Contains("cpu") || msg.Contains("memory") || msg.Contains("performance"))
            return "For performance issues, I recommend:\n1. Check Application Insights Performance blade\n2. Review recent deployments\n3. Run profiler traces\n4. Scale out if load has genuinely increased";

        if (msg.Contains("database") || msg.Contains("sql") || msg.Contains("connection"))
            return "For database connectivity issues:\n1. Verify connection strings in App Settings\n2. Check SQL Server firewall rules\n3. Review connection pool settings\n4. Check for blocking queries in Query Performance Insight";

        return "I'm your Azure Helpdesk Copilot. I can help you:\n- **Analyze alerts** — describe the issue and I'll provide guidance\n- **Search knowledge base** — ask about specific error types or services\n- **Generate ticket summaries** — describe the incident\n- **Suggest fixes** — based on similar past incidents\n\n*Tip: Connect Azure OpenAI in appsettings.json for full AI capabilities.*";
    }

    private static List<string> ExtractSuggestedActions(string message, Alert? alert)
    {
        var actions = new List<string>();

        if (alert != null)
        {
            actions.Add("View related logs");
            actions.Add("Create support ticket");
            if (alert.Status == AlertStatus.New)
                actions.Add("Acknowledge alert");
        }

        var msg = message.ToLowerInvariant();
        if (msg.Contains("scale") || msg.Contains("cpu") || msg.Contains("performance"))
            actions.Add("Scale out App Service");
        if (msg.Contains("restart") || msg.Contains("unavailable"))
            actions.Add("Restart App Service");
        if (msg.Contains("ticket") || msg.Contains("incident"))
            actions.Add("Create incident ticket");

        return actions.Distinct().Take(3).ToList();
    }
}
