using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using MissionControl.Application.DTOs;
using MissionControl.Application.Interfaces;
using MissionControl.Domain.Entities;
using MissionControl.Domain.Enums;
using MissionControl.Domain.Interfaces;

namespace MissionControl.Application.Services;

// ── Session store singleton ───────────────────────────────────────────────────

/// <summary>
/// Singleton hot-layer for in-process session memory (Layer 1).
/// Lives for the app lifetime and is safe to inject into both singletons and scoped services.
/// </summary>
public sealed class KairosSessionStore
{
    private sealed record SessionEntry(string Content, DateTime LastAccess);

    private readonly ConcurrentDictionary<string, List<SessionEntry>> _session = new();
    private readonly TimeSpan _ttl = TimeSpan.FromMinutes(30);

    public void Add(string sessionId, string content)
    {
        var bucket = _session.GetOrAdd(sessionId, _ => []);
        lock (bucket)
        {
            bucket.Add(new SessionEntry(content, DateTime.UtcNow));
            Evict();
        }
    }

    public IReadOnlyList<string> Get(string sessionId)
    {
        if (!_session.TryGetValue(sessionId, out var bucket)) return [];
        lock (bucket)
        {
            return bucket
                .Where(e => DateTime.UtcNow - e.LastAccess < _ttl)
                .Select(e => e.Content)
                .ToList();
        }
    }

    private void Evict()
    {
        var cutoff = DateTime.UtcNow - _ttl;
        foreach (var key in _session.Keys.ToList())
        {
            if (!_session.TryGetValue(key, out var b)) continue;
            lock (b)
            {
                b.RemoveAll(e => e.LastAccess < cutoff);
                if (b.Count == 0) _session.TryRemove(key, out _);
            }
        }
    }
}

// ── KairosMemoryService (scoped) ──────────────────────────────────────────────

/// <summary>
/// KairosMemoryService — ported from Claw-code memory distillation layers.
///
/// Implements 3-layer memory architecture:
///
///   Layer 1 — Session (hot):
///     Backed by the singleton KairosSessionStore.
///     Raw working context for the current agent loop run.
///     Evicted after 30 minutes of inactivity.
///
///   Layer 2 — Persistent (warm):
///     MemorySummary rows in SQLite.
///     Structured problem/fix/lesson per completed task.
///     Survives restarts, searchable.
///
///   Layer 3 — Distilled (cold):
///     MemoryEntry rows with Type=LongTerm.
///     Written by the KAIROS daemon or manually.
///     High-signal, condensed wisdom extracted by the Trunks/KAIROS agents.
///
/// GetRelevantContextAsync(projectId, prompt):
///   Delegates to MemoryController for multi-factor scored retrieval.
///   Falls back to keyword-only ranking when MemoryController is unavailable.
/// </summary>
public sealed class KairosMemoryService
{
    private readonly KairosSessionStore _session;
    private readonly IMemoryRepository _memRepo;
    private readonly IMemorySummaryRepository _summaryRepo;
    private readonly IMemoryController _memController;
    private readonly ISignalRNotifier _notifier;
    private readonly ILogService _logSvc;
    private readonly ILogger<KairosMemoryService> _logger;

    public KairosMemoryService(
        KairosSessionStore session,
        IMemoryRepository memRepo,
        IMemorySummaryRepository summaryRepo,
        IMemoryController memController,
        ISignalRNotifier notifier,
        ILogService logSvc,
        ILogger<KairosMemoryService> logger)
    {
        _session = session;
        _memRepo = memRepo;
        _summaryRepo = summaryRepo;
        _memController = memController;
        _notifier = notifier;
        _logSvc = logSvc;
        _logger = logger;
    }

    // ── Layer 1: Session operations ───────────────────────────────────────

    /// <summary>Pushes a raw text chunk into the hot session cache.</summary>
    public void AddToSession(string sessionId, string content) =>
        _session.Add(sessionId, content);

    /// <summary>Retrieves all live session chunks for a given session ID.</summary>
    public IReadOnlyList<string> GetSessionChunks(string sessionId) =>
        _session.Get(sessionId);

    // ── Layer 2 + 3: Persistent context retrieval ─────────────────────────

    /// <summary>
    /// Returns up to <paramref name="topK"/> memory chunks most relevant to <paramref name="prompt"/>.
    /// Delegates to <see cref="IMemoryController"/> for multi-factor scored retrieval.
    /// </summary>
    public Task<IReadOnlyList<string>> GetRelevantContextAsync(
        int projectId,
        string prompt,
        int topK = 6,
        CancellationToken ct = default) =>
        _memController.GetScoredMemoriesAsync(projectId, prompt, topK, ct);

