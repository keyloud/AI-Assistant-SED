using AssistantApi.Models.Responses;
using Microsoft.AspNetCore.Mvc;

namespace AssistantApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class HealthController : ControllerBase
{
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

    /// <summary>GET /api/health — checks Ollama, Qdrant, and SED mock connectivity.</summary>
    [HttpGet]
    public async Task<IActionResult> GetHealth(CancellationToken ct)
    {
        var (ollamaStatus, qdrantStatus) = await (
            CheckOllamaAsync(ct),
            CheckQdrantAsync(ct)
        );

        var isHealthy = ollamaStatus.Status == "ok" && qdrantStatus.Status == "ok";

        var response = new HealthResponse
        {
            Status = isHealthy ? "healthy" : "degraded",
            Services = new Dictionary<string, ServiceStatus>
            {
                ["ollama"] = ollamaStatus,
                ["qdrant"] = qdrantStatus,
                ["sed"]    = new ServiceStatus { Status = "ok", Details = "mock mode" }
            },
            Timestamp = DateTime.UtcNow
        };

        return isHealthy ? Ok(response) : StatusCode(503, response);
    }

    private async Task<ServiceStatus> CheckOllamaAsync(CancellationToken ct)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            var client = _httpClientFactory.CreateClient();
            var baseUrl = _configuration["Ollama:BaseUrl"] ?? "http://ollama:11434";
            var response = await client.GetAsync($"{baseUrl}/api/version", ct);
            return new ServiceStatus
            {
                Status = response.IsSuccessStatusCode ? "ok" : "error",
                LatencyMs = sw.ElapsedMilliseconds
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Ollama health check failed: {Message}", ex.Message);
            return new ServiceStatus { Status = "unavailable", LatencyMs = sw.ElapsedMilliseconds };
        }
    }

    private async Task<ServiceStatus> CheckQdrantAsync(CancellationToken ct)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            var client = _httpClientFactory.CreateClient();
            var host = _configuration["Qdrant:Host"] ?? "qdrant";
            var port = _configuration.GetValue<int>("Qdrant:Port", 6333);
            var response = await client.GetAsync($"http://{host}:{port}/healthz", ct);
            return new ServiceStatus
            {
                Status = response.IsSuccessStatusCode ? "ok" : "error",
                LatencyMs = sw.ElapsedMilliseconds
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Qdrant health check failed: {Message}", ex.Message);
            return new ServiceStatus { Status = "unavailable", LatencyMs = sw.ElapsedMilliseconds };
        }
    }
}
