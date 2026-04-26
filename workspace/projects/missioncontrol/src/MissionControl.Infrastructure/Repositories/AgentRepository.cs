using Microsoft.EntityFrameworkCore;
using MissionControl.Domain.Entities;
using MissionControl.Domain.Interfaces;
using MissionControl.Infrastructure.Data;

namespace MissionControl.Infrastructure.Repositories;

public class AgentRepository : Repository<Agent>, IAgentRepository
{
    public AgentRepository(AppDbContext context) : base(context) { }

    public async Task<IReadOnlyList<Agent>> GetByProjectIdAsync(int projectId) =>
        await _context.Agents.Where(a => a.ProjectId == projectId).ToListAsync();

    public async Task<IReadOnlyList<Agent>> GetByTeamIdAsync(int teamId) =>
        await _context.Agents.Where(a => a.TeamId == teamId).ToListAsync();
}