    // ── Layer 2: Distil a task outcome to a MemorySummary ─────────────────

    /// <summary>
    /// Distils the outcome of a completed task into a structured MemorySummary (layer 2).
    /// Called by the KAIROS daemon and the pipeline test.
    /// </summary>
    public async Task<MemorySummaryDto> DistilSummaryAsync(
        int projectId,
        int? taskItemId,
        string problem,
        string fix,
        string lesson,
        string agentRole,
        int retries,
        int complexity,
        CancellationToken ct = default)
    {
        var summary = new MemorySummary
        {
            ProjectId = projectId,
            TaskItemId = taskItemId,
            Problem = problem,
            Fix = fix,
            Lesson = lesson,
            AgentRole = agentRole,
            RetriesRequired = retries,
            ComplexityScore = complexity,
            CreatedAt = DateTime.UtcNow
        };

        var saved = await _summaryRepo.AddAsync(summary);
        var dto = MapSummaryToDto(saved);

        await _notifier.NotifyMemorySummaryAddedAsync(dto);
        _logger.LogInformation("KAIROS: distilled memory summary for task {TaskId}", taskItemId);

        return dto;
    }

    // ── Layer 3: Write a long-term memory entry ───────────────────────────

    /// <summary>
    /// Writes a high-signal insight to MemoryEntry (layer 3 — distilled/cold store).
    /// Called by the KAIROS 2 AM daemon after processing memory files.
    /// </summary>
    public async Task<MemoryEntryDto> WriteDistilledMemoryAsync(
        int projectId,
        string title,
        string content,
        MemoryType type,
        string tags,
        int? agentId = null,
        CancellationToken ct = default)
    {
        // Chunk content to ≤5 sentences for retrieval efficiency
        var chunked = ChunkToSentences(content, maxSentences: 5);

        var entry = new MemoryEntry
        {
            ProjectId = projectId,
            Title = title,
            Content = chunked,
            Type = type,
            Tags = tags,
            AgentId = agentId,
            CreatedAt = DateTime.UtcNow
        };

        var saved = await _memRepo.AddAsync(entry);
        var dto = MapEntryToDto(saved);

        await _notifier.NotifyMemoryAdded(dto);
        _logger.LogInformation("KAIROS: wrote distilled memory entry '{Title}' ({Type})", title, type);

        return dto;
    }

    // ── Text helpers ──────────────────────────────────────────────────────

    /// <summary>
    /// Chunks text into at most <paramref name="maxSentences"/> sentences.
    /// Implements the Claw-code "5-sentence max, entropy" chunking rule.
    /// Prefers higher-entropy (longer, more unique) sentences.
    /// </summary>
    public static string ChunkToSentences(string text, int maxSentences = 5)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;

        var sentences = text
            .Split(['.', '!', '?'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(s => s.Length > 10)
            .ToList();

        if (sentences.Count <= maxSentences)
            return string.Join(". ", sentences) + ".";

        // Entropy-based selection: pick the sentences with the most unique words
        var selected = sentences
            .Select(s => (sentence: s, entropy: UniqueWordCount(s)))
            .OrderByDescending(x => x.entropy)
            .Take(maxSentences)
            .Select(x => x.sentence)
            .ToList();

        return string.Join(". ", selected) + ".";
    }

    private static int UniqueWordCount(string text) =>
        text.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(w => w.ToLowerInvariant().Trim('.', ',', ';', ':'))
            .Distinct()
            .Count();

    // ── DTO mappers ───────────────────────────────────────────────────────

    private static MemorySummaryDto MapSummaryToDto(MemorySummary s) => new()
    {
        Id = s.Id,
        ProjectId = s.ProjectId,
        TaskItemId = s.TaskItemId,
        Problem = s.Problem,
        Fix = s.Fix,
        Lesson = s.Lesson,
        AgentRole = s.AgentRole,
        RetriesRequired = s.RetriesRequired,
        ComplexityScore = s.ComplexityScore,
        CreatedAt = s.CreatedAt
    };

    private static MemoryEntryDto MapEntryToDto(MemoryEntry e) => new()
    {
        Id = e.Id,
        ProjectId = e.ProjectId,
        Title = e.Title,
        Content = e.Content,
        Type = e.Type.ToString(),
        Tags = e.Tags,
        CreatedAt = e.CreatedAt,
        AgentId = e.AgentId,
        Confidence = e.Confidence,
        UsageCount = e.UsageCount,
        SuccessRate = e.SuccessRate,
        LastUsed = e.LastUsed,
        IsRule = e.IsRule
    };
}
