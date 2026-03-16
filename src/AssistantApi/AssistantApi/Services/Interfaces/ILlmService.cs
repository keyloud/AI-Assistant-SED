using AssistantApi.Models.Domain;

namespace AssistantApi.Services.Interfaces;

public interface ILlmService
{
    Task<string> GenerateAsync(
        string prompt,
        List<ConversationMessage>? history = null,
        CancellationToken ct = default);

    IAsyncEnumerable<string> GenerateStreamAsync(
        string prompt,
        List<ConversationMessage>? history = null,
        CancellationToken ct = default);
}
