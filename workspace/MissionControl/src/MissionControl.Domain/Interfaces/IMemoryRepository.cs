using MissionControl.Domain.Entities;

namespace MissionControl.Domain.Interfaces;

public interface IMemoryRepository : IRepository<MemoryEntry>
{
    Task<IReadOnlyList<MemoryEntry>> GetByProjectIdAsync(int projectId);
    Task<IReadOnlyList<MemoryEntry>> SearchAsync(int projectId, string query);

    /// <summary>Returns all entries flagged as rules for a project, ordered by usage count desc.</summary>
    Task<IReadOnlyList<MemoryEntry>> GetRulesAsync(int projectId);

    /// <summary>
    /// Increments UsageCount and updates LastUsed. If <paramref name="succeeded"/> is true,
    /// re-weights SuccessRate with an exponential moving average (α=0.2).
    /// Also evaluates promotion: if UsageCount ≥ 5 and SuccessRate ≥ 0.8, sets IsRule = true.
    /// </summary>
    Task TrackUsageAsync(int id, bool succeeded, CancellationToken ct = default);
}
