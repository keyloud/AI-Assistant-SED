namespace AssistantApi.Models.Responses;

public class HealthResponse
{
    public string Status { get; set; } = string.Empty;
    public Dictionary<string, ServiceStatus> Services { get; set; } = new();
    public DateTime Timestamp { get; set; }
}

public class ServiceStatus
{
    public string Status { get; set; } = string.Empty;
    public long LatencyMs { get; set; }
    public string? Details { get; set; }
}
