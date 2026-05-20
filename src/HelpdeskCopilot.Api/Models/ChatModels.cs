namespace HelpdeskCopilot.Api.Models;

public enum MessageRole { User, Assistant, System }

public class ChatMessage
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string SessionId { get; set; } = string.Empty;
    public MessageRole Role { get; set; }
    public string Content { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public List<string> Sources { get; set; } = new();
}

public class ChatSession
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Title { get; set; } = "New Session";
    public List<ChatMessage> Messages { get; set; } = new();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Guid? RelatedAlertId { get; set; }
    public Guid? RelatedTicketId { get; set; }
}

public class ChatRequest
{
    public string SessionId { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public Guid? AlertId { get; set; }
}

public class ChatResponse
{
    public string SessionId { get; set; } = string.Empty;
    public string Response { get; set; } = string.Empty;
    public List<string> Sources { get; set; } = new();
    public List<string> SuggestedActions { get; set; } = new();
}

public class CreateSessionRequest
{
    public Guid? AlertId { get; set; }
    public string? Title { get; set; }
}
