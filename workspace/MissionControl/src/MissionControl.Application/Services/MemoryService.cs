using MissionControl.Application.DTOs;
using MissionControl.Application.Interfaces;
using MissionControl.Domain.Entities;
using MissionControl.Domain.Enums;
using MissionControl.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace MissionControl.Application.Services;

public class MemoryService
{
    private readonly IMemoryRepository _repository;
    private readonly IChromaVectorService _chroma;
    private readonly ILogger<MemoryService> _logger;

    public MemoryService(
        IMemoryRepository repository,
        IChromaVectorService chroma,
        ILogger<MemoryService> logger)
    {
        _repository = repository;
        _chroma     = chroma;
        _logger     = logger;
    }

    public async Task<IReadOnlyList<MemoryEntryDto>> GetAllAsync()
    {
        var entries = await _repository.GetAllAsync();
        return entries.Select(MapToDto).ToList();
    }

    public async Task<IReadOnlyList<MemoryEntryDto>> GetByProjectIdAsync(int projectId)
    {
        var entries = await _repository.GetByProjectIdAsync(projectId);
        return entries.Select(MapToDto).ToList();
    }

    public async Task<IReadOnlyList<MemoryEntryDto>> SearchAsync(int projectId, string query)
    {
        var entries = await _repository.SearchAsync(projectId, query);
        return entries.Select(MapToDto).ToList();
    }

    /// <summary>
    /// Semantic similarity search via ChromaDB v2 (CRN: default_tenant:default_database:mission_memories).
    /// Falls back to SQLite text search when ChromaDB is unavailable.
    /// </summary>
    public async Task<IReadOnlyList<MemoryEntryDto>> SemanticSearchAsync(
        int projectId,
        string query,
        int nResults = 10,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(query))
            return await GetByProjectIdAsync(projectId);

        try
        {
            var chromaResults = await _chroma.SearchAsync(query, nResults, ct);
            if (chromaResults.Count == 0)
                return await SearchAsync(projectId, query);

            var chromaList = chromaResults.ToList();

            // Map Chroma IDs back to MemoryEntry rows (filter to this project)
            var ids = chromaList
                .Select(r => int.TryParse(r.Id, out var id) ? (int?)id : null)
                .Where(id => id.HasValue)
                .Select(id => id!.Value)
                .ToHashSet();

            var entries = await _repository.GetByProjectIdAsync(projectId);
            var ordered = entries
                .Where(e => ids.Contains(e.Id))
                .OrderBy(e =>
                {
                    var rank = chromaList.FindIndex(r => r.Id == e.Id.ToString());
                    return rank < 0 ? int.MaxValue : rank;
                })
                .ToList();

            return ordered.Select(MapToDto).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Semantic search degraded — falling back to SQLite text search");
            return await SearchAsync(projectId, query);
        }
    }

    public async Task<MemoryEntryDto?> GetByIdAsync(int id)
    {
        var entry = await _repository.GetByIdAsync(id);
        return entry == null ? null : MapToDto(entry);
    }

    public async Task<MemoryEntryDto> CreateAsync(CreateMemoryEntryDto dto)
    {
        var entry = new MemoryEntry
        {
            ProjectId = dto.ProjectId,
            Title     = dto.Title,
            Content   = dto.Content,
            Type      = Enum.TryParse<MemoryType>(dto.Type, out var type) ? type : MemoryType.LongTerm,
            Tags      = dto.Tags,
            AgentId   = dto.AgentId,
            CreatedAt = DateTime.UtcNow
        };
        var created = await _repository.AddAsync(entry);
        var result  = MapToDto(created);

        // Upsert into ChromaDB v2 (fire-and-forget; failure logged as warning, not thrown).
        // ChromaMemoryVectorService already catches all exceptions internally;
        // the outer try/catch guards against any unexpected task-level faults.
        _ = Task.Run(async () =>
        {
            try
            {
                var metadata = new Dictionary<string, string>
                {
                    ["projectId"] = created.ProjectId.ToString(),
                    ["type"]      = created.Type.ToString(),
                    ["title"]     = created.Title,
                    ["tags"]      = created.Tags ?? string.Empty,
                };
                await _chroma.EmbedAndStoreAsync(
                    id:       created.Id.ToString(),
                    text:     $"{created.Title}\n{created.Content}",
                    metadata: metadata);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Chroma upsert background task faulted for entry {Id}", created.Id);
            }
        });

        return result;
    }

    public async Task<MemoryEntryDto?> UpdateAsync(int id, CreateMemoryEntryDto dto)
    {
        var entry = await _repository.GetByIdAsync(id);
        if (entry == null) return null;
        entry.Title   = dto.Title;
        entry.Content = dto.Content;
        entry.Type    = Enum.TryParse<MemoryType>(dto.Type, out var type) ? type : MemoryType.LongTerm;
        entry.Tags    = dto.Tags;
        entry.AgentId = dto.AgentId;
        await _repository.UpdateAsync(entry);

        // Re-upsert updated document into Chroma (fire-and-forget with exception guard).
        _ = Task.Run(async () =>
        {
            try
            {
                var metadata = new Dictionary<string, string>
                {
                    ["projectId"] = entry.ProjectId.ToString(),
                    ["type"]      = entry.Type.ToString(),
                    ["title"]     = entry.Title,
                    ["tags"]      = entry.Tags ?? string.Empty,
                };
                await _chroma.EmbedAndStoreAsync(
                    id:       entry.Id.ToString(),
                    text:     $"{entry.Title}\n{entry.Content}",
                    metadata: metadata);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Chroma upsert background task faulted for entry {Id}", entry.Id);
            }
        });

        return MapToDto(entry);
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var entry = await _repository.GetByIdAsync(id);
        if (entry == null) return false;
        await _repository.DeleteAsync(entry);

        // Remove from Chroma too (fire-and-forget with exception guard).
        _ = Task.Run(async () =>
        {
            try { await _chroma.DeleteAsync(id.ToString()); }
            catch (Exception ex) { _logger.LogWarning(ex, "Chroma delete background task faulted for entry {Id}", id); }
        });

        return true;
    }

    private static MemoryEntryDto MapToDto(MemoryEntry m) => new()
    {
        Id        = m.Id,
        ProjectId = m.ProjectId,
        Title     = m.Title,
        Content   = m.Content,
        Type      = m.Type.ToString(),
        Tags      = m.Tags,
        CreatedAt = m.CreatedAt,
        AgentId   = m.AgentId,
        Confidence  = m.Confidence,
        UsageCount  = m.UsageCount,
        SuccessRate = m.SuccessRate,
        LastUsed    = m.LastUsed,
        IsRule      = m.IsRule
    };
}
