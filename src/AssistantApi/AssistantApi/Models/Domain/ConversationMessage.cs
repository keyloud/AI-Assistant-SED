namespace AssistantApi.Models.Domain;

public class ConversationMessage
{
    public string Role { get; set; } = string.Empty;  // "user" | "assistant"
    public string Content { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
