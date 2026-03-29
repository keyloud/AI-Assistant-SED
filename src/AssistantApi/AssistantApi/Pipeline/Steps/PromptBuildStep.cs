using System.Text;
using AssistantApi.Models.Enums;

namespace AssistantApi.Pipeline.Steps;

/// <summary>
/// Phase 5: build context-augmented prompt — the scientific novelty.
/// Selects prompt template by RequestType and injects DocumentContext + RAG results.
/// </summary>
public class PromptBuildStep : IPipelineStep
{
    public Task ExecuteAsync(PipelineContext context, CancellationToken ct = default)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Ты ассистент системы электронного документооборота (СЭД).");
        sb.AppendLine("Помогай пользователям разбираться с документооборотом, инструкциями и регламентами.");
        sb.AppendLine("Отвечай кратко и по делу на русском языке.");

        if (context.RagResults.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("Информация из базы знаний:");
            for (int i = 0; i < context.RagResults.Count; i++)
                sb.AppendLine($"{i + 1}. {context.RagResults[i].Content}");
        }

        if (context.DocumentContext != null)
        {
            sb.AppendLine();
            sb.AppendLine("Контекст документа:");
            sb.AppendLine($"  Тип: {context.DocumentContext.DocumentType}");
            sb.AppendLine($"  Статус: {context.DocumentContext.Status}");
            sb.AppendLine($"  Этап: {context.DocumentContext.Stage}");
        }

        if (context.UserContext != null)
        {
            sb.AppendLine();
            sb.AppendLine($"Пользователь: {context.UserContext.FullName}, роль: {context.UserContext.Role}");
        }

        sb.AppendLine();
        sb.AppendLine($"Вопрос: {context.UserMessage}");

        context.AugmentedPrompt = sb.ToString();
        return Task.CompletedTask;
    }
}
