using AssistantApi.Models.Domain;

namespace AssistantApi.Models.Requests;

public class ChatRequest
{
    public string SessionId { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? AttachedFileName { get; set; }
    public string? AttachedFileContent { get; set; }
    public List<ConversationMessage> ConversationHistory { get; set; } = new();
}
