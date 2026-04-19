using MissionControl.Application.DTOs;
using MissionControl.Application.Interfaces;
using MissionControl.Domain.Entities;
using MissionControl.Domain.Enums;
using MissionControl.Domain.Interfaces;

namespace MissionControl.Application.Services;

/// <summary>
/// ReviewService — enforces the review loop.
/// If review fails once → Fix (same agent, incremented RetryCount).
/// If review fails twice → escalate to stronger agent (Vegeta).
/// </summary>
public class ReviewService
{
    private readonly ITaskRepository _taskRepository;
    private readonly IAgentRepository _agentRepository;
    private readonly IActivityLogRepository _logRepository;
    private readonly ISignalRNotifier _notifier;

    public ReviewService(
        ITaskRepository taskRepository,
        IAgentRepository agentRepository,
        IActivityLogRepository logRepository,
        ISignalRNotifier notifier)
    {
        _taskRepository = taskRepository;
        _agentRepository = agentRepository;
        _logRepository = logRepository;
        _notifier = notifier;
    }

    /// <summary>
    /// Passes a task — moves to Done.
    /// </summary>
    public async Task<TaskItemDto> PassAsync(int taskId, int projectId)
    {
        var task = await _taskRepository.GetByIdAsync(taskId)
            ?? throw new InvalidOperationException($"Task {taskId} not found");

        task.Status = TaskItemStatus.Done;
        task.UpdatedAt = DateTime.UtcNow;
        await _taskRepository.UpdateAsync(task);

        await LogAsync(projectId, null, $"✅ Task '{task.Title}' passed review and moved to Done.");
        await _notifier.NotifyTaskUpdatedAsync(TaskService.MapToDto(task));
        return TaskService.MapToDto(task);
    }

    /// <summary>
    /// Fails a task:
    /// - First fail → Enforcement (Jiren)
    /// - Second fail → escalate to Coding (Vegeta)
    /// </summary>
    public async Task<TaskItemDto> FailAsync(int taskId, int projectId, string reviewNotes)
    {
        var task = await _taskRepository.GetByIdAsync(taskId)
            ?? throw new InvalidOperationException($"Task {taskId} not found");

        task.ReviewFailCount++;
        task.RetryCount++;
        task.ReviewNotes = reviewNotes;
        task.UpdatedAt = DateTime.UtcNow;

        if (task.ReviewFailCount >= 2)
        {
            // Escalate to Vegeta (advanced coder)
            var vegeta = (await _agentRepository.GetByProjectIdAsync(projectId))
                .FirstOrDefault(a => a.Role != null && a.Role.Equals("Coder", StringComparison.OrdinalIgnoreCase));

            if (vegeta != null)
                task.AssignedAgentId = vegeta.Id;

            task.ReviewFailCount = 0; // Reset after escalation
            task.Status = TaskItemStatus.Coding;

            await LogAsync(projectId, vegeta?.Id,
                $"⬆️ Task '{task.Title}' escalated to Vegeta after 2 review failures.");
        }
        else
        {
            // First fail → Enforcement (Jiren)
            var jiren = (await _agentRepository.GetByProjectIdAsync(projectId))
                .FirstOrDefault(a => a.Role != null && a.Role.Equals("Enforcement", StringComparison.OrdinalIgnoreCase));
            if (jiren != null)
                task.AssignedAgentId = jiren.Id;
            task.Status = TaskItemStatus.Enforcement;
            await LogAsync(projectId, jiren?.Id,
                $"💪 Task '{task.Title}' sent to Enforcement (Jiren) (retry #{task.RetryCount}). Notes: {reviewNotes}");
        }

        await _taskRepository.UpdateAsync(task);
        await _notifier.NotifyTaskUpdatedAsync(TaskService.MapToDto(task));
        return TaskService.MapToDto(task);
    }

    private async Task LogAsync(int projectId, int? agentId, string message)
    {
        var log = new ActivityLog
        {
            ProjectId = projectId,
            AgentId = agentId,
            Message = message,
            Timestamp = DateTime.UtcNow
        };
        var saved = await _logRepository.AddAsync(log);
        await _notifier.NotifyLogCreatedAsync(new ActivityLogDto
        {
            Id = saved.Id,
            ProjectId = saved.ProjectId,
            AgentId = saved.AgentId,
            Message = saved.Message,
            Timestamp = saved.Timestamp
        });
    }
}
