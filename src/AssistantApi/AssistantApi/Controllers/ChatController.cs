using AssistantApi.Models.Requests;
using AssistantApi.Models.Responses;
using AssistantApi.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;

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
        var sw = Stopwatch.StartNew();

        if (string.IsNullOrWhiteSpace(request.Message))
        {
            _logger.LogWarning("Chat request rejected: empty message");
            return BadRequest(new { error = "Поле message обязательно." });
        }

        _logger.LogInformation(
            "Chat request started: sessionId={SessionId}, messageLength={MessageLength}, historyCount={HistoryCount}",
            request.SessionId ?? "<new>",
            request.Message.Length,
            request.ConversationHistory?.Count ?? 0);

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

            _logger.LogInformation(
                "Chat request finished: sessionId={SessionId}, responseLength={ResponseLength}, elapsedMs={ElapsedMs}",
                response.SessionId,
                response.Response?.Length ?? 0,
                sw.ElapsedMilliseconds);

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Chat endpoint failed after {ElapsedMs}ms", sw.ElapsedMilliseconds);
            return StatusCode(502, new { error = "Не удалось получить ответ от LLM." });
        }
    }
}
