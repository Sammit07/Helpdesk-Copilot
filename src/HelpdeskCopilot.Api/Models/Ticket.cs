namespace HelpdeskCopilot.Api.Models;

public enum TicketStatus { Open, InProgress, Resolved, Closed }

public enum TicketPriority { Low, Medium, High, Critical }

public class Ticket
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public TicketStatus Status { get; set; } = TicketStatus.Open;
    public TicketPriority Priority { get; set; } = TicketPriority.Medium;
    public string AffectedService { get; set; } = string.Empty;
    public Guid? AlertId { get; set; }
    public string AiSummary { get; set; } = string.Empty;
    public string PossibleRootCause { get; set; } = string.Empty;
    public string RecommendedActions { get; set; } = string.Empty;
    public string? RelatedDocumentationLink { get; set; }
    public string AssignedTo { get; set; } = "Unassigned";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public List<TicketComment> Comments { get; set; } = new();
}

public class TicketComment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Author { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class AddCommentRequest
{
    public string Author { get; set; } = "Engineer";
    public string Content { get; set; } = string.Empty;
}
