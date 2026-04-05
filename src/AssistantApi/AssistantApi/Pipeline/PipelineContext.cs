using AssistantApi.Models.Domain;
using AssistantApi.Models.Enums;

namespace AssistantApi.Pipeline;

public class PipelineContext
{
    // Input
    public string SessionId { get; set; } = string.Empty;
    public string UserMessage { get; set; } = string.Empty;
    public string? AttachedFileName { get; set; }
    public string? AttachedFileContent { get; set; }
    public List<ConversationMessage> History { get; set; } = new();

    // Step 1: Classification
    public RequestType RequestType { get; set; }
    public float ClassificationConfidence { get; set; }

    // Step 2: RAG search
    public List<KnowledgeChunk> RagResults { get; set; } = new();

    // Step 3: Prompt building
    public string AugmentedPrompt { get; set; } = string.Empty;

    // Step 4: LLM generation / rule-based remarks
    public string LlmResponse { get; set; } = string.Empty;
    public List<string> ValidationRemarks { get; set; } = new();

    // Observability
    public Dictionary<string, long> StepDurationsMs { get; set; } = new();
}
