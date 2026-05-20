using HelpdeskCopilot.Api.Data;
using HelpdeskCopilot.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace HelpdeskCopilot.Api.Services;

public class AlertIngestionService(
    HelpdeskDbContext db,
    ILogger<AlertIngestionService> logger) : IAlertIngestionService
{
    private static readonly Dictionary<AlertType, (string title, string description, AlertSeverity severity, string service, string resource)> _alertTemplates = new()
    {
        [AlertType.HighCpuUsage] = ("High CPU Usage Detected", "CPU utilization exceeded 90% for over 5 minutes on the target resource.", AlertSeverity.High, "App Service", "app-service-prod-01"),
        [AlertType.FailedRequests] = ("Elevated HTTP 5xx Error Rate", "HTTP 500 error rate exceeded 10% — 247 failed requests in the last 15 minutes from the payment API.", AlertSeverity.Critical, "Payment API", "payment-api-prod"),
        [AlertType.SlowApiResponse] = ("API Response Time Degraded", "Average response time exceeded 3000ms (P95: 5.2s). SLA threshold is 800ms.", AlertSeverity.High, "Order Service", "order-service-prod"),
        [AlertType.AppServiceUnavailable] = ("App Service Health Check Failing", "Azure App Service health endpoint returning non-2xx responses. Service appears unavailable.", AlertSeverity.Critical, "Web App", "webapp-frontend-prod"),
        [AlertType.DatabaseConnectionFailure] = ("Database Connection Pool Exhausted", "SQL connection pool exhausted. Requests queuing — 42 timeouts in last 5 minutes.", AlertSeverity.Critical, "Azure SQL", "sql-server-prod/HelpdeskDb"),
        [AlertType.MemorySpike] = ("Memory Pressure Detected", "Working set memory exceeded 85% of available RAM. GC pressure increasing, potential OOM condition.", AlertSeverity.High, "App Service", "app-service-prod-01"),
        [AlertType.LoginFailureSpike] = ("Login Failure Rate Spike", "Authentication failures increased 300% over baseline. Possible credential stuffing attack.", AlertSeverity.Critical, "Auth Service", "auth-service-prod")
    };

    public Alert GenerateMockAlert(AlertType type)
    {
        var (title, description, severity, service, resource) = _alertTemplates[type];
        return new Alert
        {
            Title = title,
            Description = description,
            Type = type,
            Severity = severity,
            AffectedService = service,
            AffectedResource = resource,
            TriggeredAt = DateTime.UtcNow,
            Metadata = new Dictionary<string, string>
            {
                ["environment"] = "production",
                ["region"] = "eastus",
                ["subscription"] = "sub-helpdesk-demo"
            }
        };
    }

    public async Task<Alert> IngestAlertAsync(Alert alert)
    {
        alert.Id = Guid.NewGuid();
        alert.TriggeredAt = DateTime.UtcNow;
        alert.Status = AlertStatus.New;

        db.Alerts.Add(alert);
        await db.SaveChangesAsync();

        logger.LogInformation("Alert ingested: {AlertId} - {Title} [{Severity}]", alert.Id, alert.Title, alert.Severity);
        return alert;
    }

    public async Task<Alert?> GetAlertAsync(Guid id) =>
        await db.Alerts.FindAsync(id);

    public async Task<List<Alert>> GetAlertsAsync(AlertStatus? status = null, int limit = 50)
    {
        var query = db.Alerts.AsQueryable();
        if (status.HasValue)
            query = query.Where(a => a.Status == status.Value);
        return await query
            .OrderByDescending(a => a.TriggeredAt)
            .Take(limit)
            .ToListAsync();
    }

    public async Task<Alert> UpdateAlertStatusAsync(Guid id, AlertStatus status)
    {
        var alert = await db.Alerts.FindAsync(id)
            ?? throw new KeyNotFoundException($"Alert {id} not found.");

        alert.Status = status;
        if (status == AlertStatus.Resolved)
            alert.ResolvedAt = DateTime.UtcNow;

        await db.SaveChangesAsync();
        logger.LogInformation("Alert {AlertId} status updated to {Status}", id, status);
        return alert;
    }

    public async Task<string> AnalyzeAlertWithAiAsync(Guid id)
    {
        var alert = await db.Alerts.FindAsync(id)
            ?? throw new KeyNotFoundException($"Alert {id} not found.");

        var analysis = GenerateContextualAnalysis(alert);

        alert.AiAnalysis = analysis;
        await db.SaveChangesAsync();

        return analysis;
    }

    private static string GenerateContextualAnalysis(Alert alert) => alert.Type switch
    {
        AlertType.HighCpuUsage =>
            $"The CPU spike on '{alert.AffectedResource}' is likely caused by a sudden increase in request volume or an inefficient background job. " +
            "Review recent deployments and check Application Insights for long-running operations. " +
            "Consider scaling out horizontally if load is legitimate, or profiling CPU-heavy operations.\n\n" +
            "**Immediate actions:** Scale up/out the App Service Plan, check for runaway threads in Application Insights, review scheduled jobs.",

        AlertType.FailedRequests =>
            $"The elevated 5xx error rate on '{alert.AffectedService}' suggests an upstream dependency failure or an unhandled exception in request processing. " +
            "Similar incidents in the past were resolved by checking database connection strings and verifying downstream API availability.\n\n" +
            "**Suggested priority:** High\n" +
            "**Immediate actions:** Check Application Insights exceptions, verify DB connectivity, review recent deployments, restart App Service if errors are transient.",

        AlertType.SlowApiResponse =>
            $"Response time degradation on '{alert.AffectedService}' typically indicates slow database queries, external API latency, or resource contention. " +
            "Review slow query logs in Azure SQL and check for N+1 query patterns.\n\n" +
            "**Immediate actions:** Run slow query analysis in Azure SQL, check for missing indexes, verify external dependency response times.",

        AlertType.AppServiceUnavailable =>
            $"'{alert.AffectedResource}' is failing health checks. This may indicate an application crash, misconfiguration, or deployment failure. " +
            "Check Kudu (SCM) site for deployment logs and review the application event log.\n\n" +
            "**Immediate actions:** Check Kudu logs at {resource}.scm.azurewebsites.net, restart the App Service, verify application startup exceptions.",

        AlertType.DatabaseConnectionFailure =>
            $"Connection pool exhaustion on '{alert.AffectedResource}' is a critical issue. Common causes: connection leaks (missing `using` statements), under-sized connection pool, or high traffic spike.\n\n" +
            "**Immediate actions:** Scale up DTUs/vCores on the SQL database, audit connection disposal in code, increase max pool size in connection string, check for blocking queries.",

        AlertType.MemorySpike =>
            $"Memory pressure on '{alert.AffectedResource}' suggests either a memory leak, large object heap fragmentation, or an unusually large data processing job. " +
            "Review memory profiles in Application Insights.\n\n" +
            "**Immediate actions:** Capture memory dump via Kudu, review large object allocations, consider restarting instance to reclaim memory short-term.",

        AlertType.LoginFailureSpike =>
            $"The sudden spike in authentication failures on '{alert.AffectedService}' is a security concern. This pattern matches credential stuffing or brute-force attacks.\n\n" +
            "**Immediate actions:** Enable Azure AD Identity Protection alerts, implement rate limiting on auth endpoints, consider geo-blocking suspicious IP ranges, notify security team.",

        _ => $"Alert '{alert.Title}' requires investigation. Review related logs in Application Insights and Azure Monitor for the affected service '{alert.AffectedService}'."
    };
}
