namespace AssistantApi.Services.DocumentValidation;

public class DocumentValidationOptions
{
    public const string SectionName = "DocumentValidation";

    public string TemplateNotFoundMessage { get; set; } = "Эталонный шаблон не найден. Обратитесь к специалисту.";
    public string UnsupportedFormatMessage { get; set; } = "Поддерживаются только форматы DOCX и PDF.";
    public string EmptyFileMessage { get; set; } = "Файл не передан.";
    public string TextExtractionFailedMessage { get; set; } = "Не удалось извлечь текст из файла. Проверьте качество файла или формат содержимого.";
    public string PdfOcrRecommendationMessage { get; set; } = "Для PDF-сканов требуется OCR-модуль. Подключите OCR и повторите проверку.";

    public List<DocumentTemplateRule> Templates { get; set; } =
    [
        new DocumentTemplateRule
        {
            DisplayName = "приказ",
            DetectionKeywords = ["приказ"],
            RequiredKeywords = ["приказ", "номер", "дата", "основан", "подпись"]
        },
        new DocumentTemplateRule
        {
            DisplayName = "доверенность",
            DetectionKeywords = ["доверенн", "доверител", "представител"],
            RequiredKeywords = ["доверенность", "доверител", "представител", "паспорт", "дата", "подпись"]
        },
        new DocumentTemplateRule
        {
            DisplayName = "договор",
            DetectionKeywords = ["договор", "сторон", "предмет"],
            RequiredKeywords = ["договор", "предмет", "сторон", "срок", "подпись"]
        }
    ];
}

public class DocumentTemplateRule
{
    public string DisplayName { get; set; } = string.Empty;
    public List<string> DetectionKeywords { get; set; } = [];
    public List<string> RequiredKeywords { get; set; } = [];
}
