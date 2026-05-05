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
    private const int MaxDocumentContextCharsForPrompt = 12000;
    private const int DocumentChunkSize = 900;
    private const int DocumentChunkOverlap = 180;
    private const int TopDocumentChunks = 3;

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

    [HttpDelete("sessions/{sessionId}")]
    public IActionResult DeleteSession([FromRoute] string sessionId)
    {
        return _chatSessionStore.RemoveSession(sessionId)
            ? NoContent()
            : NotFound(new { error = "Чат не найден." });
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
        if (docsCountBefore >= 1)
        {
            return BadRequest(new { error = "В одном чате может быть только 1 документ." });
        }

        _chatSessionStore.Upsert(sessionId, session =>
        {
            if (session.Documents.Count >= 1)
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

            session.AttachedDocumentName = request.Name;
            if (!string.IsNullOrWhiteSpace(request.ContextSummary))
            {
                session.AttachedDocumentContext = request.ContextSummary;
            }

            session.UpdatedAtUtc = DateTime.UtcNow;
        });

        _logger.LogInformation(
            "Документ прикреплен к чату: sessionId={SessionId}, имя={DocumentName}, длина контекста={ContextLength}",
            sessionId,
            request.Name,
            request.ContextSummary?.Length ?? 0);

        var updated = _chatSessionStore.GetOrCreate(sessionId);
        return Ok(ToDetailsResponse(updated));
    }

    [HttpPost]
    public async Task<IActionResult> Post([FromBody] ChatRequest request, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();

        if (string.IsNullOrWhiteSpace(request.Message))
        {
            _logger.LogWarning("Запрос чата отклонен: пустое сообщение");
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

            if (!string.IsNullOrWhiteSpace(request.AttachedFileName))
            {
                session.AttachedDocumentName = request.AttachedFileName;
            }

            if (!string.IsNullOrWhiteSpace(request.AttachedFileContent))
            {
                session.AttachedDocumentContext = request.AttachedFileContent;

                _logger.LogDebug(
                    "Контекст документа обновлен из chat-запроса: sessionId={SessionId}, длина={ContextLength}",
                    sessionId,
                    request.AttachedFileContent.Length);
            }

            session.UpdatedAtUtc = DateTime.UtcNow;
        });

        _logger.LogInformation(
            "Запрос чата начат: sessionId={SessionId}, длина сообщения={MessageLength}, элементов истории={HistoryCount}",
            sessionId,
            request.Message.Length,
            request.ConversationHistory?.Count ?? 0);

        try
        {
            var sessionSnapshot = _chatSessionStore.GetOrCreate(sessionId);
            // Полный текст документа храним в сессии, а в prompt передаем управляемый фрагмент,
            // чтобы не переполнить контекст модели на очень больших документах.
            var documentContextForPrompt = BuildDocumentContextForPrompt(request.Message, sessionSnapshot.AttachedDocumentContext);
            var ragChunks = await _ragService.SearchAsync(request.Message, topK: 3, ct);
            var prompt = BuildAugmentedPrompt(
                request.Message,
                ragChunks,
                request.AttachedFileName ?? sessionSnapshot.AttachedDocumentName,
                documentContextForPrompt);

            _logger.LogInformation(
                "Для запроса чата найден RAG-контекст: чанков={ChunksCount}, длина контекста документа в prompt={DocumentContextLength}",
                ragChunks.Count,
                documentContextForPrompt.Length);

            _logger.LogDebug(
                "Подготовка контекста документа для prompt завершена: sessionId={SessionId}, естьКонтекст={HasDocumentContext}",
                sessionId,
                !string.IsNullOrWhiteSpace(sessionSnapshot.AttachedDocumentContext));

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
                        SourceFile = chunk.SourceFile,
                        Section = chunk.Section,
                        Score = chunk.RelevanceScore
                    })
                    .ToList(),
                PipelineTrace = new Dictionary<string, long>()
            };

            _logger.LogInformation(
                "Запрос чата завершен: sessionId={SessionId}, длина ответа={ResponseLength}, время={ElapsedMs}мс",
                response.SessionId,
                response.Response?.Length ?? 0,
                sw.ElapsedMilliseconds);

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Эндпоинт чата завершился с ошибкой через {ElapsedMs}мс", sw.ElapsedMilliseconds);
            return StatusCode(502, new { error = "Не удалось получить ответ от LLM." });
        }
    }

    private static string BuildAugmentedPrompt(
        string message,
        List<AssistantApi.Models.Domain.KnowledgeChunk> ragChunks,
        string? attachedFileName = null,
        string? attachedDocumentContext = null)
    {
        var behavior = """
            Инструкции к ответу:
            - Отвечай сразу по сути, без вступлений и без рассуждений о том, как ты отвечаешь.
            - Не используй формулировки про \"релевантные источники\".
            - Не вставляй в ответ маркеры вида (Источник 1), (Источник 2) и т.п.
            - Не используй слово \"источник\" в тексте ответа.
            - Если в приведенном контексте нет точного ответа, скажи: \"В базе знаний нет точного ответа по этому вопросу.\"
            """;

        var attachedFileBlock = string.IsNullOrWhiteSpace(attachedFileName)
            ? string.Empty
            : $"Прикрепленный файл: {attachedFileName}\n";
        var attachedDocumentContextBlock = string.IsNullOrWhiteSpace(attachedDocumentContext)
            ? string.Empty
            : $"Контекст прикрепленного документа:\n{attachedDocumentContext}\n\n";

        if (ragChunks.Count == 0)
        {
            if (string.IsNullOrEmpty(attachedFileBlock) && string.IsNullOrEmpty(attachedDocumentContextBlock))
            {
                return message;
            }

            return $"""
                {behavior}

                {attachedFileBlock}
                {attachedDocumentContextBlock}Вопрос пользователя:
                {message}
                """;
        }

        var context = string.Join("\n\n", ragChunks
            .Where(chunk => !string.IsNullOrWhiteSpace(chunk.Content))
            .Select(chunk =>
                $"""
                Документ: {(string.IsNullOrWhiteSpace(chunk.DocumentTitle) ? chunk.SourceFile : chunk.DocumentTitle)}
                Раздел: {chunk.Section}
                Файл: {chunk.SourceFile}
                Текст:
                {chunk.Content}
                """.Trim()));

        return $"""
            Ты ассистент СЭД.
            {behavior}

            {attachedFileBlock}
            {attachedDocumentContextBlock}

            Контекст базы знаний:
            {context}

            Вопрос пользователя:
            {message}
            """;
    }

    private static string BuildDocumentContextForPrompt(string message, string? fullDocumentContext)
    {
        if (string.IsNullOrWhiteSpace(fullDocumentContext))
        {
            return string.Empty;
        }

        var normalized = fullDocumentContext.Trim();
        if (normalized.Length <= MaxDocumentContextCharsForPrompt)
        {
            return normalized;
        }

        // Для длинных документов берем не просто первый срез, а наиболее релевантные фрагменты по вопросу.
        var chunks = SplitDocumentIntoChunks(normalized, DocumentChunkSize, DocumentChunkOverlap);
        var questionTokens = TokenizeForSearch(message);

        var ranked = chunks
            .Select((chunk, index) => new
            {
                Index = index,
                Chunk = chunk,
                Score = CalculateChunkScore(chunk, questionTokens)
            })
            .OrderByDescending(x => x.Score)
            .ThenBy(x => x.Index)
            .Take(TopDocumentChunks)
            .OrderBy(x => x.Index)
            .ToList();

        if (ranked.Count == 0 || ranked.All(x => x.Score <= 0))
        {
            return $"{normalized[..MaxDocumentContextCharsForPrompt]}\n\n[Контекст документа сокращен из-за большого объема]";
        }

        var selected = string.Join("\n\n", ranked.Select((x, i) => $"Фрагмент {i + 1}:\n{x.Chunk}"));
        if (selected.Length > MaxDocumentContextCharsForPrompt)
        {
            selected = selected[..MaxDocumentContextCharsForPrompt];
        }

        return $"Фрагменты прикрепленного документа:\n{selected}\n\n[Показаны только части документа из-за большого объема]";
    }

    private static List<string> SplitDocumentIntoChunks(string text, int chunkSize, int overlap)
    {
        var chunks = new List<string>();
        if (string.IsNullOrWhiteSpace(text))
        {
            return chunks;
        }

        var step = Math.Max(1, chunkSize - overlap);
        for (var start = 0; start < text.Length; start += step)
        {
            var length = Math.Min(chunkSize, text.Length - start);
            if (length <= 0)
            {
                break;
            }

            var chunk = text.Substring(start, length).Trim();
            if (!string.IsNullOrWhiteSpace(chunk))
            {
                chunks.Add(chunk);
            }

            if (start + length >= text.Length)
            {
                break;
            }
        }

        return chunks;
    }

    private static HashSet<string> TokenizeForSearch(string text)
    {
        return text
            .ToLowerInvariant()
            .Split([' ', '\n', '\r', '\t', ',', '.', ';', ':', '!', '?', '(', ')', '[', ']', '{', '}', '"', '\'', '-', '_', '/'], StringSplitOptions.RemoveEmptyEntries)
            .Where(t => t.Length >= 3)
            .ToHashSet(StringComparer.Ordinal);
    }

    private static int CalculateChunkScore(string chunk, HashSet<string> queryTokens)
    {
        if (queryTokens.Count == 0)
        {
            return 0;
        }

        var normalizedChunk = chunk.ToLowerInvariant();
        var score = 0;
        foreach (var token in queryTokens)
        {
            if (normalizedChunk.Contains(token, StringComparison.Ordinal))
            {
                score++;
            }
        }

        return score;
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
            _logger.LogWarning(ex, "Не удалось сгенерировать заголовок чата через LLM, будет использован fallback");
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
