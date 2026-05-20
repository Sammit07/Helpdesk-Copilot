using HelpdeskCopilot.Api.Models;

namespace HelpdeskCopilot.Api.Services;

public interface IRagService
{
    Task<List<KnowledgeDocument>> SearchAsync(string query, int topK = 3);
    Task IndexDocumentAsync(KnowledgeDocument document);
    Task<List<KnowledgeDocument>> GetAllDocumentsAsync();
    Task SeedKnowledgeBaseAsync();
}
