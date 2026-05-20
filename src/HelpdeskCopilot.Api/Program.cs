using HelpdeskCopilot.Api.Data;
using HelpdeskCopilot.Api.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Controllers + API explorer
builder.Services.AddControllers()
    .AddJsonOptions(opt =>
        opt.JsonSerializerOptions.Converters.Add(
            new System.Text.Json.Serialization.JsonStringEnumConverter()));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new()
    {
        Title = "Azure Helpdesk Copilot API",
        Version = "v1",
        Description = "AI-powered helpdesk copilot for Azure infrastructure support"
    });
});

// Database
builder.Services.AddDbContext<HelpdeskDbContext>(opt =>
    opt.UseInMemoryDatabase("HelpdeskCopilotDb"));

// Core services
builder.Services.AddSingleton<ChatSessionStore>();
builder.Services.AddScoped<IAlertIngestionService, AlertIngestionService>();
builder.Services.AddScoped<ILogAnalysisService, LogAnalysisService>();
builder.Services.AddScoped<IRagService, RagService>();
builder.Services.AddScoped<ICopilotChatService, CopilotChatService>();
builder.Services.AddScoped<ITicketService, TicketService>();
builder.Services.AddScoped<INotificationService, NotificationService>();

// CORS for Blazor frontend
builder.Services.AddCors(opt =>
{
    var origins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
        ?? ["http://localhost:5001", "https://localhost:5001"];
    opt.AddDefaultPolicy(policy =>
        policy.WithOrigins(origins)
              .AllowAnyMethod()
              .AllowAnyHeader());
});

// Application Insights (optional)
var aiConnection = builder.Configuration["ApplicationInsights:ConnectionString"];
if (!string.IsNullOrEmpty(aiConnection))
    builder.Services.AddApplicationInsightsTelemetry(opt => opt.ConnectionString = aiConnection);

builder.Services.AddLogging(lb => lb.AddConsole());

var app = builder.Build();

// Seed knowledge base on startup
using (var scope = app.Services.CreateScope())
{
    var rag = scope.ServiceProvider.GetRequiredService<IRagService>();
    await rag.SeedKnowledgeBaseAsync();
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Helpdesk Copilot API v1");
        c.RoutePrefix = "swagger";
    });
}

app.UseExceptionHandler(errApp =>
{
    errApp.Run(async ctx =>
    {
        ctx.Response.StatusCode = 500;
        ctx.Response.ContentType = "application/json";
        await ctx.Response.WriteAsJsonAsync(new { error = "An unexpected error occurred. Check logs for details." });
    });
});

app.UseCors();
app.UseAuthorization();
app.MapControllers();

// Health endpoint
app.MapGet("/health", () => Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow }));

app.Run();
