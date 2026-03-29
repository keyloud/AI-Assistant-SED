using AssistantApi.Models.Domain;

namespace AssistantApi.Models.Responses;

public class ValidateDocumentResponse
{
    public string SessionId { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;

    /// <summary>Превью извлечённого текста (первые 500 символов).</summary>
    public string ExtractedTextPreview { get; set; } = string.Empty;

    public DocumentValidationResult ValidationResult { get; set; } = new();
    public Dictionary<string, long> PipelineTrace { get; set; } = new();
}
