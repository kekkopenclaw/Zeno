using Microsoft.EntityFrameworkCore;
using MissionControl.Domain.Entities;
using MissionControl.Domain.Interfaces;
using MissionControl.Infrastructure.Data;

namespace MissionControl.Infrastructure.Repositories;

public class TaskRepository : Repository<TaskItem>, ITaskRepository
{
    public TaskRepository(AppDbContext context) : base(context) { }

    public async Task<IReadOnlyList<TaskItem>> GetByProjectIdAsync(int projectId) =>
        await _context.Tasks
            .Include(t => t.AssignedAgent)
            .Where(t => t.ProjectId == projectId)
            .OrderByDescending(t => t.UpdatedAt ?? t.CreatedAt)
            .ToListAsync();

    // Override GetAllAsync to include agent navigation
    public new async Task<IReadOnlyList<TaskItem>> GetAllAsync() =>
        await _context.Tasks
            .Include(t => t.AssignedAgent)
            .OrderByDescending(t => t.UpdatedAt ?? t.CreatedAt)
            .ToListAsync();

    public new async Task<TaskItem?> GetByIdAsync(int id) =>
        await _context.Tasks
            .Include(t => t.AssignedAgent)
            .FirstOrDefaultAsync(t => t.Id == id);
}
