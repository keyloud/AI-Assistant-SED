namespace AssistantApi.Models.Requests;

public class AttachDocumentToChatRequest
{
    public string Name { get; set; } = string.Empty;
    public string UploadDate { get; set; } = string.Empty;
    public string Status { get; set; } = "processing";
    public string Size { get; set; } = string.Empty;
    public string UploadedBy { get; set; } = string.Empty;
}
