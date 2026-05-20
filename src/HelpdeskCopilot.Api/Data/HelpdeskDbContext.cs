using HelpdeskCopilot.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace HelpdeskCopilot.Api.Data;

public class HelpdeskDbContext(DbContextOptions<HelpdeskDbContext> options) : DbContext(options)
{
    public DbSet<Alert> Alerts => Set<Alert>();
    public DbSet<Ticket> Tickets => Set<Ticket>();
    public DbSet<ChatSession> ChatSessions => Set<ChatSession>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<KnowledgeDocument> KnowledgeDocuments => Set<KnowledgeDocument>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Alert>(e =>
        {
            e.HasKey(a => a.Id);
            e.Property(a => a.Metadata)
             .HasConversion(
                 v => System.Text.Json.JsonSerializer.Serialize(v, (System.Text.Json.JsonSerializerOptions?)null),
                 v => System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(v, (System.Text.Json.JsonSerializerOptions?)null) ?? new());
        });

        modelBuilder.Entity<Ticket>(e =>
        {
            e.HasKey(t => t.Id);
            e.OwnsMany(t => t.Comments, c =>
            {
                c.WithOwner().HasForeignKey("TicketId");
                c.HasKey(x => x.Id);
            });
        });

        modelBuilder.Entity<ChatSession>(e =>
        {
            e.HasKey(s => s.Id);
            e.OwnsMany(s => s.Messages, m =>
            {
                m.WithOwner().HasForeignKey("SessionId");
                m.HasKey(x => x.Id);
                m.Property(x => x.Sources)
                 .HasConversion(
                     v => System.Text.Json.JsonSerializer.Serialize(v, (System.Text.Json.JsonSerializerOptions?)null),
                     v => System.Text.Json.JsonSerializer.Deserialize<List<string>>(v, (System.Text.Json.JsonSerializerOptions?)null) ?? new());
            });
        });

        modelBuilder.Entity<KnowledgeDocument>(e =>
        {
            e.HasKey(d => d.Id);
            e.Property(d => d.Tags)
             .HasConversion(
                 v => System.Text.Json.JsonSerializer.Serialize(v, (System.Text.Json.JsonSerializerOptions?)null),
                 v => System.Text.Json.JsonSerializer.Deserialize<List<string>>(v, (System.Text.Json.JsonSerializerOptions?)null) ?? new());
        });

        modelBuilder.Entity<Notification>(e =>
        {
            e.HasKey(n => n.Id);
        });
    }
}
