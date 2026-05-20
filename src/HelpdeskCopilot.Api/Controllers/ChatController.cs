using HelpdeskCopilot.Api.Models;
using HelpdeskCopilot.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace HelpdeskCopilot.Api.Controllers;

[ApiController]
[Route("api/chat")]
public class ChatController(ICopilotChatService chatService) : ControllerBase
{
    [HttpPost("message")]
    public async Task<IActionResult> SendMessage([FromBody] ChatRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Message))
            return BadRequest(new { error = "Message cannot be empty." });

        var response = await chatService.ChatAsync(request);
        return Ok(response);
    }

    [HttpPost("sessions")]
    public async Task<IActionResult> CreateSession([FromBody] CreateSessionRequest req)
    {
        var session = await chatService.CreateSessionAsync(req.AlertId, req.Title);
        return CreatedAtAction(nameof(GetSession), new { sessionId = session.Id }, session);
    }

    [HttpGet("sessions")]
    public async Task<IActionResult> GetSessions([FromQuery] int limit = 10) =>
        Ok(await chatService.GetRecentSessionsAsync(limit));

    [HttpGet("sessions/{sessionId}")]
    public async Task<IActionResult> GetSession(string sessionId)
    {
        var session = await chatService.GetSessionAsync(sessionId);
        return session is null ? NotFound() : Ok(session);
    }
}
