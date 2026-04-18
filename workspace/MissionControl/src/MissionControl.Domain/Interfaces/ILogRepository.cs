using MissionControl.Domain.Entities;

namespace MissionControl.Domain.Interfaces;

public interface ILogRepository
{
    Task<LogEntry> AddAsync(LogEntry entry);
    Task<IReadOnlyList<LogEntry>> GetAllAsync(int limit = 200);
    Task<IReadOnlyList<LogEntry>> GetByTaskIdAsync(string taskId, int limit = 100);
    Task<IReadOnlyList<LogEntry>> GetByAgentNameAsync(string agentName, int limit = 100);
    Task<IReadOnlyList<LogEntry>> GetByLevelAsync(string level, int limit = 100);
}
