using HelpdeskCopilot.Api.Models;
using HelpdeskCopilot.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace HelpdeskCopilot.Api.Controllers;

[ApiController]
[Route("api/alerts")]
public class AlertsController(
    IAlertIngestionService alertService,
    ITicketService ticketService,
    ICopilotChatService chatService,
    ILogAnalysisService logService,
    INotificationService notificationService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAlerts([FromQuery] AlertStatus? status, [FromQuery] int limit = 50) =>
        Ok(await alertService.GetAlertsAsync(status, limit));

    [HttpPost]
    public async Task<IActionResult> IngestAlert([FromBody] Alert alert)
    {
        var created = await alertService.IngestAlertAsync(alert);
        await notificationService.SendAlertNotificationAsync(created);
        return CreatedAtAction(nameof(GetAlert), new { id = created.Id }, created);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetAlert(Guid id)
    {
        var alert = await alertService.GetAlertAsync(id);
        return alert is null ? NotFound() : Ok(alert);
    }

    [HttpPut("{id:guid}/status")]
    public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] AlertStatusUpdateRequest req)
    {
        try
        {
            var updated = await alertService.UpdateAlertStatusAsync(id, req.Status);
            return Ok(updated);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpPost("{id:guid}/analyze")]
    public async Task<IActionResult> AnalyzeAlert(Guid id)
    {
        try
        {
            var analysis = await alertService.AnalyzeAlertWithAiAsync(id);
            return Ok(new { alertId = id, analysis });
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpPost("mock")]
    public async Task<IActionResult> GenerateMockAlert([FromBody] MockAlertRequest req)
    {
        var mock = alertService.GenerateMockAlert(req.Type);
        var created = await alertService.IngestAlertAsync(mock);
        await notificationService.SendAlertNotificationAsync(created);
        return CreatedAtAction(nameof(GetAlert), new { id = created.Id }, created);
    }

    [HttpPost("{id:guid}/create-ticket")]
    public async Task<IActionResult> CreateTicketFromAlert(Guid id)
    {
        var alert = await alertService.GetAlertAsync(id);
        if (alert is null) return NotFound();

        if (string.IsNullOrEmpty(alert.AiAnalysis))
            await alertService.AnalyzeAlertWithAiAsync(id);

        alert = await alertService.GetAlertAsync(id);
        var ticket = await ticketService.CreateTicketFromAlertAsync(alert!, alert!.AiAnalysis);
        await notificationService.SendTicketCreatedNotificationAsync(ticket);

        return CreatedAtAction("GetTicket", "Tickets", new { id = ticket.Id }, ticket);
    }

    [HttpGet("{id:guid}/logs")]
    public async Task<IActionResult> GetAlertLogs(Guid id)
    {
        var alert = await alertService.GetAlertAsync(id);
        if (alert is null) return NotFound();
        var result = await logService.AnalyzeLogsForAlertAsync(alert);
        return Ok(result);
    }
}
