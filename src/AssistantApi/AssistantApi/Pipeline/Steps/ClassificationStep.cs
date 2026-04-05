using AssistantApi.Models.Enums;

namespace AssistantApi.Pipeline.Steps;

/// <summary>
/// MVP classifier: decides between KB Q&A and document validation.
/// </summary>
public class ClassificationStep : IPipelineStep
{
    private static readonly string[] ValidationKeywords =
    [
        "проверь", "провер", "реквизит", "документ", "договор", "доверенность", "приказ",
        "ошибк", "исправ", "pdf", "docx", "файл", "влож"
    ];

    public Task ExecuteAsync(PipelineContext context, CancellationToken ct = default)
    {
        var message = (context.UserMessage ?? string.Empty).ToLowerInvariant();
        var fileName = (context.AttachedFileName ?? string.Empty).ToLowerInvariant();

        var hasAttachment = !string.IsNullOrWhiteSpace(context.AttachedFileName)
            || !string.IsNullOrWhiteSpace(context.AttachedFileContent);

        var asksForValidation = ValidationKeywords.Any(message.Contains)
            || ValidationKeywords.Any(fileName.Contains);

        if (hasAttachment || asksForValidation)
        {
            context.RequestType = RequestType.DocumentValidationQuery;
            context.ClassificationConfidence = hasAttachment ? 0.95f : 0.80f;
            return Task.CompletedTask;
        }

        context.RequestType = RequestType.KnowledgeBaseQuery;
        context.ClassificationConfidence = 0.80f;
        return Task.CompletedTask;
    }
}
