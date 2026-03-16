using AssistantApi.Models.Domain;

namespace AssistantApi.Models.Requests;

public class ChatRequest
{
    public string SessionId { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? DocumentId { get; set; }
    public string UserId { get; set; } = "user-001";
    public List<ConversationMessage> ConversationHistory { get; set; } = new();
}
