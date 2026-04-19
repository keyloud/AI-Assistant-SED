using AssistantApi.Models.Domain;
using AssistantApi.Services.Interfaces;
using Microsoft.Extensions.Options;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Diagnostics;

namespace AssistantApi.Infrastructure.OllamaClient;

/// <summary>
/// HTTP-клиент для взаимодействия с локальным API Ollama.
/// Обеспечивает генерацию текста и работу с эмбеддингами.
/// </summary>
public class OllamaHttpClient : ILlmService
{
    private readonly HttpClient _httpClient;
    private readonly OllamaOptions _options;
    private readonly ILogger<OllamaHttpClient> _logger;

    public OllamaHttpClient(
        HttpClient httpClient,
        IOptions<OllamaOptions> options,
        ILogger<OllamaHttpClient> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>
    /// Асинхронная генерация полного ответа от модели.
    /// </summary>
    /// <param name="prompt">Сформированный промпт для модели.</param>
    /// <param name="history">История текущего диалога.</param>
    /// <param name="ct">Токен отмены операции.</param>
    /// <returns>Строка с ответом ассистента.</returns>
    public Task<string> GenerateAsync(
        string prompt,
        List<ConversationMessage>? history = null,
        CancellationToken ct = default)
    {
        return GenerateInternalAsync(prompt, history, ct);
    }

    /// <summary>
    /// Потоковая генерация ответа от модели (Server-Sent Events).
    /// </summary>
    /// <param name="prompt">Сформированный промпт для модели.</param>
    /// <param name="history">История текущего диалога.</param>
    /// <param name="ct">Токен отмены операции.</param>
    /// <returns>Асинхронный поток строк.</returns>
    public IAsyncEnumerable<string> GenerateStreamAsync(
        string prompt,
        List<ConversationMessage>? history = null,
        CancellationToken ct = default)
    {
        // TODO Phase 2: streaming via SSE
        throw new NotImplementedException("Implemented in Phase 2");
    }

    private async Task<string> GenerateInternalAsync(
        string prompt,
        List<ConversationMessage>? history,
        CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        var endpoint = (_options.BaseUrl?.TrimEnd('/') ?? "http://ollama:11434") + "/api/generate";

        var finalPrompt = BuildPromptWithHistory(prompt, history);

        _logger.LogInformation(
            "Генерация в Ollama запущена: модель={Model}, endpoint={Endpoint}, длина промпта={PromptLength}, элементов истории={HistoryCount}",
            _options.Model,
            endpoint,
            finalPrompt.Length,
            history?.Count ?? 0);

        var payload = new
        {
            model = _options.Model,
            prompt = finalPrompt,
            stream = false
        };

        using var response = await _httpClient.PostAsJsonAsync(endpoint, payload, ct);

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(ct);
            _logger.LogWarning("Запрос к Ollama завершился ошибкой со статусом {Status}: {Body}", response.StatusCode, errorBody);
            throw new InvalidOperationException($"Ollama request failed: {(int)response.StatusCode}");
        }

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        var result = await JsonSerializer.DeserializeAsync<OllamaGenerateResponse>(stream, cancellationToken: ct);

        _logger.LogInformation(
            "Генерация в Ollama завершена: модель={Model}, длина ответа={ResponseLength}, время={ElapsedMs}мс",
            _options.Model,
            result?.Response?.Length ?? 0,
            sw.ElapsedMilliseconds);

        return result?.Response ?? string.Empty;
    }

    private static string BuildPromptWithHistory(string prompt, List<ConversationMessage>? history)
    {
        if (history is null || history.Count == 0)
        {
            return prompt;
        }

        var sb = new StringBuilder();
        foreach (var item in history.TakeLast(8))
        {
            if (string.IsNullOrWhiteSpace(item.Content))
            {
                continue;
            }

            sb.AppendLine($"{item.Role}: {item.Content}");
        }

        sb.AppendLine();
        sb.AppendLine("user: " + prompt);
        return sb.ToString();
    }

    private sealed class OllamaGenerateResponse
    {
        [JsonPropertyName("response")]
        public string? Response { get; set; }
    }
}
