using System.Diagnostics;
using System.Text;
using AssistantApi.Models.Domain;
using AssistantApi.Models.Responses;
using AssistantApi.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace AssistantApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DocumentController : ControllerBase
{
    private const int MaxFileSizeBytes = 10 * 1024 * 1024; // 10 MB
    private const int TextPreviewLength = 500;

    private readonly IDocumentParserService _parser;
    private readonly ILlmService _llmService;
    private readonly ILogger<DocumentController> _logger;

    public DocumentController(
        IDocumentParserService parser,
        ILlmService llmService,
        ILogger<DocumentController> logger)
    {
        _parser = parser;
        _llmService = llmService;
        _logger = logger;
    }

    /// <summary>
    /// POST /api/document/validate — загрузить документ и получить рекомендации по его заполнению.
    /// Принимает multipart/form-data с полем 'file' (.txt или .docx).
    /// </summary>
    [HttpPost("validate")]
    [RequestSizeLimit(MaxFileSizeBytes)]
    public async Task<IActionResult> ValidateDocument(
        IFormFile file,
        [FromForm] string? sessionId,
        CancellationToken ct)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { error = "Файл не предоставлен или пустой." });

        if (file.Length > MaxFileSizeBytes)
            return BadRequest(new { error = $"Файл слишком большой. Максимальный размер: {MaxFileSizeBytes / 1024 / 1024} МБ." });

        _logger.LogInformation("Document validation: file={FileName}, size={Size}B, sessionId={SessionId}",
            file.FileName, file.Length, sessionId);

        var sw = Stopwatch.StartNew();

        // 1. Извлечение текста
        string extractedText;
        try
        {
            using var stream = file.OpenReadStream();
            extractedText = await _parser.ExtractTextAsync(stream, file.FileName, ct);
        }
        catch (NotSupportedException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse document {FileName}", file.FileName);
            return BadRequest(new { error = $"Не удалось прочитать документ: {ex.Message}" });
        }

        if (string.IsNullOrWhiteSpace(extractedText))
            return BadRequest(new { error = "Документ не содержит текста или не удалось его извлечь." });

        var parseMs = sw.ElapsedMilliseconds;

        // 2. Формирование промпта для проверки
        var prompt = BuildValidationPrompt(file.FileName, extractedText);

        // 3. Вызов LLM для анализа
        string llmResponse;
        try
        {
            llmResponse = await _llmService.GenerateAsync(prompt, null, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "LLM validation failed for file {FileName}", file.FileName);
            return StatusCode(503, new { error = "Сервис LLM временно недоступен. Убедитесь, что Ollama запущена." });
        }

        sw.Stop();

        var response = new ValidateDocumentResponse
        {
            SessionId            = sessionId ?? string.Empty,
            FileName             = file.FileName,
            ExtractedTextPreview = extractedText.Length > TextPreviewLength
                ? extractedText[..TextPreviewLength] + "..."
                : extractedText,
            ValidationResult = new DocumentValidationResult
            {
                ValidationSummary = llmResponse
            },
            PipelineTrace = new Dictionary<string, long>
            {
                ["ParseDocumentMs"] = parseMs,
                ["TotalMs"]         = sw.ElapsedMilliseconds
            }
        };

        return Ok(response);
    }

    private static string BuildValidationPrompt(string fileName, string documentText)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Ты ассистент системы электронного документооборота (СЭД) для проверки документов.");
        sb.AppendLine();
        sb.AppendLine($"Тебе предоставлен документ для проверки.");
        sb.AppendLine($"Имя файла: {fileName}");
        sb.AppendLine();
        sb.AppendLine("Содержимое документа:");
        sb.AppendLine("---");
        sb.AppendLine(documentText);
        sb.AppendLine("---");
        sb.AppendLine();
        sb.AppendLine("Задача:");
        sb.AppendLine("1. Определи тип документа (договор, доверенность, акт выполненных работ, приказ и т.д.)");
        sb.AppendLine("2. Проверь правильность заполнения согласно стандартным требованиям к документам российского документооборота");
        sb.AppendLine("3. Укажи конкретные поля или разделы, где есть ошибки или отсутствует обязательная информация");
        sb.AppendLine("4. Дай конкретные рекомендации по исправлению");
        sb.AppendLine();
        sb.AppendLine("Ответь структурировано на русском языке:");
        sb.AppendLine("**Тип документа:** ...");
        sb.AppendLine("**Статус:** Корректный / Требует исправлений");
        sb.AppendLine("**Обнаруженные проблемы:**");
        sb.AppendLine("- ...");
        sb.AppendLine("**Рекомендации:**");
        sb.AppendLine("- ...");
        return sb.ToString();
    }
}
