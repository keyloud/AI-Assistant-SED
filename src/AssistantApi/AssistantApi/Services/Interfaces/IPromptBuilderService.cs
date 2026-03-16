using AssistantApi.Models.Domain;
using AssistantApi.Models.Enums;

namespace AssistantApi.Services.Interfaces;

public interface IPromptBuilderService
{
    string BuildPrompt(
        string userMessage,
        RequestType requestType,
        DocumentContext? documentContext,
        UserContext? userContext,
        List<KnowledgeChunk> ragResults);
}
