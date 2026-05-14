namespace AssistantApi.Infrastructure.OllamaClient;

/// <summary>
/// Configuration parameters for connecting to Ollama.
/// </summary>
public class OllamaOptions
{
    /// <summary>Base URL of the Ollama API.</summary>
    public string BaseUrl { get; set; } = "http://ollama:11434";

    /// <summary>Name of the chat generation model.</summary>
    public string Model { get; set; } = "qwen2.5:7b";

    /// <summary>Name of the embedding model.</summary>
    public string EmbeddingModel { get; set; } = "bge-m3";

    /// <summary>Temperature for LLM text generation.</summary>
    public double Temperature { get; set; } = 0.7;

    /// <summary>Nucleus sampling parameter (top_p).</summary>
    public double TopP { get; set; } = 0.9;
}
