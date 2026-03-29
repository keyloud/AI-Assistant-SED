using AssistantApi.Models.Enums;

namespace AssistantApi.Pipeline.Steps;

/// <summary>
/// Phase 2: keyword-based classifier.
/// Phase 5: upgrade to LLM-based classification.
/// Если в контексте есть загруженный документ — устанавливает DocumentValidationQuery.
/// </summary>
public class ClassificationStep : IPipelineStep
{
    public Task ExecuteAsync(PipelineContext context, CancellationToken ct = default)
    {
        // Если загружен файл документа — это запрос на проверку документа
        if (context.UploadedDocumentText != null)
        {
            context.RequestType = RequestType.DocumentValidationQuery;
            context.ClassificationConfidence = 1.0f;
            return Task.CompletedTask;
        }

        var msg = context.UserMessage.ToLowerInvariant();

        if (msg.Contains("как") || msg.Contains("инструкц") || msg.Contains("создать") || msg.Contains("заполнить"))
            context.RequestType = RequestType.InstructionQuery;
        else if (msg.Contains("почему") || msg.Contains("вернул") || msg.Contains("отклонен") || msg.Contains("статус"))
            context.RequestType = RequestType.BusinessProcessQuery;
        else if (msg.Contains("найди") || msg.Contains("найти") || msg.Contains("поиск") || msg.Contains("список"))
            context.RequestType = RequestType.DocumentSearchQuery;
        else if (msg.Contains("ошибка") || msg.Contains("не могу") || msg.Contains("не работает") || msg.Contains("проблема"))
            context.RequestType = RequestType.ErrorAnalysisQuery;
        else
            context.RequestType = RequestType.GeneralQuery;

        context.ClassificationConfidence = 1.0f;
        return Task.CompletedTask;
    }
}
