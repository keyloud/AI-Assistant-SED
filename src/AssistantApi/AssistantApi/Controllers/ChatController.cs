using AssistantApi.Models.Requests;
using AssistantApi.Models.Responses;
using AssistantApi.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace AssistantApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ChatController : ControllerBase
{
    private readonly ILlmService _llmService;
    private readonly ILogger<ChatController> _logger;

    public ChatController(ILlmService llmService, ILogger<ChatController> logger)
    {
        _llmService = llmService;
        _logger = logger;
    }

    [HttpPost]
    public async Task<IActionResult> Post([FromBody] ChatRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Message))
        {
            return BadRequest(new { error = "Поле message обязательно." });
        }

        try
        {
            var responseText = await _llmService.GenerateAsync(request.Message, request.ConversationHistory, ct);

            var response = new ChatResponse
            {
                SessionId = string.IsNullOrWhiteSpace(request.SessionId) ? Guid.NewGuid().ToString("N") : request.SessionId,
                Response = responseText,
                RequestType = "KnowledgeBaseQuery",
                ClassificationConfidence = 0.9f,
                ValidationRemarks = new List<string>(),
                RagSources = new List<RagSource>(),
                PipelineTrace = new Dictionary<string, long>()
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Chat endpoint failed");
            return StatusCode(502, new { error = "Не удалось получить ответ от LLM." });
        }
    }
}
