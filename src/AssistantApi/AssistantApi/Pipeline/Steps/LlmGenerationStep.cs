using AssistantApi.Services.Interfaces;

namespace AssistantApi.Pipeline.Steps;

/// <summary>
/// Phase 2: send AugmentedPrompt to Ollama and populate LlmResponse.
/// </summary>
public class LlmGenerationStep : IPipelineStep
{
    private readonly ILlmService _llmService;
    private readonly ILogger<LlmGenerationStep> _logger;

    public LlmGenerationStep(ILlmService llmService, ILogger<LlmGenerationStep> logger)
    {
        _llmService = llmService;
        _logger = logger;
    }

    public async Task ExecuteAsync(PipelineContext context, CancellationToken ct = default)
    {
        try
        {
            context.LlmResponse = await _llmService.GenerateAsync(
                context.AugmentedPrompt,
                context.History,
                ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "LLM generation failed, returning fallback response");
            context.LlmResponse = "Сервис LLM временно недоступен. Убедитесь, что Ollama запущена.";
        }
    }
}
