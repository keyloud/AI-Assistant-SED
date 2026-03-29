using AssistantApi.Models.Requests;
using AssistantApi.Models.Responses;
using AssistantApi.Pipeline;
using Microsoft.AspNetCore.Mvc;

namespace AssistantApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ChatController : ControllerBase
{
    private readonly AssistantPipeline _pipeline;
    private readonly ILogger<ChatController> _logger;

    public ChatController(AssistantPipeline pipeline, ILogger<ChatController> logger)
    {
        _pipeline = pipeline;
        _logger = logger;
    }

    /// <summary>POST /api/chat — обработка текстового запроса пользователя.</summary>
    [HttpPost]
    public async Task<IActionResult> Chat([FromBody] ChatRequest request, CancellationToken ct)
    {
        _logger.LogInformation("Chat request: sessionId={SessionId}, userId={UserId}", request.SessionId, request.UserId);

        var context = new PipelineContext
        {
            SessionId  = request.SessionId,
            UserMessage = request.Message,
            DocumentId  = request.DocumentId,
            UserId      = request.UserId,
            History     = request.ConversationHistory
        };

        await _pipeline.ExecuteAsync(context, ct);

        var response = new ChatResponse
        {
            SessionId               = context.SessionId,
            Response                = context.LlmResponse,
            RequestType             = context.RequestType.ToString(),
            ClassificationConfidence = context.ClassificationConfidence,
            PipelineTrace           = context.StepDurationsMs
        };

        if (context.DocumentContext != null || context.RagResults.Count > 0)
        {
            response.ContextUsed = new ContextUsed
            {
                DocumentType  = context.DocumentContext?.DocumentType,
                DocumentStatus = context.DocumentContext?.Status,
                UserRole      = context.UserContext?.Role,
                RagChunksUsed = context.RagResults.Count,
                RagSources    = context.RagResults
                    .Select(r => new RagSource
                    {
                        Title   = r.DocumentTitle,
                        Section = r.Section,
                        Score   = r.RelevanceScore
                    })
                    .ToList()
            };
        }

        return Ok(response);
    }
}
