namespace AssistantApi.Infrastructure.OllamaClient;

/// <summary>
/// Параметры конфигурации для подключения к Ollama.
/// </summary>
public class OllamaOptions
{
    /// <summary>Базовый URL API Ollama.</summary>
    public string BaseUrl { get; set; } = "http://ollama:11434";
    
    /// <summary>Название используемой текстовой модели.</summary>
    public string Model { get; set; } = "qwen2.5:7b";
    
    /// <summary>Название модели для генерации эмбеддингов.</summary>
    public string EmbeddingModel { get; set; } = "bge-m3";
}
