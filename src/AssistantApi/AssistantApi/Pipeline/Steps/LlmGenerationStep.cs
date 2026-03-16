namespace AssistantApi.Pipeline.Steps;

/// <summary>
/// Phase 2: send AugmentedPrompt to Ollama and populate LlmResponse.
/// </summary>
public class LlmGenerationStep : IPipelineStep
{
    public Task ExecuteAsync(PipelineContext context, CancellationToken ct = default)
    {
        // TODO Phase 2: call ILlmService.GenerateAsync with AugmentedPrompt and History
        context.LlmResponse = "[Phase 2: LLM not yet connected]";
        return Task.CompletedTask;
    }
}
