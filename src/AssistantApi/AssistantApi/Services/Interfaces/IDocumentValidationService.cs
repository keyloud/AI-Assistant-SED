using AssistantApi.Models.Responses;

namespace AssistantApi.Services.Interfaces;

public interface IDocumentValidationService
{
    Task<DocumentValidationResponse> ValidateAsync(
        Stream fileStream,
        string fileName,
        string? documentTypeHint,
        bool summaryOnly,
        CancellationToken ct = default);
}
