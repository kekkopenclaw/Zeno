using MissionControl.Application.DTOs;

namespace MissionControl.Application.Interfaces;

public interface ILogService
{
    Task WriteAsync(string level, string message, string? agentName = null, string? taskId = null,
        string? correlationId = null, string? action = null, string? exception = null, string source = "Backend");

    Task<IReadOnlyList<LogEntryDto>> GetAllAsync(int limit = 200);
    Task<IReadOnlyList<LogEntryDto>> GetByTaskIdAsync(string taskId, int limit = 100);
    Task<IReadOnlyList<LogEntryDto>> GetByAgentNameAsync(string agentName, int limit = 100);
    Task<IReadOnlyList<LogEntryDto>> GetByLevelAsync(string level, int limit = 100);
}
