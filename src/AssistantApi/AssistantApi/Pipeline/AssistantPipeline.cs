using System.Diagnostics;
using AssistantApi.Pipeline.Steps;

namespace AssistantApi.Pipeline;

public class AssistantPipeline
{
    private readonly IEnumerable<IPipelineStep> _steps;
    private readonly ILogger<AssistantPipeline> _logger;

    public AssistantPipeline(IEnumerable<IPipelineStep> steps, ILogger<AssistantPipeline> logger)
    {
        _steps = steps;
        _logger = logger;
    }

    public async Task<PipelineContext> ExecuteAsync(PipelineContext context, CancellationToken ct = default)
    {
        foreach (var step in _steps)
        {
            var sw = Stopwatch.StartNew();
            await step.ExecuteAsync(context, ct);
            sw.Stop();
            context.StepDurationsMs[step.GetType().Name] = sw.ElapsedMilliseconds;
            _logger.LogDebug("Step {Step} completed in {Ms}ms", step.GetType().Name, sw.ElapsedMilliseconds);
        }
        return context;
    }
}
