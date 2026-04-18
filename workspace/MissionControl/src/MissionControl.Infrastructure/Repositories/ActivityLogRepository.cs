using Microsoft.EntityFrameworkCore;
using MissionControl.Domain.Entities;
using MissionControl.Domain.Interfaces;
using MissionControl.Infrastructure.Data;

namespace MissionControl.Infrastructure.Repositories;

public class ActivityLogRepository : Repository<ActivityLog>, IActivityLogRepository
{
    public ActivityLogRepository(AppDbContext context) : base(context) { }

    public async Task<IReadOnlyList<ActivityLog>> GetByProjectIdAsync(int projectId, int limit = 50) =>
        await _context.ActivityLogs
            .Include(l => l.Agent)
            .Where(l => l.ProjectId == projectId)
            .OrderByDescending(l => l.Timestamp)
            .Take(limit)
            .ToListAsync();

    public async Task<IReadOnlyList<ActivityLog>> GetByAgentIdAsync(int agentId, int limit = 100) =>
        await _context.ActivityLogs
            .Include(l => l.Agent)
            .Where(l => l.AgentId == agentId)
            .OrderByDescending(l => l.Timestamp)
            .Take(limit)
            .ToListAsync();
}
