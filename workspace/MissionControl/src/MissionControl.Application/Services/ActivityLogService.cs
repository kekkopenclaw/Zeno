using MissionControl.Application.DTOs;
using MissionControl.Application.Interfaces;
using MissionControl.Domain.Entities;
using MissionControl.Domain.Interfaces;

namespace MissionControl.Application.Services;

public class ActivityLogService
{
    private readonly IActivityLogRepository _repository;
    private readonly ISignalRNotifier? _notifier;

    public ActivityLogService(IActivityLogRepository repository, ISignalRNotifier? notifier = null)
    {
        _repository = repository;
        _notifier = notifier;
    }

    public async Task<IReadOnlyList<ActivityLogDto>> GetByProjectIdAsync(int projectId, int limit = 50)
    {
        var logs = await _repository.GetByProjectIdAsync(projectId, limit);
        return logs.Select(MapToDto).ToList();
    }

    public async Task<IReadOnlyList<ActivityLogDto>> GetByAgentIdAsync(int agentId, int limit = 100)
    {
        var logs = await _repository.GetByAgentIdAsync(agentId, limit);
        return logs.Select(MapToDto).ToList();
    }

    public async Task<ActivityLogDto> CreateAsync(CreateActivityLogDto dto)
    {
        var log = new ActivityLog
        {
            ProjectId = dto.ProjectId,
            AgentId = dto.AgentId,
            Message = dto.Message,
            Timestamp = DateTime.UtcNow
        };
        var saved = await _repository.AddAsync(log);
        var result = MapToDto(saved);
        if (_notifier != null) await _notifier.NotifyLogCreatedAsync(result);
        return result;
    }

    private static ActivityLogDto MapToDto(ActivityLog l) => new()
    {
        Id = l.Id,
        AgentId = l.AgentId,
        AgentName = l.Agent?.Name,
        AgentEmoji = l.Agent?.Emoji,
        ProjectId = l.ProjectId,
        Message = l.Message,
        Timestamp = l.Timestamp
    };
}
