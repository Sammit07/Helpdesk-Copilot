using HelpdeskCopilot.Api.Models;
using HelpdeskCopilot.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace HelpdeskCopilot.Api.Controllers;

[ApiController]
[Route("api/tickets")]
public class TicketsController(ITicketService ticketService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetTickets([FromQuery] TicketStatus? status, [FromQuery] int limit = 50) =>
        Ok(await ticketService.GetTicketsAsync(status, limit));

    [HttpPost]
    public async Task<IActionResult> CreateTicket([FromBody] Ticket ticket)
    {
        var created = await ticketService.CreateTicketAsync(ticket);
        return CreatedAtAction(nameof(GetTicket), new { id = created.Id }, created);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetTicket(Guid id)
    {
        var ticket = await ticketService.GetTicketAsync(id);
        return ticket is null ? NotFound() : Ok(ticket);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdateTicket(Guid id, [FromBody] Ticket updates)
    {
        try
        {
            return Ok(await ticketService.UpdateTicketAsync(id, updates));
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpPost("{id:guid}/comments")]
    public async Task<IActionResult> AddComment(Guid id, [FromBody] AddCommentRequest req)
    {
        try
        {
            var comment = new TicketComment { Author = req.Author, Content = req.Content };
            return Ok(await ticketService.AddCommentAsync(id, comment));
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpPut("{id:guid}/resolve")]
    public async Task<IActionResult> ResolveTicket(Guid id)
    {
        var ticket = await ticketService.GetTicketAsync(id);
        if (ticket is null) return NotFound();

        ticket.Status = TicketStatus.Resolved;
        return Ok(await ticketService.UpdateTicketAsync(id, ticket));
    }
}
