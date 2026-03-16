using AssistantApi.Models.Domain;
using AssistantApi.Services.Interfaces;
using Microsoft.Extensions.Options;

namespace AssistantApi.Infrastructure.OllamaClient;

/// <summary>
/// Phase 2: implement GenerateAsync with POST /api/chat.
/// Phase 4: implement embedding via POST /api/embeddings.
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

    public Task<string> GenerateAsync(
        string prompt,
        List<ConversationMessage>? history = null,
        CancellationToken ct = default)
    {
        // TODO Phase 2: POST to {BaseUrl}/api/chat with model and messages
        throw new NotImplementedException("Implemented in Phase 2");
    }

    public IAsyncEnumerable<string> GenerateStreamAsync(
        string prompt,
        List<ConversationMessage>? history = null,
        CancellationToken ct = default)
    {
        // TODO Phase 2: streaming via SSE
        throw new NotImplementedException("Implemented in Phase 2");
    }
}
