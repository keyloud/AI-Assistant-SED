namespace AssistantApi.Models.Enums;

public enum RequestType
{
    InstructionQuery,           // "Как создать документ?"
    BusinessProcessQuery,       // "Почему документ вернулся?"
    DocumentSearchQuery,        // "Найди договоры от января"
    ErrorAnalysisQuery,         // "Почему не могу подписать?"
    DocumentValidationQuery,    // Загружен файл документа для проверки
    GeneralQuery                // Fallback
}
