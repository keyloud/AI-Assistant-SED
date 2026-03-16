using AssistantApi.Models.Domain;
using AssistantApi.Models.Enums;

namespace AssistantApi.Services.Interfaces;

public interface IRagService
{
    Task<List<KnowledgeChunk>> SearchAsync(
        string query,
        RequestType requestType,
        int topK = 3,
        CancellationToken ct = default);
}
