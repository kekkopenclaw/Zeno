using MissionControl.Domain.Entities;

namespace MissionControl.Domain.Interfaces;

public interface IActivityLogRepository : IRepository<ActivityLog>
{
    Task<IReadOnlyList<ActivityLog>> GetByProjectIdAsync(int projectId, int limit = 50);
    Task<IReadOnlyList<ActivityLog>> GetByAgentIdAsync(int agentId, int limit = 100);
}
