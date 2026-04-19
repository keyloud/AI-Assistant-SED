using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using AssistantApi.Infrastructure.OllamaClient;
using AssistantApi.Models.Domain;
using AssistantApi.Services.Interfaces;
using Microsoft.Extensions.Options;
using System.Net.Http.Json;

namespace AssistantApi.Infrastructure.QdrantClient;

/// <summary>
/// Phase 4: embed query via Ollama, search Qdrant by cosine similarity, return top-k chunks.
/// </summary>
public class QdrantHttpClient : IRagService
{
    private readonly HttpClient _httpClient;
    private readonly QdrantOptions _options;
    private readonly OllamaOptions _ollamaOptions;
    private readonly ILogger<QdrantHttpClient> _logger;

    public QdrantHttpClient(
        HttpClient httpClient,
        IOptions<QdrantOptions> options,
        IOptions<OllamaOptions> ollamaOptions,
        ILogger<QdrantHttpClient> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _ollamaOptions = ollamaOptions.Value;
        _logger = logger;
    }

    public async Task<List<KnowledgeChunk>> SearchAsync(
        string query,
        int topK = 3,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return [];
        }

        var sw = Stopwatch.StartNew();
        var embedding = await CreateEmbeddingAsync(query, ct);
        if (embedding.Count == 0)
        {
            _logger.LogWarning("RAG-поиск пропущен: генерация эмбеддинга вернула пустой вектор");
            return [];
        }

        var endpoint = $"http://{_options.Host}:{_options.Port}/collections/{_options.CollectionName}/points/search";
        var payload = new
        {
            vector = embedding,
            limit = topK,
            with_payload = true,
            with_vector = false
        };

        using var response = await _httpClient.PostAsJsonAsync(endpoint, payload, ct);
        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(ct);
            _logger.LogWarning(
                "Поиск в Qdrant завершился ошибкой со статусом {Status}: {Body}",
                response.StatusCode,
                errorBody);
            return [];
        }

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        var searchResponse = await JsonSerializer.DeserializeAsync<QdrantSearchResponse>(stream, cancellationToken: ct);
        var result = searchResponse?.Result ?? [];

        var chunks = result
            .Select(MapToKnowledgeChunk)
            .Where(chunk => !string.IsNullOrWhiteSpace(chunk.Content))
            .ToList();

        _logger.LogInformation(
            "RAG-поиск завершен: длина запроса={QueryLength}, topK={TopK}, найдено={Hits}, время={ElapsedMs}мс",
            query.Length,
            topK,
            chunks.Count,
            sw.ElapsedMilliseconds);

        return chunks;
    }

    private async Task<List<float>> CreateEmbeddingAsync(string query, CancellationToken ct)
    {
        var endpoint = ($"{_ollamaOptions.BaseUrl?.TrimEnd('/') ?? "http://ollama:11434"}/api/embeddings");
        var payload = new
        {
            model = string.IsNullOrWhiteSpace(_ollamaOptions.EmbeddingModel) ? "bge-m3" : _ollamaOptions.EmbeddingModel,
            prompt = query
        };

        using var response = await _httpClient.PostAsJsonAsync(endpoint, payload, ct);
        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(ct);
            _logger.LogWarning(
                "Запрос эмбеддинга завершился ошибкой со статусом {Status}: {Body}",
                response.StatusCode,
                errorBody);
            return [];
        }

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        var embeddingResponse = await JsonSerializer.DeserializeAsync<OllamaEmbeddingResponse>(stream, cancellationToken: ct);
        return embeddingResponse?.Embedding ?? [];
    }

    private static KnowledgeChunk MapToKnowledgeChunk(QdrantSearchResult result)
    {
        var payload = result.Payload;
        var metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        var content = GetString(payload, "content")
            ?? GetString(payload, "text")
            ?? string.Empty;

        var sourceFile = GetString(payload, "source_file")
            ?? GetString(payload, "sourceFile")
            ?? GetString(payload, "file")
            ?? string.Empty;

        var documentTitle = GetString(payload, "document_title")
            ?? GetString(payload, "documentTitle")
            ?? GetString(payload, "title")
            ?? string.Empty;

        var section = GetString(payload, "section")
            ?? GetString(payload, "chunk_section")
            ?? string.Empty;

        foreach (var property in payload.EnumerateObject())
        {
            if (property.Value.ValueKind is JsonValueKind.String)
            {
                metadata[property.Name] = property.Value.GetString() ?? string.Empty;
            }
        }

        return new KnowledgeChunk
        {
            Id = result.Id ?? string.Empty,
            Content = content,
            SourceFile = sourceFile,
            DocumentTitle = documentTitle,
            Section = section,
            RelevanceScore = result.Score,
            Metadata = metadata
        };
    }

    private static string? GetString(JsonElement payload, string name)
    {
        if (payload.ValueKind != JsonValueKind.Object || !payload.TryGetProperty(name, out var value))
        {
            return null;
        }

        return value.ValueKind == JsonValueKind.String ? value.GetString() : value.ToString();
    }

    private sealed class OllamaEmbeddingResponse
    {
        [JsonPropertyName("embedding")]
        public List<float>? Embedding { get; set; }
    }

    private sealed class QdrantSearchResponse
    {
        [JsonPropertyName("result")]
        public List<QdrantSearchResult>? Result { get; set; }
    }

    private sealed class QdrantSearchResult
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("score")]
        public float Score { get; set; }

        [JsonPropertyName("payload")]
        public JsonElement Payload { get; set; }
    }
}
