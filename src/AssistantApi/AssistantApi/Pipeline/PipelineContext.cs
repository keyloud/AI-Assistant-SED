using AssistantApi.Models.Domain;
using AssistantApi.Models.Enums;

namespace AssistantApi.Pipeline;

public class PipelineContext
{
    // Input
    public string SessionId { get; set; } = string.Empty;
    public string UserMessage { get; set; } = string.Empty;
    public string? DocumentId { get; set; }
    public string UserId { get; set; } = string.Empty;
    public List<ConversationMessage> History { get; set; } = new();

    // Document upload (for validation)
    public string? UploadedDocumentText { get; set; }
    public string? UploadedFileName { get; set; }

    // Step 1: Classification
    public RequestType RequestType { get; set; }
    public float ClassificationConfidence { get; set; }

    // Step 2: Context extraction
    public DocumentContext? DocumentContext { get; set; }
    public UserContext? UserContext { get; set; }

    // Step 3: RAG search
    public List<KnowledgeChunk> RagResults { get; set; } = new();

    // Step 4: Prompt building
    public string AugmentedPrompt { get; set; } = string.Empty;

    // Step 5: LLM generation
    public string LlmResponse { get; set; } = string.Empty;

    // Observability
    public Dictionary<string, long> StepDurationsMs { get; set; } = new();
}
