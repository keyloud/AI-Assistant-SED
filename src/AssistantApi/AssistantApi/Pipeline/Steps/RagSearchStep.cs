namespace AssistantApi.Pipeline.Steps;

/// <summary>
/// Phase 4: search Qdrant for relevant knowledge chunks.
/// </summary>
public class RagSearchStep : IPipelineStep
{
    public Task ExecuteAsync(PipelineContext context, CancellationToken ct = default)
    {
        // TODO Phase 4: embed UserMessage via Ollama, search Qdrant, populate RagResults
        return Task.CompletedTask;
    }
}
