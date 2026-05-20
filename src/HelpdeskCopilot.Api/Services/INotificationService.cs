using HelpdeskCopilot.Api.Models;

namespace HelpdeskCopilot.Api.Services;

public interface INotificationService
{
    Task SendAlertNotificationAsync(Alert alert);
    Task SendTicketCreatedNotificationAsync(Ticket ticket);
    Task<List<Notification>> GetNotificationsAsync(bool unreadOnly = false, int limit = 20);
    Task MarkAsReadAsync(Guid id);
    Task MarkAllAsReadAsync();
}
