using AssistantApi.Models.Enums;

namespace AssistantApi.Pipeline.Steps;

/// <summary>
/// Builds a focused prompt for the two MVP scenarios.
/// </summary>
public class PromptBuildStep : IPipelineStep
{
    public Task ExecuteAsync(PipelineContext context, CancellationToken ct = default)
    {
        if (context.RequestType == RequestType.DocumentValidationQuery)
        {
            context.AugmentedPrompt = $"""
                Ты ассистент СЭД. Проверь документ только по обязательным реквизитам.
                Типы документов в MVP: приказ, доверенность, договор.
                Если эталонный шаблон не найден, сообщи: "Эталонный шаблон не найден. Обратитесь к специалисту.".
                Верни результат как список замечаний.

                Сообщение пользователя:
                {context.UserMessage}

                Имя файла:
                {context.AttachedFileName}

                Текст документа:
                {context.AttachedFileContent}
                """;
            return Task.CompletedTask;
        }

        var ragContext = context.RagResults.Count == 0
            ? "Релевантные источники не найдены."
            : string.Join("\n\n", context.RagResults.Select((chunk, index) =>
                $"Источник {index + 1}: {chunk.DocumentTitle} {chunk.Section}\n{chunk.Content}".Trim()));

        context.AugmentedPrompt = $"""
            Ты ассистент СЭД. Ответь на вопрос пользователя по базе знаний кратко и по делу.

            Используй только релевантные источники ниже. Если источников нет, отвечай осторожно и не выдумывай факты.

            Источники:
            {ragContext}

            Вопрос пользователя:
            {context.UserMessage}
            """;
        return Task.CompletedTask;
    }
}
