using Microsoft.Extensions.Logging;
using MissionControl.Application.DTOs;
using MissionControl.Application.Interfaces;
using MissionControl.Domain.Entities;
using MissionControl.Domain.Interfaces;

namespace MissionControl.Application.Services;

/// <summary>
/// Central log service — writes structured logs to the database and through the Serilog pipeline.
/// Supports querying logs by task, agent, and level for the observability dashboard.
/// </summary>
public class LogService : ILogService
{
    private readonly ILogRepository _repository;
    private readonly ILogger<LogService> _logger;

    public LogService(ILogRepository repository, ILogger<LogService> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task WriteAsync(
        string level,
        string message,
        string? agentName = null,
        string? taskId = null,
        string? correlationId = null,
        string? action = null,
        string? exception = null,
        string source = "Backend")
    {
        // Emit via Serilog pipeline (console + file sinks)
        using var scope = _logger.BeginScope(new Dictionary<string, object?>
        {
            ["AgentName"]     = agentName,
            ["TaskId"]        = taskId,
            ["CorrelationId"] = correlationId,
            ["Action"]        = action,
            ["Source"]        = source,
        });

        switch (level.ToUpperInvariant())
        {
            case "WARNING":
            case "WARN":
                _logger.LogWarning("[{Agent}] [{Action}] {Message}", agentName ?? "-", action ?? "-", message);
                break;
            case "ERROR":
                _logger.LogError("[{Agent}] [{Action}] {Message} | Exception: {Exception}", agentName ?? "-", action ?? "-", message, exception);
                break;
            default:
                _logger.LogInformation("[{Agent}] [{Action}] {Message}", agentName ?? "-", action ?? "-", message);
                break;
        }

        // Persist to database for API queries
        var entry = new LogEntry
        {
            Timestamp     = DateTime.UtcNow,
            Level         = NormalizeLevel(level),
            AgentName     = agentName,
            TaskId        = taskId,
            CorrelationId = correlationId,
            Action        = action,
            Message       = message,
            Exception     = exception,
            Source        = source,
        };

        await _repository.AddAsync(entry);
    }

    public async Task<IReadOnlyList<LogEntryDto>> GetAllAsync(int limit = 200) =>
        (await _repository.GetAllAsync(limit)).Select(MapToDto).ToList();

    public async Task<IReadOnlyList<LogEntryDto>> GetByTaskIdAsync(string taskId, int limit = 100) =>
        (await _repository.GetByTaskIdAsync(taskId, limit)).Select(MapToDto).ToList();

    public async Task<IReadOnlyList<LogEntryDto>> GetByAgentNameAsync(string agentName, int limit = 100) =>
        (await _repository.GetByAgentNameAsync(agentName, limit)).Select(MapToDto).ToList();

    public async Task<IReadOnlyList<LogEntryDto>> GetByLevelAsync(string level, int limit = 100) =>
        (await _repository.GetByLevelAsync(NormalizeLevel(level), limit)).Select(MapToDto).ToList();

    private static string NormalizeLevel(string level) => level.ToUpperInvariant() switch
    {
        "WARN" or "WARNING" => "Warning",
        "ERR"  or "ERROR"   => "Error",
        _                   => "Info",
    };

    private static LogEntryDto MapToDto(LogEntry e) => new()
    {
        Id            = e.Id,
        Timestamp     = e.Timestamp,
        Level         = e.Level,
        AgentName     = e.AgentName,
        TaskId        = e.TaskId,
        CorrelationId = e.CorrelationId,
        Action        = e.Action,
        Message       = e.Message,
        Exception     = e.Exception,
        Source        = e.Source,
    };
}
