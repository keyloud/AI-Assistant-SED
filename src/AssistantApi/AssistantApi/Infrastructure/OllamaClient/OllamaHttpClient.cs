using AssistantApi.Models.Domain;
using AssistantApi.Services.Interfaces;
using Microsoft.Extensions.Options;

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
        // TODO Phase 2: POST to {BaseUrl}/api/chat with model and messages
        throw new NotImplementedException("Implemented in Phase 2");
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
}
