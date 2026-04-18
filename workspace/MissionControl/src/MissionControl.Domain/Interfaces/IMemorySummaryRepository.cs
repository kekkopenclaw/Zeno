using MissionControl.Domain.Entities;

namespace MissionControl.Domain.Interfaces;

public interface IMemorySummaryRepository
{
    Task<IReadOnlyList<MemorySummary>> GetAllAsync();
    Task<IReadOnlyList<MemorySummary>> GetByProjectIdAsync(int projectId);
    Task<MemorySummary?> GetByIdAsync(int id);
    Task<MemorySummary> AddAsync(MemorySummary summary);
    Task<IReadOnlyList<MemorySummary>> GetRecentAsync(int projectId, int count = 20);
}
