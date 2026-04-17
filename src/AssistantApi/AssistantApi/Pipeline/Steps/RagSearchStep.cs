using AssistantApi.Services.Interfaces;

namespace AssistantApi.Pipeline.Steps;

/// <summary>
/// MVP placeholder for RAG search.
/// </summary>
public class RagSearchStep : IPipelineStep
{
    private readonly IRagService _ragService;

    public RagSearchStep(IRagService ragService)
    {
        _ragService = ragService;
    }

    public async Task ExecuteAsync(PipelineContext context, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(context.UserMessage))
        {
            context.RagResults.Clear();
            return;
        }

        context.RagResults = await _ragService.SearchAsync(context.UserMessage, topK: 3, ct);
    }
}
