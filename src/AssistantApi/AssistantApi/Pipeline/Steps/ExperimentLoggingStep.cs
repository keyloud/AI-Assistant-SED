namespace AssistantApi.Pipeline.Steps;

/// <summary>
/// Phase 6: log pipeline run data for A/B experiment comparison (thesis evaluation).
/// </summary>
public class ExperimentLoggingStep : IPipelineStep
{
    public Task ExecuteAsync(PipelineContext context, CancellationToken ct = default)
    {
        // TODO Phase 6: serialize context to JSON and write to experiments/logs/
        return Task.CompletedTask;
    }
}
