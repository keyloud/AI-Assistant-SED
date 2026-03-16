namespace AssistantApi.Models.Responses;

public class ChatResponse
{
    public string SessionId { get; set; } = string.Empty;
    public string Response { get; set; } = string.Empty;
    public string RequestType { get; set; } = string.Empty;
    public float ClassificationConfidence { get; set; }
    public ContextUsed? ContextUsed { get; set; }
    public Dictionary<string, long> PipelineTrace { get; set; } = new();
}

public class ContextUsed
{
    public string? DocumentType { get; set; }
    public string? DocumentStatus { get; set; }
    public string? UserRole { get; set; }
    public int RagChunksUsed { get; set; }
    public List<RagSource> RagSources { get; set; } = new();
}

public class RagSource
{
    public string Title { get; set; } = string.Empty;
    public string Section { get; set; } = string.Empty;
    public float Score { get; set; }
}
