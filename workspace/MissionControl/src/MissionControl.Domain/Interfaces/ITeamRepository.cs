using MissionControl.Domain.Entities;

namespace MissionControl.Domain.Interfaces;

public interface ITeamRepository
{
    Task<IEnumerable<Team>> GetByProjectIdAsync(int projectId);
    Task AddAsync(Team team);
    Task<Team?> GetByIdAsync(int teamId);
    Task<IEnumerable<Agent>> GetAgentsAsync(int teamId);
}
