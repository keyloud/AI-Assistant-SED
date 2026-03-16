namespace AssistantApi.Pipeline.Steps;

public interface IPipelineStep
{
    Task ExecuteAsync(PipelineContext context, CancellationToken ct = default);
}
