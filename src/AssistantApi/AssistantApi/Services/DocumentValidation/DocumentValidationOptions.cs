namespace AssistantApi.Services.DocumentValidation;

public class DocumentValidationOptions
{
    public const string SectionName = "DocumentValidation";

    public string TemplateNotFoundMessage { get; set; } = "Эталонный шаблон не найден. Обратитесь к специалисту.";
    public string UnsupportedFormatMessage { get; set; } = "Поддерживаются только форматы DOCX и PDF.";
    public string EmptyFileMessage { get; set; } = "Файл не передан.";
    public string TextExtractionFailedMessage { get; set; } = "Не удалось извлечь текст из файла. Проверьте качество файла или формат содержимого.";
    public string PdfOcrRecommendationMessage { get; set; } = "Для PDF-сканов требуется OCR-модуль. Подключите OCR и повторите проверку.";
    public string OcrFailedMessage { get; set; } = "OCR не смог распознать текст из PDF. Требуется проверка качества скана.";

    public DocumentValidationOcrOptions Ocr { get; set; } = new();
    public DocumentValidationMlOptions Ml { get; set; } = new();

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

public class DocumentValidationOcrOptions
{
    public bool Enabled { get; set; }
    public string Command { get; set; } = "ocrmypdf";
    public string ArgumentsTemplate { get; set; } = "--force-ocr --skip-text --language {lang} {input} {output}";
    public string Language { get; set; } = "rus+eng";
    public int TimeoutSeconds { get; set; } = 180;
    public int MinExtractedTextLength { get; set; } = 120;
}

public class DocumentValidationMlOptions
{
    public bool Enabled { get; set; } = true;
    public float ConfidenceThreshold { get; set; } = 0.55f;
    public bool EnableSummary { get; set; } = true;
    public int MaxInputChars { get; set; } = 4500;
}

public class DocumentTemplateRule
{
    public string DisplayName { get; set; } = string.Empty;
    public List<string> DetectionKeywords { get; set; } = [];
    public List<string> RequiredKeywords { get; set; } = [];
}
