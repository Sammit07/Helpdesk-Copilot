using HelpdeskCopilot.Api.Models;
using HelpdeskCopilot.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace HelpdeskCopilot.Api.Controllers;

[ApiController]
[Route("api/knowledge")]
public class KnowledgeController(IRagService ragService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAllDocuments() =>
        Ok(await ragService.GetAllDocumentsAsync());

    [HttpPost("search")]
    public async Task<IActionResult> Search([FromBody] KnowledgeSearchRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Query))
            return BadRequest(new { error = "Query cannot be empty." });

        return Ok(await ragService.SearchAsync(req.Query, req.TopK));
    }

    [HttpPost]
    public async Task<IActionResult> IndexDocument([FromBody] KnowledgeDocument doc)
    {
        await ragService.IndexDocumentAsync(doc);
        return Ok(doc);
    }
}
