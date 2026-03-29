using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
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
    public async Task<string> GenerateAsync(
        string prompt,
        List<ConversationMessage>? history = null,
        CancellationToken ct = default)
    {
        var messages = BuildMessages(prompt, history);
        var requestBody = new { model = _options.Model, messages, stream = false };
        var json = JsonSerializer.Serialize(requestBody);

        _logger.LogDebug("Sending request to Ollama: model={Model}", _options.Model);

        var response = await _httpClient.PostAsync(
            $"{_options.BaseUrl}/api/chat",
            new StringContent(json, Encoding.UTF8, "application/json"),
            ct);

        response.EnsureSuccessStatusCode();

        using var responseStream = await response.Content.ReadAsStreamAsync(ct);
        using var doc = await JsonDocument.ParseAsync(responseStream, cancellationToken: ct);

        return doc.RootElement
            .GetProperty("message")
            .GetProperty("content")
            .GetString() ?? string.Empty;
    }

    /// <summary>
    /// Потоковая генерация ответа от модели (Server-Sent Events).
    /// </summary>
    public async IAsyncEnumerable<string> GenerateStreamAsync(
        string prompt,
        List<ConversationMessage>? history = null,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var messages = BuildMessages(prompt, history);
        var requestBody = new { model = _options.Model, messages, stream = true };
        var json = JsonSerializer.Serialize(requestBody);

        using var request = new HttpRequestMessage(HttpMethod.Post, $"{_options.BaseUrl}/api/chat")
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

        using var response = await _httpClient.SendAsync(
            request, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();

        using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var reader = new StreamReader(stream);

        while (!reader.EndOfStream && !ct.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(ct);
            if (string.IsNullOrWhiteSpace(line)) continue;

            using var chunk = JsonDocument.Parse(line);
            var content = chunk.RootElement
                .GetProperty("message")
                .GetProperty("content")
                .GetString();

            if (!string.IsNullOrEmpty(content))
                yield return content;

            if (chunk.RootElement.TryGetProperty("done", out var done) && done.GetBoolean())
                break;
        }
    }

    private static List<object> BuildMessages(string prompt, List<ConversationMessage>? history)
    {
        var messages = new List<object>();

        if (history != null)
        {
            foreach (var msg in history)
                messages.Add(new { role = msg.Role, content = msg.Content });
        }

        messages.Add(new { role = "user", content = prompt });
        return messages;
    }
}
