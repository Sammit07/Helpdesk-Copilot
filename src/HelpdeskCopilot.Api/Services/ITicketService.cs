using HelpdeskCopilot.Api.Models;

namespace HelpdeskCopilot.Api.Services;

public interface ITicketService
{
    Task<Ticket> CreateTicketAsync(Ticket ticket);
    Task<Ticket> CreateTicketFromAlertAsync(Alert alert, string? aiSummary = null);
    Task<List<Ticket>> GetTicketsAsync(TicketStatus? status = null, int limit = 50);
    Task<Ticket?> GetTicketAsync(Guid id);
    Task<Ticket> UpdateTicketAsync(Guid id, Ticket updates);
    Task<Ticket> AddCommentAsync(Guid id, TicketComment comment);
}
