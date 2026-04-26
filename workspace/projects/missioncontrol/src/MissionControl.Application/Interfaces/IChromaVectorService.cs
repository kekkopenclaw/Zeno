namespace MissionControl.Application.Interfaces;

/// <summary>
/// Abstraction over the Chroma vector database (ChromaDB v2 HTTP API).
///
/// Collection CRN: default_tenant:default_database:mission_memories
/// All API paths use /api/v2/tenants/{tenant}/databases/{database}/collections/...
///
/// Embedding generation: Ollama /api/embeddings endpoint.
/// Gracefully degrades when Chroma is not running.
/// </summary>
public interface IChromaVectorService
{
    /// <summary>
    /// Generates an embedding for <paramref name="text"/> via Ollama and upserts it into
    /// the "mission_memories" Chroma collection.
    /// </summary>
    /// <param name="id">Unique document ID (e.g. "memory_42").</param>
    /// <param name="text">The text content to embed.</param>
    /// <param name="metadata">Optional key/value metadata stored alongside the vector.</param>
    Task EmbedAndStoreAsync(
        string id,
        string text,
        Dictionary<string, string>? metadata = null,
        CancellationToken ct = default);

    /// <summary>
    /// Embeds <paramref name="query"/> and returns the top-<paramref name="topK"/> most similar
    /// documents from "mission_memories", ranked by cosine similarity.
    /// </summary>
    Task<IReadOnlyList<ChromaSearchResult>> SearchAsync(
        string query,
        int topK = 6,
        CancellationToken ct = default);

    /// <summary>Removes a document from the collection by its ID.</summary>
    Task DeleteAsync(string id, CancellationToken ct = default);
}

/// <summary>A single result from a Chroma semantic search.</summary>
public sealed record ChromaSearchResult(
    string Id,
    string Document,
    double Distance,
    IReadOnlyDictionary<string, string> Metadata);
