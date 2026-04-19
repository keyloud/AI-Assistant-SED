using AssistantApi.Models.Responses;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using System.Net.Http.Headers;

namespace AssistantApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class HealthController : ControllerBase
{
    private const string HealthyStatus = "healthy";
    private const string DegradedStatus = "degraded";
    private const string OkStatus = "ok";
    private const string ErrorStatus = "error";
    private const string UnavailableStatus = "unavailable";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<HealthController> _logger;

    public HealthController(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<HealthController> logger)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _logger = logger;
    }

    /// <summary>GET /api/health - checks Ollama and Qdrant connectivity.</summary>
    [HttpGet]
    public async Task<IActionResult> GetHealth(CancellationToken ct)
    {
        var ollamaTask = CheckOllamaAsync(ct);
        var qdrantTask = CheckQdrantAsync(ct);

        await Task.WhenAll(ollamaTask, qdrantTask);

        var ollamaStatus = ollamaTask.Result;
        var qdrantStatus = qdrantTask.Result;

        var isHealthy = ollamaStatus.Status == OkStatus && qdrantStatus.Status == OkStatus;

        var response = new HealthResponse
        {
            Status = isHealthy ? HealthyStatus : DegradedStatus,
            Services = new Dictionary<string, ServiceStatus>
            {
                ["ollama"] = ollamaStatus,
                ["qdrant"] = qdrantStatus
            },
            Timestamp = DateTime.UtcNow
        };

        return isHealthy ? Ok(response) : StatusCode(StatusCodes.Status503ServiceUnavailable, response);
    }

    private Task<ServiceStatus> CheckOllamaAsync(CancellationToken ct)
    {
        var baseUrl = _configuration["Ollama:BaseUrl"] ?? "http://ollama:11434";
        return CheckServiceAsync("ollama", $"{baseUrl.TrimEnd('/')}/api/version", ct);
    }

    private Task<ServiceStatus> CheckQdrantAsync(CancellationToken ct)
    {
        var host = _configuration["Qdrant:Host"] ?? "qdrant";
        var port = _configuration.GetValue<int>("Qdrant:Port", 6333);
        return CheckServiceAsync("qdrant", $"http://{host}:{port}/healthz", ct);
    }

    private async Task<ServiceStatus> CheckServiceAsync(string serviceName, string url, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();

        try
        {
            var client = _httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(5);

            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.CacheControl = new CacheControlHeaderValue { NoCache = true };

            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
            return new ServiceStatus
            {
                Status = response.IsSuccessStatusCode ? OkStatus : ErrorStatus,
                LatencyMs = sw.ElapsedMilliseconds,
                Details = response.IsSuccessStatusCode
                    ? null
                    : $"HTTP {(int)response.StatusCode} ({response.ReasonPhrase})"
            };
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            _logger.LogWarning("Таймаут проверки здоровья сервиса {ServiceName}", serviceName);
            return new ServiceStatus
            {
                Status = UnavailableStatus,
                LatencyMs = sw.ElapsedMilliseconds,
                Details = "Request timed out"
            };
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Проверка здоровья сервиса {ServiceName} завершилась ошибкой", serviceName);
            return new ServiceStatus
            {
                Status = UnavailableStatus,
                LatencyMs = sw.ElapsedMilliseconds,
                Details = ex.Message
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Непредвиденная ошибка во время проверки здоровья сервиса {ServiceName}", serviceName);
            return new ServiceStatus
            {
                Status = UnavailableStatus,
                LatencyMs = sw.ElapsedMilliseconds,
                Details = "Unexpected error"
            };
        }
    }
}
