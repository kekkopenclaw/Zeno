using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MissionControl.Application.Interfaces;
using MissionControl.Domain.Interfaces;

namespace MissionControl.Infrastructure.Services;

/// <summary>
/// ChromaMemoryVectorService — ChromaDB v2 semantic vector store.
///
/// Uses the ChromaDB v2 HTTP REST API with the CRN:
///   default_tenant:default_database:mission_memories
///
/// All API paths follow the v2 structure:
///   /api/v2/tenants/{tenant}/databases/{database}/collections
///   /api/v2/tenants/{tenant}/databases/{database}/collections/{id}/upsert
///   /api/v2/tenants/{tenant}/databases/{database}/collections/{id}/query
///   /api/v2/tenants/{tenant}/databases/{database}/collections/{id}/delete
///
/// Embedding generation: Ollama /api/embeddings (same model as Ollama:DefaultModel or Chroma:EmbeddingModel).
/// The collection UUID is resolved on first use via GET and cached for the service lifetime.
///
/// Gracefully degrades — all methods log a warning and no-op when ChromaDB is unavailable.
/// </summary>
public sealed class ChromaMemoryVectorService : IChromaVectorService
{
    private readonly HttpClient _chromaHttp;
    private readonly IOllamaClient _ollama;
    private readonly ILogger<ChromaMemoryVectorService> _logger;
    private readonly string _embeddingModel;
    private readonly string _tenant;
    private readonly string _database;
    private readonly string _collectionName;

    // Cached collection UUID — resolved once via GET, not hard-coded.
    private string? _collectionId;
    private readonly SemaphoreSlim _initLock = new(1, 1);

    /// <summary>Human-readable CRN for log messages.</summary>
    private string CRN => $"{_tenant}:{_database}:{_collectionName}";

    public ChromaMemoryVectorService(
        HttpClient chromaHttp,
        IOllamaClient ollama,
        IConfiguration config,
        ILogger<ChromaMemoryVectorService> logger)
    {
        _chromaHttp     = chromaHttp;
        _ollama         = ollama;
        _logger         = logger;
        _embeddingModel = config["Chroma:EmbeddingModel"] ?? config["Ollama:DefaultModel"] ?? "llama3";
        _tenant         = config["Chroma:Tenant"]         ?? "default_tenant";
        _database       = config["Chroma:Database"]       ?? "default_database";
        _collectionName = config["Chroma:Collection"]     ?? "mission_memories";
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task EmbedAndStoreAsync(
        string id,
        string text,
        Dictionary<string, string>? metadata = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(text)) return;

        try
        {
            var collectionId = await EnsureCollectionAsync(ct);
            if (collectionId is null) return;

            var embedding = await _ollama.EmbedAsync(_embeddingModel, text, ct);
            if (embedding.Length == 0)
            {
                _logger.LogWarning("Chroma EmbedAndStore: empty embedding for id={Id} — skipping.", id);
                return;
            }

            var payload = new
            {
                ids        = new[] { id },
                embeddings = new[] { embedding },
                documents  = new[] { text },
                metadatas  = new[] { metadata ?? new Dictionary<string, string>() }
            };

            var url = CollectionPath(collectionId, "upsert");
            using var response = await _chromaHttp.PostAsJsonAsync(url, payload, ct);

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(ct);
                _logger.LogWarning("Chroma upsert failed CRN={CRN} id={Id}: {Status} {Body}", CRN, id, (int)response.StatusCode, body.Trim());
            }
            else
            {
                _logger.LogDebug("Chroma upsert OK: CRN={CRN} id={Id} ({Dim}-dim)", CRN, id, embedding.Length);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Chroma EmbedAndStore degraded for CRN={CRN} id={Id}", CRN, id);
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ChromaSearchResult>> SearchAsync(
        string query,
        int topK = 6,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(query)) return [];

        try
        {
            var collectionId = await EnsureCollectionAsync(ct);
            if (collectionId is null) return [];

            var queryEmbedding = await _ollama.EmbedAsync(_embeddingModel, query, ct);
            if (queryEmbedding.Length == 0)
            {
                _logger.LogWarning("Chroma search: empty query embedding — returning empty results.");
                return [];
            }

            var payload = new
            {
                query_embeddings = new[] { queryEmbedding },
                n_results        = topK,
                include          = new[] { "documents", "metadatas", "distances" }
            };

            var url = CollectionPath(collectionId, "query");
            using var response = await _chromaHttp.PostAsJsonAsync(url, payload, ct);

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(ct);
                _logger.LogWarning("Chroma query failed CRN={CRN}: {Status} {Body}", CRN, (int)response.StatusCode, body.Trim());
                return [];
            }

            using var stream = await response.Content.ReadAsStreamAsync(ct);
            using var doc    = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
            return ParseQueryResults(doc.RootElement);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Chroma search degraded for CRN={CRN}", CRN);
            return [];
        }
    }

    /// <inheritdoc />
    public async Task DeleteAsync(string id, CancellationToken ct = default)
    {
        try
        {
            var collectionId = await EnsureCollectionAsync(ct);
            if (collectionId is null) return;

            var url     = CollectionPath(collectionId, "delete");
            var payload = new { ids = new[] { id } };
            using var response = await _chromaHttp.PostAsJsonAsync(url, payload, ct);

            if (!response.IsSuccessStatusCode)
                _logger.LogWarning("Chroma delete failed CRN={CRN} id={Id}: {Status}", CRN, id, (int)response.StatusCode);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Chroma delete degraded for CRN={CRN} id={Id}", CRN, id);
        }
    }

