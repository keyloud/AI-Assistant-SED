using AssistantApi.Models.Domain;

namespace AssistantApi.Services.Interfaces;

public interface IRagService
{
    Task<List<KnowledgeChunk>> SearchAsync(
        string query,
        int topK = 3,
        CancellationToken ct = default);
}
