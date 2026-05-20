using HelpdeskCopilot.Api.Models;

namespace HelpdeskCopilot.Api.Services;

public interface IAlertIngestionService
{
    Task<Alert> IngestAlertAsync(Alert alert);
    Task<Alert?> GetAlertAsync(Guid id);
    Task<List<Alert>> GetAlertsAsync(AlertStatus? status = null, int limit = 50);
    Task<Alert> UpdateAlertStatusAsync(Guid id, AlertStatus status);
    Task<string> AnalyzeAlertWithAiAsync(Guid id);
    Alert GenerateMockAlert(AlertType type);
}
