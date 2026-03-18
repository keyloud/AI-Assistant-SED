namespace AssistantApi.Infrastructure.QdrantClient;

/// <summary>
/// Параметры конфигурации для подключения к векторной базе данных Qdrant.
/// </summary>
public class QdrantOptions
{
    /// <summary>Хост сервера Qdrant.</summary>
    public string Host { get; set; } = "qdrant";
    
    /// <summary>Порт сервера Qdrant.</summary>
    public int Port { get; set; } = 6333;
    
    /// <summary>Название коллекции векторного поиска.</summary>
    public string CollectionName { get; set; } = "knowledge_base";
}
