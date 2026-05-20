namespace HelpdeskCopilot.Api.Models;

public class LogAnalysisResult
{
    public string Query { get; set; } = string.Empty;
    public List<LogEntry> Entries { get; set; } = new();
    public string Summary { get; set; } = string.Empty;
    public DateTime AnalyzedAt { get; set; } = DateTime.UtcNow;
    public string TimeRange { get; set; } = "30m";
    public int TotalEvents { get; set; }
}

public class LogEntry
{
    public DateTime Timestamp { get; set; }
    public string Level { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public string? ExceptionType { get; set; }
    public int Count { get; set; }
    public Dictionary<string, string> Properties { get; set; } = new();
}

public class KnowledgeDocument
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public List<string> Tags { get; set; } = new();
    public DateTime IndexedAt { get; set; } = DateTime.UtcNow;
    public double? Score { get; set; }
}

public class Notification
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Channel { get; set; } = "Dashboard";
    public string Recipient { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public bool IsRead { get; set; }
    public DateTime SentAt { get; set; } = DateTime.UtcNow;
    public NotificationSeverity Severity { get; set; }
}

public enum NotificationSeverity { Info, Warning, Critical }

public class KqlQueryRequest
{
    public string Query { get; set; } = string.Empty;
    public string TimeRange { get; set; } = "30m";
}

public class KnowledgeSearchRequest
{
    public string Query { get; set; } = string.Empty;
    public int TopK { get; set; } = 3;
}
