namespace AssistantApi.Pipeline.Steps;

/// <summary>
/// Phase 5: build context-augmented prompt — the scientific novelty.
/// Selects prompt template by RequestType and injects DocumentContext + RAG results.
/// </summary>
public class PromptBuildStep : IPipelineStep
{
    public Task ExecuteAsync(PipelineContext context, CancellationToken ct = default)
    {
        // TODO Phase 5: call IPromptBuilderService with context and RAG results
        // For now pass the raw user message as the prompt
        context.AugmentedPrompt = context.UserMessage;
        return Task.CompletedTask;
    }
}
