using AssistantApi.Models.Requests;
using AssistantApi.Models.Responses;
using AssistantApi.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using System.Linq;
using AssistantApi.Models.Domain;

namespace AssistantApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ChatController : ControllerBase
{
    private readonly ILlmService _llmService;
    private readonly IRagService _ragService;
    private readonly IChatSessionStore _chatSessionStore;
    private readonly ILogger<ChatController> _logger;

    public ChatController(
        ILlmService llmService,
        IRagService ragService,
        IChatSessionStore chatSessionStore,
        ILogger<ChatController> logger)
    {
        _llmService = llmService;
        _ragService = ragService;
        _chatSessionStore = chatSessionStore;
        _logger = logger;
    }

    [HttpGet("sessions")]
    public IActionResult ListSessions()
    {
        var sessions = _chatSessionStore
            .ListSessions()
            .Select(ToSummaryResponse)
            .ToList();

        return Ok(sessions);
    }

    [HttpPost("sessions")]
    public IActionResult CreateSession([FromBody] CreateChatSessionRequest? request)
    {
        var session = _chatSessionStore.CreateSession(request?.Title);
        return Ok(ToDetailsResponse(session));
    }

    [HttpGet("sessions/{sessionId}")]
    public IActionResult GetSession([FromRoute] string sessionId)
    {
        if (!_chatSessionStore.TryGet(sessionId, out var session) || session is null)
        {
            return NotFound(new { error = "Чат не найден." });
        }

        return Ok(ToDetailsResponse(session));
    }

    [HttpPost("sessions/{sessionId}/documents")]
    public IActionResult AttachDocument([FromRoute] string sessionId, [FromBody] AttachDocumentToChatRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return BadRequest(new { error = "Имя документа обязательно." });
        }

        if (!_chatSessionStore.TryGet(sessionId, out var existing) || existing is null)
        {
            return NotFound(new { error = "Чат не найден." });
        }

        var docsCountBefore = existing.Documents.Count;
        if (docsCountBefore >= 3)
        {
            return BadRequest(new { error = "В одном чате может быть не более 3 документов." });
        }

        _chatSessionStore.Upsert(sessionId, session =>
        {
            if (session.Documents.Count >= 3)
            {
                return;
            }

            session.Documents.Insert(0, new ChatDocumentInfo
            {
                Id = Guid.NewGuid().ToString("N"),
                Name = request.Name,
                UploadDate = request.UploadDate,
                Status = request.Status,
                Size = request.Size,
                UploadedBy = request.UploadedBy
            });

            session.UpdatedAtUtc = DateTime.UtcNow;
        });

        var updated = _chatSessionStore.GetOrCreate(sessionId);
        return Ok(ToDetailsResponse(updated));
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

        var sessionId = string.IsNullOrWhiteSpace(request.SessionId)
            ? Guid.NewGuid().ToString("N")
            : request.SessionId;

        _chatSessionStore.Upsert(sessionId, session =>
        {
            session.Messages.Add(new ConversationMessage
            {
                Role = "user",
                Content = request.Message,
                Timestamp = DateTime.UtcNow
            });

            session.UpdatedAtUtc = DateTime.UtcNow;
        });

        _logger.LogInformation(
            "Chat request started: sessionId={SessionId}, messageLength={MessageLength}, historyCount={HistoryCount}",
            sessionId,
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

            _chatSessionStore.Upsert(sessionId, session =>
            {
                session.Messages.Add(new ConversationMessage
                {
                    Role = "assistant",
                    Content = responseText,
                    Timestamp = DateTime.UtcNow
                });

                session.UpdatedAtUtc = DateTime.UtcNow;
            });

            var currentSession = _chatSessionStore.GetOrCreate(sessionId);
            var generatedTitle = currentSession.Title;

            if (!currentSession.IsTitleFinalized && currentSession.Messages.Count(m => m.Role == "user") == 1)
            {
                generatedTitle = await GenerateTitleAsync(request.Message, ct);

                _chatSessionStore.Upsert(sessionId, session =>
                {
                    session.Title = generatedTitle;
                    session.IsTitleFinalized = true;
                    session.UpdatedAtUtc = DateTime.UtcNow;
                });

                currentSession = _chatSessionStore.GetOrCreate(sessionId);
            }

            var response = new ChatResponse
            {
                SessionId = sessionId,
                ChatTitle = currentSession.Title,
                UpdatedAt = FormatUpdatedAt(currentSession.UpdatedAtUtc),
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

    private async Task<string> GenerateTitleAsync(string firstUserMessage, CancellationToken ct)
    {
        var prompt = $"""
            Сгенерируй короткий заголовок чата на русском языке.
            Требования:
            - 3-6 слов
            - без кавычек
            - деловой стиль
            - только сам заголовок, без пояснений

            Первое сообщение пользователя:
            {firstUserMessage}
            """;

        try
        {
            var raw = await _llmService.GenerateAsync(prompt, null, ct);
            var normalized = NormalizeTitle(raw);
            return string.IsNullOrWhiteSpace(normalized)
                ? BuildFallbackTitle(firstUserMessage)
                : normalized;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to generate chat title with LLM, fallback will be used");
            return BuildFallbackTitle(firstUserMessage);
        }
    }

    private static string NormalizeTitle(string? title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return string.Empty;
        }

        var singleLine = title
            .Replace("\r", " ")
            .Replace("\n", " ")
            .Replace("\"", string.Empty)
            .Trim();

        return singleLine.Length > 80 ? singleLine[..80].Trim() : singleLine;
    }

    private static string BuildFallbackTitle(string firstUserMessage)
    {
        var text = firstUserMessage.Trim();
        if (text.Length <= 48)
        {
            return text;
        }

        return $"{text[..48].Trim()}...";
    }

    private static string FormatUpdatedAt(DateTime utc)
    {
        var local = utc.ToLocalTime();
        return local.ToString("dd.MM.yyyy HH:mm");
    }

    private static ChatSessionSummaryResponse ToSummaryResponse(ChatSessionState session)
    {
        return new ChatSessionSummaryResponse
        {
            SessionId = session.SessionId,
            Title = session.Title,
            Status = session.Status,
            UpdatedAt = FormatUpdatedAt(session.UpdatedAtUtc),
            Documents = session.Documents
                .Select(d => new ChatDocumentInfo
                {
                    Id = d.Id,
                    Name = d.Name,
                    UploadDate = d.UploadDate,
                    Status = d.Status,
                    Size = d.Size,
                    UploadedBy = d.UploadedBy
                })
                .ToList()
        };
    }

    private static ChatSessionDetailsResponse ToDetailsResponse(ChatSessionState session)
    {
        var summary = ToSummaryResponse(session);
        return new ChatSessionDetailsResponse
        {
            SessionId = summary.SessionId,
            Title = summary.Title,
            Status = summary.Status,
            UpdatedAt = summary.UpdatedAt,
            Documents = summary.Documents,
            Messages = session.Messages
                .Select(m => new ConversationMessage
                {
                    Role = m.Role,
                    Content = m.Content,
                    Timestamp = m.Timestamp
                })
                .ToList()
        };
    }
}
