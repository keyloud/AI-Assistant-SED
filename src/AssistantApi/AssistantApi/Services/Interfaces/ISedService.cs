using AssistantApi.Models.Domain;

namespace AssistantApi.Services.Interfaces;

public interface ISedService
{
    Task<DocumentContext?> GetDocumentContextAsync(string documentId, CancellationToken ct = default);
    Task<UserContext?> GetUserContextAsync(string userId, CancellationToken ct = default);
    Task<List<DocumentContext>> SearchDocumentsAsync(string query, string userId, CancellationToken ct = default);
}
