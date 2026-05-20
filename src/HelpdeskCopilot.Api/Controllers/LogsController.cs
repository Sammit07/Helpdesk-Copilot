using HelpdeskCopilot.Api.Models;
using HelpdeskCopilot.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace HelpdeskCopilot.Api.Controllers;

[ApiController]
[Route("api/logs")]
public class LogsController(
    ILogAnalysisService logService,
    IAlertIngestionService alertService) : ControllerBase
{
    [HttpGet("analyze/{alertId:guid}")]
    public async Task<IActionResult> AnalyzeAlertLogs(Guid alertId)
    {
        var alert = await alertService.GetAlertAsync(alertId);
        if (alert is null) return NotFound();
        return Ok(await logService.AnalyzeLogsForAlertAsync(alert));
    }

    [HttpPost("query")]
    public async Task<IActionResult> ExecuteQuery([FromBody] KqlQueryRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Query))
            return BadRequest(new { error = "Query cannot be empty." });

        return Ok(await logService.ExecuteKqlQueryAsync(req.Query, req.TimeRange));
    }

    [HttpGet("service/{serviceName}")]
    public async Task<IActionResult> GetServiceLogs(string serviceName, [FromQuery] int minutes = 30) =>
        Ok(await logService.GetRecentErrorsAsync(serviceName, minutes));
}
