namespace AssistantApi.Pipeline.Steps;

/// <summary>
/// Phase 3: extract DocumentContext and UserContext from SED (JSON mock).
/// </summary>
public class ContextExtractionStep : IPipelineStep
{
    public Task ExecuteAsync(PipelineContext context, CancellationToken ct = default)
    {
        // TODO Phase 3: call ISedService to populate DocumentContext and UserContext
        return Task.CompletedTask;
    }
}
