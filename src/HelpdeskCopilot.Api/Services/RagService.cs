using Azure;
using Azure.Search.Documents;
using Azure.Search.Documents.Indexes;
using Azure.Search.Documents.Indexes.Models;
using Azure.Search.Documents.Models;
using HelpdeskCopilot.Api.Data;
using HelpdeskCopilot.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace HelpdeskCopilot.Api.Services;

public class RagService(
    HelpdeskDbContext db,
    IConfiguration config,
    ILogger<RagService> logger) : IRagService
{
    private readonly string? _searchEndpoint = config["AzureSearch:Endpoint"];
    private readonly string? _searchKey = config["AzureSearch:ApiKey"];
    private readonly string _indexName = config["AzureSearch:IndexName"] ?? "helpdesk-knowledge";

    public async Task<List<KnowledgeDocument>> SearchAsync(string query, int topK = 3)
    {
        if (!string.IsNullOrEmpty(_searchEndpoint) && !string.IsNullOrEmpty(_searchKey))
        {
            try
            {
                return await SearchAzureAsync(query, topK);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Azure AI Search failed, falling back to in-memory search");
            }
        }

        return await SearchInMemoryAsync(query, topK);
    }

    private async Task<List<KnowledgeDocument>> SearchAzureAsync(string query, int topK)
    {
        var client = new SearchClient(
            new Uri(_searchEndpoint!),
            _indexName,
            new AzureKeyCredential(_searchKey!));

        var options = new SearchOptions
        {
            Size = topK,
            QueryType = SearchQueryType.Semantic,
            SemanticSearch = new SemanticSearchOptions { SemanticConfigurationName = "default" },
            Select = { "id", "title", "content", "category", "tags" }
        };

        var response = await client.SearchAsync<SearchDocument>(query, options);
        var results = new List<KnowledgeDocument>();

        await foreach (var result in response.Value.GetResultsAsync())
        {
            static string? Str(Azure.Search.Documents.Models.SearchDocument doc, string key) =>
                doc.TryGetValue(key, out var v) ? v?.ToString() : null;

            results.Add(new KnowledgeDocument
            {
                Id = Str(result.Document, "id") ?? Guid.NewGuid().ToString(),
                Title = Str(result.Document, "title") ?? string.Empty,
                Content = Str(result.Document, "content") ?? string.Empty,
                Category = Str(result.Document, "category") ?? string.Empty,
                Score = result.Score
            });
        }

        return results;
    }

    private async Task<List<KnowledgeDocument>> SearchInMemoryAsync(string query, int topK)
    {
        var docs = await db.KnowledgeDocuments.ToListAsync();
        var terms = query.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries);

        return docs
            .Select(d => new
            {
                Doc = d,
                Score = ScoreDocument(d, terms)
            })
            .Where(x => x.Score > 0)
            .OrderByDescending(x => x.Score)
            .Take(topK)
            .Select(x => new KnowledgeDocument
            {
                Id = x.Doc.Id,
                Title = x.Doc.Title,
                Content = x.Doc.Content,
                Category = x.Doc.Category,
                Tags = x.Doc.Tags,
                Score = x.Score
            })
            .ToList();
    }

    private static double ScoreDocument(KnowledgeDocument doc, string[] terms)
    {
        var text = $"{doc.Title} {doc.Content} {string.Join(" ", doc.Tags)}".ToLowerInvariant();
        return terms.Sum(t => text.Contains(t) ? 1.0 : 0.0) / terms.Length;
    }

    public async Task IndexDocumentAsync(KnowledgeDocument document)
    {
        var existing = await db.KnowledgeDocuments.FindAsync(document.Id);
        if (existing != null)
        {
            existing.Title = document.Title;
            existing.Content = document.Content;
            existing.Category = document.Category;
            existing.Tags = document.Tags;
            existing.IndexedAt = DateTime.UtcNow;
        }
        else
        {
            document.IndexedAt = DateTime.UtcNow;
            db.KnowledgeDocuments.Add(document);
        }

        await db.SaveChangesAsync();

        if (!string.IsNullOrEmpty(_searchEndpoint) && !string.IsNullOrEmpty(_searchKey))
        {
            try
            {
                await IndexToAzureSearchAsync(document);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to index document in Azure AI Search");
            }
        }
    }

    private async Task IndexToAzureSearchAsync(KnowledgeDocument doc)
    {
        var client = new SearchClient(new Uri(_searchEndpoint!), _indexName, new AzureKeyCredential(_searchKey!));
        var batch = IndexDocumentsBatch.Upload(new[]
        {
            new SearchDocument
            {
                ["id"] = doc.Id,
                ["title"] = doc.Title,
                ["content"] = doc.Content,
                ["category"] = doc.Category,
                ["tags"] = doc.Tags
            }
        });
        await client.IndexDocumentsAsync(batch);
    }

    public async Task<List<KnowledgeDocument>> GetAllDocumentsAsync() =>
        await db.KnowledgeDocuments.OrderBy(d => d.Category).ToListAsync();

    public async Task SeedKnowledgeBaseAsync()
    {
        if (await db.KnowledgeDocuments.AnyAsync())
            return;

        var documents = new List<KnowledgeDocument>
        {
            new()
            {
                Id = "kb-001",
                Title = "Troubleshooting Azure App Service 500 Errors",
                Category = "App Service",
                Tags = ["500", "error", "app-service", "http", "failed-requests"],
                Content = """
                    Azure App Service 500 Internal Server Error — Troubleshooting Guide

                    SYMPTOMS
                    - HTTP 5xx responses in Application Insights requests table
                    - Elevated failed request rate in Azure Monitor
                    - Customer-facing error pages

                    COMMON CAUSES
                    1. Unhandled application exceptions in code
                    2. Database connection string misconfiguration
                    3. Missing environment variables / app settings
                    4. Dependency service unavailability (downstream API, database)
                    5. Out of memory conditions

                    DIAGNOSTIC STEPS
                    1. Check Application Insights: exceptions | where timestamp > ago(1h) | order by timestamp desc
                    2. Check failed requests: requests | where success == false | summarize count() by resultCode, name
                    3. Review Kudu log stream at https://<appname>.scm.azurewebsites.net
                    4. Check App Service Diagnose & Solve Problems blade
                    5. Review deployment logs for recent changes

                    RESOLUTION
                    - If database issue: verify connection string in App Settings, test connectivity from Kudu console
                    - If missing config: compare app settings with what the application expects
                    - If dependency issue: check downstream service health, implement circuit breaker
                    - Restart App Service: az webapp restart --name <app> --resource-group <rg>
                    """
            },
            new()
            {
                Id = "kb-002",
                Title = "Database Connection Timeout — Resolution Guide",
                Category = "Database",
                Tags = ["sql", "connection", "timeout", "pool", "database"],
                Content = """
                    Database Connection Timeout Troubleshooting Guide

                    SYMPTOMS
                    - SqlException: Timeout expired
                    - Connection pool exhausted errors
                    - Requests queuing up and failing
                    - High latency on all database-dependent operations

                    ROOT CAUSES
                    1. Connection pool exhaustion (most common) — connections not disposed properly
                    2. Long-running blocking queries holding connections
                    3. SQL Server under-resourced (too few DTUs/vCores)
                    4. Network firewall blocking connections
                    5. Incorrect connection string or expired credentials

                    DIAGNOSTIC QUERIES (KQL)
                    exceptions
                    | where type has "SqlException" or type has "TimeoutException"
                    | where timestamp > ago(1h)
                    | summarize count() by problemId, type
                    | order by count_ desc

                    RESOLUTION STEPS
                    1. Check for connection leaks — ensure all DbContext/SqlConnection instances are in 'using' blocks
                    2. Scale up Azure SQL: az sql db update --service-objective S3 ...
                    3. Review blocking queries in Azure SQL Query Performance Insight
                    4. Increase Max Pool Size in connection string: Max Pool Size=200
                    5. Add retry logic with Polly: services.AddDbContext with EnableRetryOnFailure

                    PREVENTION
                    - Always use dependency injection for DbContext
                    - Implement connection resiliency with EF Core
                    - Set up Azure SQL alerts for DTU consumption > 80%
                    """
            },
            new()
            {
                Id = "kb-003",
                Title = "High CPU Investigation Checklist",
                Category = "Performance",
                Tags = ["cpu", "performance", "profiling", "scale", "app-service"],
                Content = """
                    High CPU Usage Investigation — Step-by-Step Checklist

                    TRIAGE
                    □ Confirm CPU is actually high (not alert misconfiguration)
                    □ Identify which instance(s) are affected
                    □ Correlate with deployment or traffic spike timeline

                    DIAGNOSTIC COMMANDS
                    # Get CPU metrics via CLI
                    az monitor metrics list --resource <resource-id> --metric "CpuPercentage" --interval PT1M

                    KQL for CPU-correlated requests:
                    performanceCounters
                    | where name == "% Processor Time"
                    | where timestamp > ago(1h)
                    | summarize avg(value) by bin(timestamp, 5m), cloud_RoleInstance
                    | render timechart

                    COMMON CAUSES & FIXES
                    1. Infinite loop / tight loop in code — Profile with Application Insights Profiler
                    2. Inefficient LINQ / ORM queries generating N+1 — Use SQL profiler or EF logging
                    3. Excessive serialization/deserialization — Profile hot paths
                    4. Background job runaway — Review Hangfire / Azure WebJobs status
                    5. Legitimate traffic growth — Scale out: az appservice plan update --number-of-workers 3

                    PROFILING
                    - Enable Application Insights Profiler in the App Service
                    - Collect profiler trace during the spike
                    - Review flame graphs in Azure Portal > Application Insights > Performance > Profiler
                    """
            },
            new()
            {
                Id = "kb-004",
                Title = "Azure Function Failure Runbook",
                Category = "Azure Functions",
                Tags = ["function", "azure-functions", "timeout", "cold-start", "durable"],
                Content = """
                    Azure Function Failure Runbook

                    COMMON FAILURE MODES

                    1. COLD START LATENCY
                    - Symptom: First invocation takes 10-30s
                    - Fix: Use Always On (App Service Plan), Premium Plan, or pre-warm triggers
                    - KQL: traces | where message has "cold start" | where timestamp > ago(1h)

                    2. FUNCTION TIMEOUT
                    - Default timeout: Consumption=5m, Premium=30m, Dedicated=30m
                    - Fix: Increase host.json timeout, or move to durable functions for long tasks
                    - host.json: { "functionTimeout": "00:10:00" }

                    3. DEPENDENCY INJECTION ERRORS
                    - Symptom: InvalidOperationException on startup
                    - Fix: Verify service registration in Program.cs, check constructor parameters

                    4. STORAGE ACCOUNT ISSUES
                    - Symptom: Function app won't start, AzureWebJobsStorage error
                    - Fix: Verify storage connection string, check storage account firewall settings

                    5. ORCHESTRATION FAILURES (Durable)
                    - Check Durable Task Hub tables in Azure Storage
                    - Review orchestration history: SELECT * FROM [dt].[Instances]
                    - Terminate stuck orchestrations via Durable Functions HTTP API

                    MONITORING
                    traces
                    | where customDimensions.Category == "Function.FunctionName.User"
                    | where severityLevel >= 3
                    | where timestamp > ago(30m)

                    ESCALATION
                    1. Collect invocation logs from Azure Portal > Function App > Monitor
                    2. Check Application Insights Live Metrics during failure window
                    3. If persists > 30min, escalate to Azure Support with correlation IDs
                    """
            },
            new()
            {
                Id = "kb-005",
                Title = "Memory Spike Investigation and Resolution",
                Category = "Performance",
                Tags = ["memory", "oom", "leak", "gc", "performance"],
                Content = """
                    Memory Spike / Out-of-Memory Resolution Guide

                    SYMPTOMS
                    - Working set memory > 85% of available RAM
                    - Increasing GC pause times in Application Insights
                    - Eventual OutOfMemoryException
                    - App Service restarting unexpectedly

                    DIAGNOSTIC STEPS
                    1. Check memory metrics:
                    performanceCounters
                    | where name == "Private Bytes"
                    | summarize avg(value) by bin(timestamp, 5m), cloud_RoleInstance
                    | render timechart

                    2. Capture memory dump via Kudu:
                    - Navigate to https://<appname>.scm.azurewebsites.net/DebugConsole
                    - Run: procdump -ma <pid> /home/LogFiles/dumps/

                    3. Analyze with dotnet-dump or PerfView

                    COMMON CAUSES
                    1. Static collection growing unbounded — Review static caches, add eviction
                    2. Event handler leaks — Ensure events are unsubscribed
                    3. Large in-memory buffers — Paginate queries, stream large responses
                    4. String interning abuse — Review string.Intern usage
                    5. Third-party library leaks — Update NuGet packages

                    IMMEDIATE RELIEF
                    - Restart app instance to reclaim memory (temporary fix)
                    - Scale up to a tier with more RAM
                    - Enable auto-heal in App Service to restart on memory threshold
                    """
            },
            new()
            {
                Id = "kb-006",
                Title = "Login Failure Spike — Security Incident Response",
                Category = "Security",
                Tags = ["auth", "login", "security", "brute-force", "credential-stuffing"],
                Content = """
                    Login Failure Spike — Security Incident Response Guide

                    TRIAGE
                    Determine if this is an attack or a legitimate issue:
                    customEvents
                    | where name == "LoginFailed"
                    | where timestamp > ago(1h)
                    | summarize count() by tostring(customDimensions.ipAddress), tostring(customDimensions.userAgent)
                    | order by count_ desc

                    INDICATORS OF ATTACK
                    - Multiple failures from a single IP or IP range
                    - Failures against many different username/email combinations
                    - Unusual geographic origins (review with Azure AD Sign-ins)

                    IMMEDIATE RESPONSE
                    1. Enable Azure AD Identity Protection risk policies
                    2. Apply conditional access: require MFA for affected users
                    3. Block malicious IPs at Azure Front Door / WAF
                    4. Notify affected users of suspicious activity

                    RATE LIMITING IMPLEMENTATION
                    - Add IP-based rate limiting middleware (AspNetCoreRateLimit NuGet)
                    - Implement exponential backoff on failed logins
                    - Add CAPTCHA after 3 failed attempts

                    POST-INCIDENT
                    - Review Azure AD audit logs for compromised accounts
                    - Force password reset for accounts with many failures
                    - File security incident report
                    - Update threat model documentation
                    """
            }
        };

        foreach (var doc in documents)
            db.KnowledgeDocuments.Add(doc);

        await db.SaveChangesAsync();
        logger.LogInformation("Knowledge base seeded with {Count} documents", documents.Count);
    }
}
