namespace AssistantApi.Models.Domain;

public class DocumentValidationResult
{
    /// <summary>Тип документа, определённый ассистентом.</summary>
    public string DetectedDocumentType { get; set; } = string.Empty;

    /// <summary>Общий вывод о корректности документа.</summary>
    public string ValidationSummary { get; set; } = string.Empty;

    /// <summary>Список обнаруженных проблем.</summary>
    public List<string> Issues { get; set; } = new();

    /// <summary>Рекомендации по исправлению.</summary>
    public List<string> Recommendations { get; set; } = new();

    /// <summary>Признак корректности документа.</summary>
    public bool IsValid { get; set; }
}
