using AssistantApi.Models.Enums;

namespace AssistantApi.Pipeline.Steps;

/// <summary>
/// MVP placeholder for LLM generation.
/// </summary>
public class LlmGenerationStep : IPipelineStep
{
    public Task ExecuteAsync(PipelineContext context, CancellationToken ct = default)
    {
        if (context.RequestType == RequestType.DocumentValidationQuery)
        {
            context.ValidationRemarks =
            [
                "MVP placeholder: проверка обязательных реквизитов пока не подключена.",
                "Следующий шаг: связать extraction текста и сравнение с эталонным шаблоном."
            ];

            context.LlmResponse = "Сформирован предварительный список замечаний.";
            return Task.CompletedTask;
        }

        context.LlmResponse = "[MVP placeholder: LLM generation for KB Q&A is not connected yet]";
        return Task.CompletedTask;
    }
}
