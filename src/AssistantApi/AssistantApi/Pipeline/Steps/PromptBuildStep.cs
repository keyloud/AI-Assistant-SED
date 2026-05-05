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
            ? "Контекст по запросу в базе знаний не найден."
            : string.Join("\n\n", context.RagResults
                .Where(chunk => !string.IsNullOrWhiteSpace(chunk.Content))
                .Select(chunk =>
                    $"""
                    Документ: {(string.IsNullOrWhiteSpace(chunk.DocumentTitle) ? chunk.SourceFile : chunk.DocumentTitle)}
                    Раздел: {chunk.Section}
                    Файл: {chunk.SourceFile}
                    Текст:
                    {chunk.Content}
                    """.Trim()));

        context.AugmentedPrompt = $"""
            Ты ассистент СЭД. Ответь на вопрос пользователя по базе знаний кратко и по делу.

            Инструкции к ответу:
            - Отвечай сразу по сути, без вступлений и без рассуждений о том, как ты отвечаешь.
            - Не используй формулировки про "релевантные источники".
            - Не вставляй в ответ маркеры вида (Источник 1), (Источник 2) и т.п.
            - Не используй слово "источник" в тексте ответа.
            - Если в контексте ниже нет точного ответа, скажи: "В базе знаний нет точного ответа по этому вопросу."

            Контекст базы знаний:
            {ragContext}

            Вопрос пользователя:
            {context.UserMessage}
            """;
        return Task.CompletedTask;
    }
}
