using HelpdeskCopilot.Api.Models;

namespace HelpdeskCopilot.Api.Services;

public interface ILogAnalysisService
{
    Task<LogAnalysisResult> AnalyzeLogsForAlertAsync(Alert alert);
    Task<LogAnalysisResult> ExecuteKqlQueryAsync(string kqlQuery, string? timeRange = null);
    Task<List<LogEntry>> GetRecentErrorsAsync(string serviceName, int minutes = 30);
}