    // ── Collection management ─────────────────────────────────────────────────

    /// <summary>
    /// Returns the cached collection UUID, creating the collection if it doesn't exist.
    /// Uses double-checked locking. Returns null when ChromaDB is unreachable.
    /// </summary>
    private async Task<string?> EnsureCollectionAsync(CancellationToken ct)
    {
        if (_collectionId is not null) return _collectionId;

        await _initLock.WaitAsync(ct);
        try
        {
            if (_collectionId is not null) return _collectionId;

            // GET /api/v2/tenants/{tenant}/databases/{database}/collections/{name}
            var getUrl = $"api/v2/tenants/{_tenant}/databases/{_database}/collections/{_collectionName}";
            try
            {
                using var getResp = await _chromaHttp.GetAsync(getUrl, ct);
                if (getResp.IsSuccessStatusCode)
                {
                    using var stream = await getResp.Content.ReadAsStreamAsync(ct);
                    using var doc    = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
                    if (doc.RootElement.TryGetProperty("id", out var idProp))
                    {
                        _collectionId = idProp.GetString();
                        _logger.LogInformation("Chroma: found collection CRN={CRN} uuid={Id}", CRN, _collectionId);
                        return _collectionId;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Chroma is not reachable — vector memory disabled.");
                return null;
            }

            // POST /api/v2/tenants/{tenant}/databases/{database}/collections
            var createUrl = $"api/v2/tenants/{_tenant}/databases/{_database}/collections";
            var createPayload = new
            {
                name          = _collectionName,
                metadata      = new Dictionary<string, string> { ["description"] = "Mission Control agent memories" },
                get_or_create = true,
            };

            try
            {
                using var createResp = await _chromaHttp.PostAsJsonAsync(createUrl, createPayload, ct);
                if (!createResp.IsSuccessStatusCode)
                {
                    var body = await createResp.Content.ReadAsStringAsync(ct);
                    _logger.LogWarning("Chroma create collection failed CRN={CRN}: {Status} {Body}", CRN, (int)createResp.StatusCode, body.Trim());
                    return null;
                }

                using var stream = await createResp.Content.ReadAsStreamAsync(ct);
                using var doc    = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
                if (doc.RootElement.TryGetProperty("id", out var idProp))
                {
                    _collectionId = idProp.GetString();
                    _logger.LogInformation("Chroma: created collection CRN={CRN} uuid={Id}", CRN, _collectionId);
                    return _collectionId;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Chroma create-collection request failed for CRN={CRN}", CRN);
            }

            return null;
        }
        finally
        {
            _initLock.Release();
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>Builds the v2 path for a collection sub-operation (upsert/query/delete).</summary>
    private string CollectionPath(string collectionId, string operation) =>
        $"api/v2/tenants/{_tenant}/databases/{_database}/collections/{collectionId}/{operation}";

    /// <summary>Parses a ChromaDB v2 query response into <see cref="ChromaSearchResult"/> records.</summary>
    private static IReadOnlyList<ChromaSearchResult> ParseQueryResults(JsonElement root)
    {
        var results = new List<ChromaSearchResult>();
        try
        {
            if (!root.TryGetProperty("ids", out var idsOuter) ||
                !root.TryGetProperty("documents", out var docsOuter) ||
                !root.TryGetProperty("distances", out var distOuter))
                return results;

            var ids       = idsOuter.EnumerateArray().FirstOrDefault();
            var documents = docsOuter.EnumerateArray().FirstOrDefault();
            var distances = distOuter.EnumerateArray().FirstOrDefault();
            var metadatas = root.TryGetProperty("metadatas", out var metaOuter)
                ? metaOuter.EnumerateArray().FirstOrDefault()
                : default;

            if (ids.ValueKind == JsonValueKind.Undefined) return results;

            var idArr   = ids.EnumerateArray().ToList();
            var docArr  = documents.ValueKind != JsonValueKind.Undefined ? documents.EnumerateArray().ToList() : new();
            var distArr = distances.ValueKind != JsonValueKind.Undefined ? distances.EnumerateArray().ToList() : new();
            var metaArr = metadatas.ValueKind != JsonValueKind.Undefined ? metadatas.EnumerateArray().ToList() : new();

            for (int i = 0; i < idArr.Count; i++)
            {
                var id       = idArr[i].GetString() ?? string.Empty;
                var document = i < docArr.Count  ? docArr[i].GetString()  ?? string.Empty : string.Empty;
                var distance = i < distArr.Count ? distArr[i].GetDouble() : 0.0;
                var meta     = new Dictionary<string, string>();

                if (i < metaArr.Count && metaArr[i].ValueKind == JsonValueKind.Object)
                    foreach (var prop in metaArr[i].EnumerateObject())
                        meta[prop.Name] = prop.Value.GetString() ?? string.Empty;

                results.Add(new ChromaSearchResult(id, document, distance, meta));
            }
        }
        catch (Exception)
        {
            // Malformed response — return whatever was parsed so far
        }
        return results;
    }
}
