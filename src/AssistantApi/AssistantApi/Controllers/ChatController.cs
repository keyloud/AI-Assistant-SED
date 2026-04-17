using AssistantApi.Models.Requests;
using AssistantApi.Models.Responses;
using AssistantApi.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using System.Linq;

namespace AssistantApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ChatController : ControllerBase
{
    private readonly ILlmService _llmService;
    private readonly IRagService _ragService;
    private readonly ILogger<ChatController> _logger;

    public ChatController(
        ILlmService llmService,
        IRagService ragService,
        ILogger<ChatController> logger)
    {
        _llmService = llmService;
        _ragService = ragService;
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
            var ragChunks = await _ragService.SearchAsync(request.Message, topK: 3, ct);
            var prompt = BuildAugmentedPrompt(request.Message, ragChunks);

            _logger.LogInformation(
                "RAG context found for chat request: chunks={ChunksCount}",
                ragChunks.Count);

            var responseText = await _llmService.GenerateAsync(prompt, request.ConversationHistory, ct);

            var response = new ChatResponse
            {
                SessionId = string.IsNullOrWhiteSpace(request.SessionId) ? Guid.NewGuid().ToString("N") : request.SessionId,
                Response = responseText,
                RequestType = "KnowledgeBaseQuery",
                ClassificationConfidence = 0.9f,
                ValidationRemarks = new List<string>(),
                RagSources = ragChunks
                    .Select(chunk => new RagSource
                    {
                        Title = string.IsNullOrWhiteSpace(chunk.DocumentTitle) ? chunk.SourceFile : chunk.DocumentTitle,
                        Section = chunk.Section,
                        Score = chunk.RelevanceScore
                    })
                    .ToList(),
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

    private static string BuildAugmentedPrompt(string message, List<AssistantApi.Models.Domain.KnowledgeChunk> ragChunks)
    {
        if (ragChunks.Count == 0)
        {
            return message;
        }

        var sources = string.Join("\n\n", ragChunks.Select((chunk, index) =>
            $"Источник {index + 1}: {chunk.DocumentTitle} {chunk.Section}\n{chunk.Content}".Trim()));

        return $"""
            Ты ассистент СЭД. Отвечай только на основе релевантных источников ниже. Если источники не дают уверенного ответа, так и скажи.

            Источники:
            {sources}

            Вопрос пользователя:
            {message}
            """;
    }
}
