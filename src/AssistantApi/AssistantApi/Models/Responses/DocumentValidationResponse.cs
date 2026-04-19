namespace AssistantApi.Models.Responses;

public class DocumentValidationResponse
{
    public string Status { get; set; } = string.Empty;
    public string? DocumentType { get; set; }
    public float? ClassificationConfidence { get; set; }
    public bool OcrUsed { get; set; }
    public int ExtractedTextLength { get; set; }
    public string? ExtractedText { get; set; }
    public string? Summary { get; set; }
    public List<string> Recommendations { get; set; } = new();
    public List<string> Remarks { get; set; } = new();
}
