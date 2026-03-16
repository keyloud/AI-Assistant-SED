namespace AssistantApi.Infrastructure.OllamaClient;

public class OllamaOptions
{
    public string BaseUrl { get; set; } = "http://ollama:11434";
    public string Model { get; set; } = "qwen2.5:7b";
    public string EmbeddingModel { get; set; } = "nomic-embed-text";
}
