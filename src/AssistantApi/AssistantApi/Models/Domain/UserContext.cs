namespace AssistantApi.Models.Domain;

public class UserContext
{
    public string UserId { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;
    public List<string> Permissions { get; set; } = new();
    public string? CurrentDocumentId { get; set; }
}
