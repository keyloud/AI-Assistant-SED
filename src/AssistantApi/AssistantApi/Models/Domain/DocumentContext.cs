namespace AssistantApi.Models.Domain;

public class DocumentContext
{
    public string DocumentId { get; set; } = string.Empty;
    public string DocumentType { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Stage { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public string Responsible { get; set; } = string.Empty;
    public BusinessProcess? BusinessProcess { get; set; }
    public Dictionary<string, string> Attributes { get; set; } = new();
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
