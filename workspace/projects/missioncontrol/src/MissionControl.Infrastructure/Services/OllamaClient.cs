using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using MissionControl.Domain.Interfaces;

namespace MissionControl.Infrastructure.Services;

/// <summary>
/// Direct HTTP client for locally running Ollama (http://127.0.0.1:11434).
/// Supports fast text-in/text-out inference with no tool/function calling.
/// Installed models: llama3, qwen2.5-coder:14b-instruct-q4_K_M.
/// </summary>
public sealed class OllamaClient : IOllamaClient
{
    private readonly HttpClient _http;
    private readonly ILogger<OllamaClient> _logger;

    public OllamaClient(HttpClient http, ILogger<OllamaClient> logger)
    {
        _http   = http;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<string> GenerateAsync(string modelId, string prompt, CancellationToken ct = default)
    {
        var payload = new { model = modelId, prompt, stream = false };

        try
        {
            using var response = await _http.PostAsJsonAsync("/api/generate", payload, ct);
            response.EnsureSuccessStatusCode();

            var stream = await response.Content.ReadAsStreamAsync(ct);
            await using (stream)
            {
                using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
                return doc.RootElement.TryGetProperty("response", out var prop)
                    ? prop.GetString() ?? string.Empty
                    : string.Empty;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Ollama generate failed for model {ModelId}", modelId);
            return string.Empty;
        }
    }

    /// <inheritdoc />
    public async Task<float[]> EmbedAsync(string modelId, string text, CancellationToken ct = default)
    {
        var payload = new { model = modelId, prompt = text };

        try
        {
            using var response = await _http.PostAsJsonAsync("/api/embeddings", payload, ct);
            response.EnsureSuccessStatusCode();

            var stream = await response.Content.ReadAsStreamAsync(ct);
            await using (stream)
            {
                using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
                if (!doc.RootElement.TryGetProperty("embedding", out var arr))
                    return [];

                var floats = new float[arr.GetArrayLength()];
                int i = 0;
                foreach (var el in arr.EnumerateArray())
                    floats[i++] = el.GetSingle();
                return floats;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Ollama embed failed for model {ModelId}", modelId);
            return [];
        }
    }
}
