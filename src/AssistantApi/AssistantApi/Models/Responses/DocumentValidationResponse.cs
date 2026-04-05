namespace AssistantApi.Models.Responses;

public class DocumentValidationResponse
{
    public string Status { get; set; } = string.Empty;
    public string? DocumentType { get; set; }
    public int ExtractedTextLength { get; set; }
    public List<string> Remarks { get; set; } = new();
}
