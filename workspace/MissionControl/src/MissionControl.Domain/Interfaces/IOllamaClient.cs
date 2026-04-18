namespace MissionControl.Domain.Interfaces;

/// <summary>
/// Provides direct text-in/text-out inference against a locally running Ollama instance
/// (http://127.0.0.1:11434).  No tool/function calling; for fast local LLM inference only.
/// </summary>
public interface IOllamaClient
{
    /// <summary>
    /// Sends <paramref name="prompt"/> to <paramref name="modelId"/> and returns the generated text.
    /// Supported model IDs: "llama3", "qwen2.5-coder:14b-instruct-q4_K_M".
    /// </summary>
    Task<string> GenerateAsync(string modelId, string prompt, CancellationToken ct = default);

    /// <summary>
    /// Generates a dense float embedding vector for <paramref name="text"/> using the given model.
    /// Calls POST /api/embeddings.  Returns an empty array on failure.
    /// </summary>
    Task<float[]> EmbedAsync(string modelId, string text, CancellationToken ct = default);
}
