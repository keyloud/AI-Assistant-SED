using System.Collections.Concurrent;

namespace AssistantApi.Models.Domain;

public class ChatDocumentInfo
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string UploadDate { get; set; } = string.Empty;
    public string Status { get; set; } = "processing";
    public string Size { get; set; } = string.Empty;
    public string UploadedBy { get; set; } = string.Empty;
}

public class ChatSessionState
{
    public string SessionId { get; set; } = string.Empty;
    public string Title { get; set; } = "Новый чат";
    public string Status { get; set; } = "active";
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public bool IsTitleFinalized { get; set; }
    public List<ConversationMessage> Messages { get; set; } = new();
    public List<ChatDocumentInfo> Documents { get; set; } = new();
    public object SyncRoot { get; } = new();
}
