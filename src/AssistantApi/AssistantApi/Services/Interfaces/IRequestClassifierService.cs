using AssistantApi.Models.Domain;
using AssistantApi.Models.Enums;

namespace AssistantApi.Services.Interfaces;

public interface IRequestClassifierService
{
    Task<(RequestType Type, float Confidence)> ClassifyAsync(
        string userMessage,
        DocumentContext? documentContext,
        CancellationToken ct = default);
}
