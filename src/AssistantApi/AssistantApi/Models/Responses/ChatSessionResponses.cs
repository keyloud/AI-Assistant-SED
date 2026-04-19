using AssistantApi.Models.Domain;

namespace AssistantApi.Models.Responses;

public class ChatSessionSummaryResponse
{
    public string SessionId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string UpdatedAt { get; set; } = string.Empty;
    public string Status { get; set; } = "active";
    public List<ChatDocumentInfo> Documents { get; set; } = new();
}

public class ChatSessionDetailsResponse : ChatSessionSummaryResponse
{
    public List<ConversationMessage> Messages { get; set; } = new();
}
