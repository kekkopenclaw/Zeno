namespace MissionControl.Application.Interfaces;

/// <summary>
/// Central memory orchestrator — handles scored retrieval, usage tracking,
/// and rule promotion across all memory layers.
/// </summary>
public interface IMemoryController
{
    /// <summary>
    /// Returns at most <paramref name="topK"/> memory chunks most relevant to
    /// <paramref name="prompt"/>, applying multi-factor scoring:
    ///   score = (semantic_similarity * 0.5)
    ///         + (success_rate        * 0.2)
    ///         + (usage_count_norm    * 0.2)
    ///         + (recency             * 0.1)
    ///
    /// Rules (IsRule=true) are always prepended ahead of the ranked list.
    /// Tracks usage for every returned entry.
    /// </summary>
    Task<IReadOnlyList<string>> GetScoredMemoriesAsync(
        int projectId,
        string prompt,
        int topK = 6,
        CancellationToken ct = default);

    /// <summary>
    /// Records that a memory entry was used and whether the downstream task succeeded.
    /// Updates UsageCount, LastUsed, SuccessRate, and promotes to rule if eligible.
    /// </summary>
    Task TrackUsageAsync(int memoryEntryId, bool succeeded, CancellationToken ct = default);
}
