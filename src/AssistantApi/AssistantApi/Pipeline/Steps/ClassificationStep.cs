using AssistantApi.Models.Enums;

namespace AssistantApi.Pipeline.Steps;

/// <summary>
/// Phase 2: classify request type by keywords.
/// Phase 5: upgrade to LLM-based classification.
/// </summary>
public class ClassificationStep : IPipelineStep
{
    public Task ExecuteAsync(PipelineContext context, CancellationToken ct = default)
    {
        // TODO Phase 2: keyword-based classifier
        context.RequestType = RequestType.GeneralQuery;
        context.ClassificationConfidence = 1.0f;
        return Task.CompletedTask;
    }
}
