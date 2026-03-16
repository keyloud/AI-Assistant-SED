namespace AssistantApi.Infrastructure.QdrantClient;

public class QdrantOptions
{
    public string Host { get; set; } = "qdrant";
    public int Port { get; set; } = 6333;
    public string CollectionName { get; set; } = "knowledge_base";
}
