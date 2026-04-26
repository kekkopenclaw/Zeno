using MissionControl.Domain.Entities;
using MissionControl.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;
using MissionControl.Infrastructure.Data;

namespace MissionControl.Infrastructure.Repositories;

public class TeamRepository : ITeamRepository
{
    private readonly AppDbContext _db;
    public TeamRepository(AppDbContext db)
    {
        _db = db;
    }

    public async Task<IEnumerable<Team>> GetByProjectIdAsync(int projectId)
    {
        return await _db.Teams.Where(t => t.ProjectId == projectId).ToListAsync();
    }

    public async Task AddAsync(Team team)
    {
        _db.Teams.Add(team);
        await _db.SaveChangesAsync();
    }

    public async Task<Team?> GetByIdAsync(int teamId)
    {
        return await _db.Teams.FindAsync(teamId);
    }

    public async Task<IEnumerable<Agent>> GetAgentsAsync(int teamId)
    {
        return await _db.Agents.Where(a => a.TeamId == teamId).ToListAsync();
    }

    public async Task UpdateAsync(Team team)
    {
        _db.Teams.Update(team);
        await _db.SaveChangesAsync();
    }

    public async Task DeleteAsync(Team team)
    {
        _db.Teams.Remove(team);
        await _db.SaveChangesAsync();
    }
}
