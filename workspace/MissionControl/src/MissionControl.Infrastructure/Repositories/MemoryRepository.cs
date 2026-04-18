using Microsoft.EntityFrameworkCore;
using MissionControl.Domain.Entities;
using MissionControl.Domain.Interfaces;
using MissionControl.Infrastructure.Data;

namespace MissionControl.Infrastructure.Repositories;

public class MemoryRepository : Repository<MemoryEntry>, IMemoryRepository
{
    public MemoryRepository(AppDbContext context) : base(context) { }

    public async Task<IReadOnlyList<MemoryEntry>> GetByProjectIdAsync(int projectId) =>
        await _context.MemoryEntries.Where(m => m.ProjectId == projectId).ToListAsync();

    public async Task<IReadOnlyList<MemoryEntry>> SearchAsync(int projectId, string query) =>
        await _context.MemoryEntries
            .Where(m => m.ProjectId == projectId &&
                        (EF.Functions.Like(m.Title, $"%{query}%") ||
                         EF.Functions.Like(m.Content, $"%{query}%") ||
                         EF.Functions.Like(m.Tags, $"%{query}%")))
            .ToListAsync();

    /// <inheritdoc />
    public async Task<IReadOnlyList<MemoryEntry>> GetRulesAsync(int projectId) =>
        await _context.MemoryEntries
            .Where(m => m.ProjectId == projectId && m.IsRule)
            .OrderByDescending(m => m.UsageCount)
            .ToListAsync();

    /// <inheritdoc />
    public async Task TrackUsageAsync(int id, bool succeeded, CancellationToken ct = default)
    {
        var entry = await _context.MemoryEntries.FindAsync([id], ct);
        if (entry is null) return;

        entry.UsageCount++;
        entry.LastUsed = DateTime.UtcNow;

        // Exponential moving average (α = 0.2) so recent outcomes matter more
        const double alpha = 0.2;
        var outcome = succeeded ? 1.0 : 0.0;
        entry.SuccessRate = (alpha * outcome) + ((1 - alpha) * entry.SuccessRate);

        // Rule promotion: high-confidence lesson used ≥5 times with ≥80% success
        if (!entry.IsRule && entry.UsageCount >= 5 && entry.SuccessRate >= 0.8)
            entry.IsRule = true;

        await _context.SaveChangesAsync(ct);
    }
}
