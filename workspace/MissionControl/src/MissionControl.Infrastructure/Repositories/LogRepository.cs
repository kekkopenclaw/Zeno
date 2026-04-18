using Microsoft.EntityFrameworkCore;
using MissionControl.Domain.Entities;
using MissionControl.Domain.Interfaces;
using MissionControl.Infrastructure.Data;

namespace MissionControl.Infrastructure.Repositories;

public class LogRepository : ILogRepository
{
    private readonly AppDbContext _context;

    public LogRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<LogEntry> AddAsync(LogEntry entry)
    {
        _context.Logs.Add(entry);
        await _context.SaveChangesAsync();
        return entry;
    }

    public async Task<IReadOnlyList<LogEntry>> GetAllAsync(int limit = 200) =>
        await _context.Logs
            .OrderByDescending(l => l.Timestamp)
            .Take(limit)
            .ToListAsync();

    public async Task<IReadOnlyList<LogEntry>> GetByTaskIdAsync(string taskId, int limit = 100) =>
        await _context.Logs
            .Where(l => l.TaskId == taskId)
            .OrderByDescending(l => l.Timestamp)
            .Take(limit)
            .ToListAsync();

    public async Task<IReadOnlyList<LogEntry>> GetByAgentNameAsync(string agentName, int limit = 100) =>
        await _context.Logs
            .Where(l => l.AgentName != null && l.AgentName.ToLower() == agentName.ToLower())
            .OrderByDescending(l => l.Timestamp)
            .Take(limit)
            .ToListAsync();

    public async Task<IReadOnlyList<LogEntry>> GetByLevelAsync(string level, int limit = 100) =>
        await _context.Logs
            .Where(l => l.Level == level)
            .OrderByDescending(l => l.Timestamp)
            .Take(limit)
            .ToListAsync();
}
