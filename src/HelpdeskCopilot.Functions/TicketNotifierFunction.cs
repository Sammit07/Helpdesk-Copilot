using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace HelpdeskCopilot.Functions;

public class TicketNotifierFunction(
    IHttpClientFactory httpClientFactory,
    ILogger<TicketNotifierFunction> logger)
{
    /// <summary>
    /// Runs every 10 minutes. Checks for high/critical open tickets older than 1 hour
    /// and sends escalation notifications.
    /// </summary>
    [Function("TicketEscalationNotifier")]
    public async Task Run([TimerTrigger("0 */10 * * * *")] TimerInfo timer)
    {
        logger.LogInformation("Ticket escalation check running at {Time}", DateTimeOffset.UtcNow);

        try
        {
            var client = httpClientFactory.CreateClient("HelpdeskApi");
            var response = await client.GetAsync("api/tickets?status=Open&limit=100");

            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Could not fetch open tickets: {StatusCode}", response.StatusCode);
                return;
            }

            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                Converters = { new JsonStringEnumConverter() }
            };
            var content = await response.Content.ReadAsStringAsync();
            var tickets = JsonSerializer.Deserialize<List<TicketSummary>>(content, options) ?? [];

            var escalationCandidates = tickets
                .Where(t => t.Priority is "Critical" or "High")
                .Where(t => (DateTime.UtcNow - t.CreatedAt).TotalHours > 1)
                .ToList();

            if (escalationCandidates.Count == 0)
            {
                logger.LogDebug("No tickets requiring escalation");
                return;
            }

            foreach (var ticket in escalationCandidates)
            {
                logger.LogWarning(
                    "[ESCALATION] Ticket {TicketId} [{Priority}] has been open for {Hours:F1}h: {Title} | Service: {Service}",
                    ticket.Id,
                    ticket.Priority,
                    (DateTime.UtcNow - ticket.CreatedAt).TotalHours,
                    ticket.Title,
                    ticket.AffectedService);

                // In production: send Teams adaptive card or email via SendGrid/Graph API
                await SendTeamsEscalationAsync(ticket);
            }

            logger.LogInformation("Escalation check complete — {Count} ticket(s) escalated", escalationCandidates.Count);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ticket escalation notifier failed");
        }
    }

    private Task SendTeamsEscalationAsync(TicketSummary ticket)
    {
        // Mock Teams webhook — in production POST adaptive card to incoming webhook URL
        logger.LogInformation(
            "[TEAMS MOCK] Escalation card posted to #azure-incidents: Ticket {TicketId} — {Title} [{Priority}] is overdue",
            ticket.Id, ticket.Title, ticket.Priority);
        return Task.CompletedTask;
    }

    private record TicketSummary(
        Guid Id,
        string Title,
        string Priority,
        string AffectedService,
        DateTime CreatedAt);
}
