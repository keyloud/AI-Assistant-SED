namespace AssistantApi.Pipeline.Steps;

/// <summary>
/// MVP placeholder for RAG search.
/// </summary>
public class RagSearchStep : IPipelineStep
{
    public Task ExecuteAsync(PipelineContext context, CancellationToken ct = default)
    {
        // RAG integration will populate RagResults in the next implementation step.
        context.RagResults.Clear();
        return Task.CompletedTask;
    }
}
