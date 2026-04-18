using Microsoft.Extensions.Logging;
using MissionControl.Application.Interfaces;
using MissionControl.Domain.Entities;
using MissionControl.Domain.Interfaces;

namespace MissionControl.Application.Services;

/// <summary>
/// MemoryController — central memory orchestrator.
///
/// Multi-factor retrieval scoring formula:
///   score = (semantic_similarity * 0.5)
///         + (success_rate        * 0.2)
///         + (usage_count_norm    * 0.2)
///         + (recency             * 0.1)
///
/// Where:
///   semantic_similarity — cosine similarity from ChromaDB (0–1); falls back to
///                         keyword overlap normalised to [0–1] when Chroma is unavailable.
///   success_rate        — [0–1] EMA-weighted ratio of successful outcomes.
///   usage_count_norm    — log1p(usage_count) / log1p(max_usage) normalised to [0–1].
///   recency             — 1.0 at creation, decays toward 0 over 180 days.
///
/// Rules (IsRule = true) bypass scoring and are always prepended.
/// Hard cap: topK capped at 7.
/// </summary>
public sealed class MemoryController : IMemoryController
{
    private const int MaxTopK = 7;

    private readonly IMemoryRepository _memRepo;
    private readonly IMemorySummaryRepository _summaryRepo;
    private readonly IChromaVectorService _chroma;
    private readonly ILogger<MemoryController> _logger;

    public MemoryController(
        IMemoryRepository memRepo,
        IMemorySummaryRepository summaryRepo,
        IChromaVectorService chroma,
        ILogger<MemoryController> logger)
    {
        _memRepo = memRepo;
        _summaryRepo = summaryRepo;
        _chroma = chroma;
        _logger = logger;
    }

    // ── IMemoryController ─────────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<IReadOnlyList<string>> GetScoredMemoriesAsync(
        int projectId,
        string prompt,
        int topK = 6,
        CancellationToken ct = default)
    {
        topK = Math.Clamp(topK, 1, MaxTopK);

        // 1. Rules are always prepended (sorted by usage desc)
        var rules = await _memRepo.GetRulesAsync(projectId);
        var ruleTexts = rules
            .Take(topK)
            .Select(r => FormatEntry(r, prefix: "[RULE]"))
            .ToList();

        var remaining = topK - ruleTexts.Count;
        if (remaining <= 0)
            return ruleTexts;

        // 2. Candidate pool: all non-rule MemoryEntries + MemorySummaries
        var entries  = await _memRepo.GetByProjectIdAsync(projectId);
        var summaries = await _summaryRepo.GetByProjectIdAsync(projectId);

        var nonRuleEntries = entries.Where(e => !e.IsRule).ToList();

        // 3. Semantic similarity from Chroma (optional, degrades gracefully)
        var chromaScores = await GetChromaScoresAsync(prompt, topK * 2, ct);

        // 4. Normalise usage_count across the candidate set
        var maxUsage = nonRuleEntries.Count > 0
            ? nonRuleEntries.Max(e => e.UsageCount)
            : 1;
        if (maxUsage == 0) maxUsage = 1;

        // 5. Score each non-rule MemoryEntry
        var keywords = ExtractKeywords(prompt);
        var scored = new List<(string text, double score, int entryId)>();

        foreach (var e in nonRuleEntries)
        {
            var text = FormatEntry(e);
            double semantic = chromaScores.TryGetValue(e.Id.ToString(), out var cs) ? cs : KeywordSimilarity(text, keywords);
            double successR  = e.SuccessRate;
            double usageN    = Math.Log(1 + e.UsageCount) / Math.Log(1 + maxUsage);
            double recency   = RecencyScore(e.CreatedAt);

            double finalScore = (semantic * 0.5) + (successR * 0.2) + (usageN * 0.2) + (recency * 0.1);
            scored.Add((text, finalScore, e.Id));
        }

        // 6. Add MemorySummary entries (keyword scored only — no Id to track)
        foreach (var s in summaries)
        {
            var text  = $"[Summary] Problem: {s.Problem} | Fix: {s.Fix} | Lesson: {s.Lesson}";
            var score = KeywordSimilarity(text, keywords);
            scored.Add((text, score, -1));
        }

        // 7. Select top remaining — retrieval is a pure read.
        // Callers are expected to call TrackUsageAsync(id, succeeded) explicitly
        // after the downstream task outcome is known.
        var topNonRule = scored
            .Where(c => c.score > 0)
            .OrderByDescending(c => c.score)
            .Take(remaining)
            .ToList();

        var result = ruleTexts.Concat(topNonRule.Select(c => c.text)).ToList();
        _logger.LogDebug(
            "MemoryController: returning {Count} memories for project {ProjectId} (rules={Rules}, scored={Scored})",
            result.Count, projectId, ruleTexts.Count, topNonRule.Count);

        return result;
    }

    /// <inheritdoc />
    public Task TrackUsageAsync(int memoryEntryId, bool succeeded, CancellationToken ct = default) =>
        _memRepo.TrackUsageAsync(memoryEntryId, succeeded, ct);

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task<Dictionary<string, double>> GetChromaScoresAsync(
        string query, int topK, CancellationToken ct)
    {
        try
        {
            var results = await _chroma.SearchAsync(query, topK, ct);
            // Convert Chroma distances (lower = closer) to similarity scores [0,1]
            return results.ToDictionary(
                r => r.Id,
                r => Math.Max(0.0, 1.0 - (r.Distance / 2.0)));
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "MemoryController: Chroma unavailable, falling back to keyword scoring.");
            return [];
        }
    }

    private static string FormatEntry(MemoryEntry e, string prefix = "") =>
        string.IsNullOrEmpty(prefix)
            ? $"[{e.Type}] {e.Title}: {e.Content}"
            : $"{prefix}[{e.Type}] {e.Title}: {e.Content}";

    /// <summary>
    /// Normalised keyword overlap in [0,1].
    /// Returns the fraction of prompt keywords found in <paramref name="text"/>.
    /// </summary>
    private static double KeywordSimilarity(string text, HashSet<string> keywords)
    {
        if (keywords.Count == 0) return 0.0;
        var textLower = text.ToLowerInvariant();
        var hits = keywords.Count(k => textLower.Contains(k));
        return (double)hits / keywords.Count;
    }

    /// <summary>
    /// Recency score: 1.0 when brand new, decaying toward 0 over 180 days.
    /// Uses an exponential decay: e^(−age_days / 90).
    /// </summary>
    private static double RecencyScore(DateTime createdAt)
    {
        var ageDays = (DateTime.UtcNow - createdAt).TotalDays;
        return Math.Exp(-ageDays / 90.0);
    }

    private static HashSet<string> ExtractKeywords(string text)
    {
        var stopWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "the", "a", "an", "and", "or", "but", "in", "on", "at", "to", "for",
            "is", "are", "was", "were", "it", "its", "this", "that", "with", "of"
        };
        return text
            .Split([' ', '\n', '\r', '.', ',', ':', ';', '(', ')', '[', ']'],
                StringSplitOptions.RemoveEmptyEntries)
            .Select(w => w.ToLowerInvariant().Trim())
            .Where(w => w.Length > 3 && !stopWords.Contains(w))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }
}
