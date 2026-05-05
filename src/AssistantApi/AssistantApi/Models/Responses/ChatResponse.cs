namespace AssistantApi.Models.Responses;

public class ChatResponse
{
    public string SessionId { get; set; } = string.Empty;
    public string ChatTitle { get; set; } = string.Empty;
    public string UpdatedAt { get; set; } = string.Empty;
    public string Response { get; set; } = string.Empty;
    public string RequestType { get; set; } = string.Empty;
    public float ClassificationConfidence { get; set; }
    public List<string> ValidationRemarks { get; set; } = new();
    public List<RagSource> RagSources { get; set; } = new();
    public Dictionary<string, long> PipelineTrace { get; set; } = new();
}

public class RagSource
{
    public string Title { get; set; } = string.Empty;
    public string SourceFile { get; set; } = string.Empty;
    public string Section { get; set; } = string.Empty;
    public float Score { get; set; }
}
