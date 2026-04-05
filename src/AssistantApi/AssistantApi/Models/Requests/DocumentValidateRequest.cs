using Microsoft.AspNetCore.Http;

namespace AssistantApi.Models.Requests;

public class DocumentValidateRequest
{
    public IFormFile? File { get; set; }
    public string? DocumentTypeHint { get; set; }
}
