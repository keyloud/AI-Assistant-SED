namespace AssistantApi.Models.Domain;

public class KnowledgeChunk
{
    public string Id { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string SourceFile { get; set; } = string.Empty;
    public string DocumentTitle { get; set; } = string.Empty;
    public string Section { get; set; } = string.Empty;
    public float RelevanceScore { get; set; }
    public Dictionary<string, string> Metadata { get; set; } = new();
}
