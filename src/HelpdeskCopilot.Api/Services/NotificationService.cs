using HelpdeskCopilot.Api.Data;
using HelpdeskCopilot.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace HelpdeskCopilot.Api.Services;

public class NotificationService(
    HelpdeskDbContext db,
    ILogger<NotificationService> logger) : INotificationService
{
    public async Task SendAlertNotificationAsync(Alert alert)
    {
        var severity = alert.Severity switch
        {
            AlertSeverity.Critical => NotificationSeverity.Critical,
            AlertSeverity.High => NotificationSeverity.Warning,
            _ => NotificationSeverity.Info
        };

        var notification = new Notification
        {
            Id = Guid.NewGuid(),
            Channel = "Dashboard",
            Recipient = "support-team",
            Subject = $"[{alert.Severity}] New Alert: {alert.Title}",
            Body = $"Service: {alert.AffectedService}\nResource: {alert.AffectedResource}\n\n{alert.Description}",
            Severity = severity,
            SentAt = DateTime.UtcNow,
            IsRead = false
        };

        db.Notifications.Add(notification);
        await db.SaveChangesAsync();

        // Mock Teams notification
        if (alert.Severity is AlertSeverity.Critical or AlertSeverity.High)
        {
            logger.LogInformation(
                "[TEAMS MOCK] Sending Teams alert to #azure-incidents: {Title} | Severity: {Severity} | Service: {Service}",
                alert.Title, alert.Severity, alert.AffectedService);
        }

        // Mock email notification for critical
        if (alert.Severity == AlertSeverity.Critical)
        {
            logger.LogInformation(
                "[EMAIL MOCK] Sending critical alert email to oncall@company.com: Subject: CRITICAL — {Title}",
                alert.Title);
        }
    }

    public async Task SendTicketCreatedNotificationAsync(Ticket ticket)
    {
        var notification = new Notification
        {
            Id = Guid.NewGuid(),
            Channel = "Dashboard",
            Recipient = ticket.AssignedTo,
            Subject = $"Ticket Created: {ticket.Title}",
            Body = $"Priority: {ticket.Priority}\nService: {ticket.AffectedService}\n\nSummary: {ticket.AiSummary}",
            Severity = ticket.Priority switch
            {
                TicketPriority.Critical => NotificationSeverity.Critical,
                TicketPriority.High => NotificationSeverity.Warning,
                _ => NotificationSeverity.Info
            },
            SentAt = DateTime.UtcNow,
            IsRead = false
        };

        db.Notifications.Add(notification);
        await db.SaveChangesAsync();

        logger.LogInformation(
            "[TEAMS MOCK] Ticket notification sent: {TicketId} | {Title} | Priority: {Priority}",
            ticket.Id, ticket.Title, ticket.Priority);
    }

    public async Task<List<Notification>> GetNotificationsAsync(bool unreadOnly = false, int limit = 20)
    {
        var query = db.Notifications.AsQueryable();
        if (unreadOnly)
            query = query.Where(n => !n.IsRead);
        return await query
            .OrderByDescending(n => n.SentAt)
            .Take(limit)
            .ToListAsync();
    }

    public async Task MarkAsReadAsync(Guid id)
    {
        var notification = await db.Notifications.FindAsync(id);
        if (notification != null)
        {
            notification.IsRead = true;
            await db.SaveChangesAsync();
        }
    }

    public async Task MarkAllAsReadAsync()
    {
        var unread = await db.Notifications.Where(n => !n.IsRead).ToListAsync();
        foreach (var n in unread)
            n.IsRead = true;
        await db.SaveChangesAsync();
    }
}
