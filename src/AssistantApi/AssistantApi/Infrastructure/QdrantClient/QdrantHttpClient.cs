using AssistantApi.Models.Domain;
using AssistantApi.Models.Enums;
using AssistantApi.Services.Interfaces;
using Microsoft.Extensions.Options;

namespace AssistantApi.Infrastructure.QdrantClient;

/// <summary>
/// Phase 4: embed query via Ollama, search Qdrant by cosine similarity, return top-k chunks.
/// </summary>
public class QdrantHttpClient : IRagService
{
    private readonly HttpClient _httpClient;
    private readonly QdrantOptions _options;
    private readonly ILogger<QdrantHttpClient> _logger;

    public QdrantHttpClient(
        HttpClient httpClient,
        IOptions<QdrantOptions> options,
        ILogger<QdrantHttpClient> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public Task<List<KnowledgeChunk>> SearchAsync(
        string query,
        RequestType requestType,
        int topK = 3,
        CancellationToken ct = default)
    {
        // TODO Phase 4: POST to /collections/{CollectionName}/points/search
        throw new NotImplementedException("Implemented in Phase 4");
    }
}
