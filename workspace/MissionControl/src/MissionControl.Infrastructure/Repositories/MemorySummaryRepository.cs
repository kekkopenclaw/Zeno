using Microsoft.EntityFrameworkCore;
using MissionControl.Domain.Entities;
using MissionControl.Domain.Interfaces;
using MissionControl.Infrastructure.Data;

namespace MissionControl.Infrastructure.Repositories;

public class MemorySummaryRepository : IMemorySummaryRepository
{
    private readonly AppDbContext _context;

    public MemorySummaryRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<MemorySummary>> GetAllAsync() =>
        await _context.MemorySummaries.ToListAsync();

    public async Task<IReadOnlyList<MemorySummary>> GetByProjectIdAsync(int projectId) =>
        await _context.MemorySummaries
            .Where(s => s.ProjectId == projectId)
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync();

    public async Task<MemorySummary?> GetByIdAsync(int id) =>
        await _context.MemorySummaries.FindAsync(id);

    public async Task<MemorySummary> AddAsync(MemorySummary summary)
    {
        _context.MemorySummaries.Add(summary);
        await _context.SaveChangesAsync();
        return summary;
    }

    public async Task<IReadOnlyList<MemorySummary>> GetRecentAsync(int projectId, int count = 20) =>
        await _context.MemorySummaries
            .Where(s => s.ProjectId == projectId)
            .OrderByDescending(s => s.CreatedAt)
            .Take(count)
            .ToListAsync();
}
