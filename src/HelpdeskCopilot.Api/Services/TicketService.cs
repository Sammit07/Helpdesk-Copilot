using HelpdeskCopilot.Api.Data;
using HelpdeskCopilot.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace HelpdeskCopilot.Api.Services;

public class TicketService(
    HelpdeskDbContext db,
    ILogger<TicketService> logger) : ITicketService
{
    public async Task<Ticket> CreateTicketAsync(Ticket ticket)
    {
        ticket.Id = Guid.NewGuid();
        ticket.CreatedAt = DateTime.UtcNow;
        ticket.Status = TicketStatus.Open;

        db.Tickets.Add(ticket);
        await db.SaveChangesAsync();
        logger.LogInformation("Ticket created: {TicketId} — {Title}", ticket.Id, ticket.Title);
        return ticket;
    }

    public async Task<Ticket> CreateTicketFromAlertAsync(Alert alert, string? aiSummary = null)
    {
        var priority = alert.Severity switch
        {
            AlertSeverity.Critical => TicketPriority.Critical,
            AlertSeverity.High => TicketPriority.High,
            AlertSeverity.Medium => TicketPriority.Medium,
            _ => TicketPriority.Low
        };

        var rootCause = InferRootCause(alert);
        var recommendedActions = InferRecommendedActions(alert);

        var ticket = new Ticket
        {
            Id = Guid.NewGuid(),
            Title = $"[{alert.Severity.ToString().ToUpper()}] {alert.Title}",
            Description = alert.Description,
            Status = TicketStatus.Open,
            Priority = priority,
            AffectedService = alert.AffectedService,
            AlertId = alert.Id,
            AiSummary = aiSummary ?? alert.AiAnalysis ?? $"Auto-generated from alert: {alert.Title}",
            PossibleRootCause = rootCause,
            RecommendedActions = recommendedActions,
            RelatedDocumentationLink = GetDocLink(alert.Type),
            CreatedAt = DateTime.UtcNow
        };

        db.Tickets.Add(ticket);
        await db.SaveChangesAsync();

        // Link alert to ticket
        alert.TicketId = ticket.Id;
        await db.SaveChangesAsync();

        logger.LogInformation("Ticket {TicketId} created from alert {AlertId}", ticket.Id, alert.Id);
        return ticket;
    }

    public async Task<List<Ticket>> GetTicketsAsync(TicketStatus? status = null, int limit = 50)
    {
        var query = db.Tickets.Include(t => t.Comments).AsQueryable();
        if (status.HasValue)
            query = query.Where(t => t.Status == status.Value);
        return await query
            .OrderByDescending(t => t.CreatedAt)
            .Take(limit)
            .ToListAsync();
    }

    public async Task<Ticket?> GetTicketAsync(Guid id) =>
        await db.Tickets.Include(t => t.Comments).FirstOrDefaultAsync(t => t.Id == id);

    public async Task<Ticket> UpdateTicketAsync(Guid id, Ticket updates)
    {
        var ticket = await db.Tickets.FindAsync(id)
            ?? throw new KeyNotFoundException($"Ticket {id} not found.");

        ticket.Title = updates.Title;
        ticket.Description = updates.Description;
        ticket.Status = updates.Status;
        ticket.Priority = updates.Priority;
        ticket.AssignedTo = updates.AssignedTo;
        ticket.AiSummary = updates.AiSummary;
        ticket.PossibleRootCause = updates.PossibleRootCause;
        ticket.RecommendedActions = updates.RecommendedActions;
        ticket.UpdatedAt = DateTime.UtcNow;

        if (updates.Status == TicketStatus.Resolved && ticket.ResolvedAt == null)
            ticket.ResolvedAt = DateTime.UtcNow;

        await db.SaveChangesAsync();
        return ticket;
    }

    public async Task<Ticket> AddCommentAsync(Guid id, TicketComment comment)
    {
        var ticket = await db.Tickets.Include(t => t.Comments).FirstOrDefaultAsync(t => t.Id == id)
            ?? throw new KeyNotFoundException($"Ticket {id} not found.");

        comment.Id = Guid.NewGuid();
        comment.CreatedAt = DateTime.UtcNow;
        ticket.Comments.Add(comment);
        ticket.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync();
        return ticket;
    }

    private static string InferRootCause(Alert alert) => alert.Type switch
    {
        AlertType.FailedRequests => "Likely caused by unhandled exception in request processing or downstream service failure. Database connection or dependency timeout is the most common root cause.",
        AlertType.HighCpuUsage => "Possible causes: sudden traffic increase, runaway background job, inefficient query executing repeatedly, or infinite loop introduced in recent deployment.",
        AlertType.DatabaseConnectionFailure => "Connection pool exhaustion due to improper connection disposal (missing using blocks) or under-resourced database unable to handle current load.",
        AlertType.SlowApiResponse => "Slow database queries (missing indexes, N+1 pattern), external API latency, or resource contention on the App Service tier.",
        AlertType.AppServiceUnavailable => "Application crash on startup (startup exception), failed deployment, or misconfigured health check endpoint.",
        AlertType.MemorySpike => "Memory leak in application code, unbounded static cache, or large in-memory data processing operation without streaming.",
        AlertType.LoginFailureSpike => "Possible credential stuffing or brute-force attack against authentication endpoint. May also indicate misconfigured client authentication.",
        _ => "Root cause under investigation. Review Application Insights and Azure Monitor for correlated events."
    };

    private static string InferRecommendedActions(Alert alert) => alert.Type switch
    {
        AlertType.FailedRequests => "1. Check Application Insights exceptions\n2. Verify downstream service availability\n3. Review recent deployments\n4. Restart App Service if errors are transient\n5. Implement circuit breaker for dependencies",
        AlertType.HighCpuUsage => "1. Scale out App Service (increase instance count)\n2. Run Application Insights Profiler\n3. Review recent code deployments\n4. Check for runaway background jobs\n5. Consider scale-up if load is persistent",
        AlertType.DatabaseConnectionFailure => "1. Scale up Azure SQL (increase DTUs)\n2. Audit connection disposal in code\n3. Add Max Pool Size to connection string\n4. Enable EF Core retry-on-failure\n5. Check for blocking queries in Query Performance Insight",
        AlertType.SlowApiResponse => "1. Run slow query analysis in Azure SQL\n2. Check for missing database indexes\n3. Profile API with Application Insights Profiler\n4. Review external dependency response times\n5. Enable response caching for read-heavy endpoints",
        AlertType.AppServiceUnavailable => "1. Check Kudu log stream for startup exceptions\n2. Review recent deployment history\n3. Verify all required App Settings are configured\n4. Restart App Service\n5. Roll back last deployment if issue is recent",
        AlertType.MemorySpike => "1. Restart instance to reclaim memory (temporary)\n2. Capture memory dump via Kudu console\n3. Enable Application Insights memory profiling\n4. Audit static collections for unbounded growth\n5. Scale up to tier with more RAM",
        AlertType.LoginFailureSpike => "1. Check Azure AD Sign-in logs for attack patterns\n2. Block suspicious IPs at WAF/Front Door\n3. Enable Identity Protection risk policies\n4. Notify security team\n5. Force MFA for affected accounts",
        _ => "1. Review Application Insights for errors\n2. Check Azure Monitor metrics\n3. Consult relevant runbook\n4. Escalate if not resolved within SLA"
    };

    private static string? GetDocLink(AlertType type) => type switch
    {
        AlertType.FailedRequests => "/knowledge/kb-001",
        AlertType.DatabaseConnectionFailure => "/knowledge/kb-002",
        AlertType.HighCpuUsage => "/knowledge/kb-003",
        AlertType.AppServiceUnavailable => "/knowledge/kb-001",
        AlertType.MemorySpike => "/knowledge/kb-005",
        AlertType.LoginFailureSpike => "/knowledge/kb-006",
        _ => null
    };
}
