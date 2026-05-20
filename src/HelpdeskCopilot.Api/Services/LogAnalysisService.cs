using Azure.Monitor.Query;
using HelpdeskCopilot.Api.Models;
using Microsoft.Extensions.Configuration;

namespace HelpdeskCopilot.Api.Services;

public class LogAnalysisService(
    IConfiguration config,
    ILogger<LogAnalysisService> logger) : ILogAnalysisService
{
    private readonly string? _workspaceId = config["LogAnalytics:WorkspaceId"];

    public async Task<LogAnalysisResult> AnalyzeLogsForAlertAsync(Alert alert)
    {
        var kqlQuery = BuildKqlForAlert(alert);
        var result = await ExecuteKqlQueryAsync(kqlQuery, "30m");
        result.Summary = GenerateLogSummary(alert, result);
        return result;
    }

    public async Task<LogAnalysisResult> ExecuteKqlQueryAsync(string kqlQuery, string? timeRange = null)
    {
        if (!string.IsNullOrEmpty(_workspaceId))
        {
            try
            {
                return await ExecuteRealKqlAsync(kqlQuery, timeRange ?? "30m");
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Log Analytics query failed, returning mock data");
            }
        }

        logger.LogDebug("Log Analytics not configured — returning mock log data");
        return GenerateMockLogResult(kqlQuery, timeRange ?? "30m");
    }

    public async Task<List<LogEntry>> GetRecentErrorsAsync(string serviceName, int minutes = 30)
    {
        var result = await ExecuteKqlQueryAsync(
            $"exceptions | where cloud_RoleName == '{serviceName}' | where timestamp > ago({minutes}m) | order by timestamp desc | take 20",
            $"{minutes}m");
        return result.Entries;
    }

    private async Task<LogAnalysisResult> ExecuteRealKqlAsync(string query, string timeRange)
    {
        var credential = new Azure.Identity.DefaultAzureCredential();
        var client = new LogsQueryClient(credential);

        var duration = TimeSpan.FromMinutes(ParseTimeRangeMinutes(timeRange));
        var response = await client.QueryWorkspaceAsync(_workspaceId!, query, new QueryTimeRange(duration));

        var entries = new List<LogEntry>();
        if (response.Value.Table.Rows.Count > 0)
        {
            foreach (var row in response.Value.Table.Rows)
            {
                entries.Add(new LogEntry
                {
                    Timestamp = row.GetDateTimeOffset("timestamp")?.UtcDateTime ?? DateTime.UtcNow,
                    Level = "Error",
                    Message = row.GetString("message") ?? row.GetString("problemId") ?? "Unknown",
                    Source = row.GetString("cloud_RoleName") ?? "Unknown",
                    Count = (int)(row.GetInt32("count_") ?? 1)
                });
            }
        }

        return new LogAnalysisResult
        {
            Query = query,
            Entries = entries,
            TimeRange = timeRange,
            TotalEvents = entries.Sum(e => e.Count)
        };
    }

    private static LogAnalysisResult GenerateMockLogResult(string kqlQuery, string timeRange)
    {
        var now = DateTime.UtcNow;
        var entries = new List<LogEntry>
        {
            new() {
                Timestamp = now.AddMinutes(-2),
                Level = "Error",
                Message = "System.Data.SqlClient.SqlException: Timeout expired. The timeout period elapsed prior to completion of the operation or the server is not responding.",
                Source = "PaymentApi",
                ExceptionType = "SqlException",
                Count = 47,
                Properties = new() { ["problemId"] = "SqlTimeout#47", ["operation"] = "POST /api/payments" }
            },
            new() {
                Timestamp = now.AddMinutes(-5),
                Level = "Error",
                Message = "HTTP 500 Internal Server Error on POST /api/payments/process — unhandled exception in payment pipeline",
                Source = "PaymentApi",
                ExceptionType = "UnhandledApiException",
                Count = 124,
                Properties = new() { ["statusCode"] = "500", ["url"] = "/api/payments/process" }
            },
            new() {
                Timestamp = now.AddMinutes(-8),
                Level = "Warning",
                Message = "Connection pool nearly exhausted: 48/50 connections in use",
                Source = "PaymentApi",
                Count = 3,
                Properties = new() { ["poolSize"] = "50", ["activeConnections"] = "48" }
            },
            new() {
                Timestamp = now.AddMinutes(-12),
                Level = "Error",
                Message = "Dependency call failed: downstream inventory service returned 503",
                Source = "OrderService",
                ExceptionType = "DependencyException",
                Count = 18,
                Properties = new() { ["dependency"] = "InventoryService", ["statusCode"] = "503" }
            },
            new() {
                Timestamp = now.AddMinutes(-15),
                Level = "Warning",
                Message = "Request queue depth exceeding threshold: 320 pending requests",
                Source = "PaymentApi",
                Count = 1,
                Properties = new() { ["queueDepth"] = "320" }
            }
        };

        return new LogAnalysisResult
        {
            Query = kqlQuery,
            Entries = entries,
            TimeRange = timeRange,
            TotalEvents = entries.Sum(e => e.Count),
            AnalyzedAt = DateTime.UtcNow
        };
    }

    private static string BuildKqlForAlert(Alert alert) => alert.Type switch
    {
        AlertType.FailedRequests =>
            "requests\n| where timestamp > ago(30m)\n| where success == false\n| summarize count() by resultCode, name\n| order by count_ desc",

        AlertType.HighCpuUsage =>
            "performanceCounters\n| where timestamp > ago(30m)\n| where name == '% Processor Time'\n| summarize avg(value) by bin(timestamp, 1m)\n| order by timestamp desc",

        AlertType.DatabaseConnectionFailure =>
            "exceptions\n| where timestamp > ago(30m)\n| where type has 'SqlException' or type has 'TimeoutException'\n| summarize count() by type, problemId\n| order by count_ desc",

        AlertType.SlowApiResponse =>
            "requests\n| where timestamp > ago(30m)\n| where duration > 3000\n| summarize avg(duration), percentile(duration, 95), count() by name\n| order by avg_duration desc",

        AlertType.LoginFailureSpike =>
            "customEvents\n| where timestamp > ago(30m)\n| where name == 'LoginFailed'\n| summarize count() by bin(timestamp, 5m), tostring(customDimensions.ipAddress)\n| order by timestamp desc",

        _ =>
            "exceptions\n| where timestamp > ago(30m)\n| summarize count() by type, problemId\n| order by count_ desc"
    };

    private static string GenerateLogSummary(Alert alert, LogAnalysisResult result)
    {
        var totalErrors = result.TotalEvents;
        var topError = result.Entries.FirstOrDefault();
        return $"Log analysis for '{alert.AffectedService}' over the last {result.TimeRange}: " +
               $"Found {totalErrors} error events across {result.Entries.Count} distinct patterns. " +
               (topError != null ? $"Most frequent: '{topError.Message}' ({topError.Count} occurrences)." : "No critical errors found.");
    }

    private static int ParseTimeRangeMinutes(string timeRange) =>
        timeRange.TrimEnd('m') is string s && int.TryParse(s, out var m) ? m : 30;
}
