using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Text;
using System.Text.Json;

namespace HelpdeskCopilot.Functions;

public class AlertProcessorFunction(
    IHttpClientFactory httpClientFactory,
    ILogger<AlertProcessorFunction> logger)
{
    private static readonly string[] _alertTypes =
    [
        "HighCpuUsage", "FailedRequests", "SlowApiResponse",
        "AppServiceUnavailable", "DatabaseConnectionFailure",
        "MemorySpike", "LoginFailureSpike"
    ];

    /// <summary>
    /// Runs every 5 minutes to simulate Azure Monitor polling.
    /// In production, this would call Azure Monitor REST API or subscribe to action groups.
    /// </summary>
    [Function("AlertPoller")]
    public async Task RunPoller([TimerTrigger("0 */5 * * * *")] TimerInfo timer)
    {
        logger.LogInformation("Alert poller running at {Time}", DateTimeOffset.UtcNow);

        // Only fire on ~20% of runs to avoid flooding demo
        if (Random.Shared.NextDouble() > 0.2)
        {
            logger.LogDebug("Poller cycle skipped (no new alerts this cycle)");
            return;
        }

        var alertType = _alertTypes[Random.Shared.Next(_alertTypes.Length)];
        await SendMockAlertToApiAsync(alertType);
    }

    /// <summary>
    /// HTTP-triggered manual alert injection — useful for testing or webhook integration.
    /// POST /api/functions/trigger-alert with { "type": "FailedRequests" }
    /// </summary>
    [Function("TriggerAlert")]
    public async Task<HttpResponseData> TriggerAlert(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "functions/trigger-alert")] HttpRequestData req)
    {
        var body = await req.ReadAsStringAsync() ?? "{}";
        var doc = JsonDocument.Parse(body);
        var alertType = doc.RootElement.TryGetProperty("type", out var typeProp)
            ? typeProp.GetString() ?? "FailedRequests"
            : "FailedRequests";

        logger.LogInformation("Manual alert trigger received for type: {AlertType}", alertType);
        await SendMockAlertToApiAsync(alertType);

        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(new { message = $"Alert triggered: {alertType}", timestamp = DateTime.UtcNow });
        return response;
    }

    private async Task SendMockAlertToApiAsync(string alertType)
    {
        try
        {
            var client = httpClientFactory.CreateClient("HelpdeskApi");
            var payload = JsonSerializer.Serialize(new { type = alertType });
            var content = new StringContent(payload, Encoding.UTF8, "application/json");
            var response = await client.PostAsync("api/alerts/mock", content);

            if (response.IsSuccessStatusCode)
                logger.LogInformation("Mock alert ingested: {AlertType}", alertType);
            else
                logger.LogWarning("Alert ingestion returned {StatusCode}", response.StatusCode);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to ingest mock alert: {AlertType}", alertType);
        }
    }
}
