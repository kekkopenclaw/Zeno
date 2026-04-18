using MissionControl.Application.DTOs;
using MissionControl.Domain.Entities;
using MissionControl.Domain.Enums;
using MissionControl.Domain.Interfaces;

namespace MissionControl.Application.Services;

public class TaskService
{
    private readonly ITaskRepository _repository;

    public TaskService(ITaskRepository repository)
    {
        _repository = repository;
    }

    public async Task<IReadOnlyList<TaskItemDto>> GetAllAsync()
    {
        var tasks = await _repository.GetAllAsync();
        return tasks.Select(MapToDto).ToList();
    }

    public async Task<IReadOnlyList<TaskItemDto>> GetByProjectIdAsync(int projectId)
    {
        var tasks = await _repository.GetByProjectIdAsync(projectId);
        return tasks.Select(MapToDto).ToList();
    }

    public async Task<TaskItemDto?> GetByIdAsync(int id)
    {
        var task = await _repository.GetByIdAsync(id);
        return task == null ? null : MapToDto(task);
    }

    public async Task<TaskItemDto> CreateAsync(CreateTaskItemDto dto)
    {
        var status = TaskItemStatus.Todo;
        if (!string.IsNullOrWhiteSpace(dto.Status) && Enum.TryParse<TaskItemStatus>(dto.Status, out var parsedStatus))
            status = parsedStatus;
        var task = new TaskItem
        {
            Title = dto.Title,
            Description = dto.Description,
            Priority = Enum.TryParse<TaskPriority>(dto.Priority, out var priority) ? priority : TaskPriority.Medium,
            AssignedAgentId = dto.AssignedAgentId,
            ProjectId = dto.ProjectId,
            Status = status,
            ComplexityScore = dto.ComplexityScore,
            CreatedAt = DateTime.UtcNow
        };
        var created = await _repository.AddAsync(task);
        return MapToDto(created);
    }

    public async Task<TaskItemDto?> UpdateStatusAsync(int id, UpdateTaskStatusDto dto)
    {
        var task = await _repository.GetByIdAsync(id);
        if (task == null) return null;
        if (!string.IsNullOrWhiteSpace(dto.Status) && Enum.TryParse<TaskItemStatus>(dto.Status, out var status))
        {
            task.Status = status;
            task.StatusEnteredAt = DateTime.UtcNow;
        }
        task.ReviewNotes = dto.ReviewNotes;
        task.UpdatedAt = DateTime.UtcNow;
        await _repository.UpdateAsync(task);
        return MapToDto(task);
    }

    public async Task<TaskItemDto?> UpdateAsync(int id, CreateTaskItemDto dto)
    {
        var task = await _repository.GetByIdAsync(id);
        if (task == null) return null;
        task.Title = dto.Title;
        task.Description = dto.Description;
        task.Priority = Enum.TryParse<TaskPriority>(dto.Priority, out var priority) ? priority : TaskPriority.Medium;
        task.AssignedAgentId = dto.AssignedAgentId;
        task.ProjectId = dto.ProjectId;
        task.ComplexityScore = dto.ComplexityScore;
        task.UpdatedAt = DateTime.UtcNow;
        await _repository.UpdateAsync(task);
        return MapToDto(task);
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var task = await _repository.GetByIdAsync(id);
        if (task == null) return false;
        await _repository.DeleteAsync(task);
        return true;
    }

    internal static TaskItemDto MapToDto(TaskItem t) => new()
    {
        Id = t.Id,
        Title = t.Title,
        Description = t.Description,
        Status = t.Status.ToString(),
        Priority = t.Priority.ToString(),
        AssignedAgentId = t.AssignedAgentId,
        AssignedAgentName = t.AssignedAgent?.Name,
        AssignedAgentEmoji = t.AssignedAgent?.Emoji,
        ProjectId = t.ProjectId,
        CreatedAt = t.CreatedAt,
        UpdatedAt = t.UpdatedAt,
        StatusEnteredAt = t.StatusEnteredAt,
        RetryCount = t.RetryCount,
        ReviewFailCount = t.ReviewFailCount,
        ReviewNotes = t.ReviewNotes,
        ComplexityScore = t.ComplexityScore
    };
}
