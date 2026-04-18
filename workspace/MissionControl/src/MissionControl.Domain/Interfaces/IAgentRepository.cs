using MissionControl.Domain.Entities;

namespace MissionControl.Domain.Interfaces;

public interface IAgentRepository : IRepository<Agent>
{
    Task<IReadOnlyList<Agent>> GetByProjectIdAsync(int projectId);
    Task<IReadOnlyList<Agent>> GetByTeamIdAsync(int teamId);
}
