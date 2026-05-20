namespace HelpdeskCopilot.Api.Models;

public enum AlertType
{
    HighCpuUsage,
    FailedRequests,
    SlowApiResponse,
    AppServiceUnavailable,
    DatabaseConnectionFailure,
    MemorySpike,
    LoginFailureSpike
}

public enum AlertSeverity { Low, Medium, High, Critical }

public enum AlertStatus { New, Acknowledged, InProgress, Resolved }

public class Alert
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public AlertType Type { get; set; }
    public AlertSeverity Severity { get; set; }
    public AlertStatus Status { get; set; } = AlertStatus.New;
    public string AffectedService { get; set; } = string.Empty;
    public string AffectedResource { get; set; } = string.Empty;
    public DateTime TriggeredAt { get; set; } = DateTime.UtcNow;
    public DateTime? ResolvedAt { get; set; }
    public Dictionary<string, string> Metadata { get; set; } = new();
    public string? AiAnalysis { get; set; }
    public Guid? TicketId { get; set; }
}

public class AlertStatusUpdateRequest
{
    public AlertStatus Status { get; set; }
}

public class MockAlertRequest
{
    public AlertType Type { get; set; } = AlertType.FailedRequests;
}
