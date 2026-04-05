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

        context.AugmentedPrompt = $"""
            Ты ассистент СЭД. Ответь на вопрос пользователя по базе знаний кратко и по делу.

            Вопрос пользователя:
            {context.UserMessage}
            """;
        return Task.CompletedTask;
    }
}
