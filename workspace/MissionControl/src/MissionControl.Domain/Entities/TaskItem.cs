using MissionControl.Domain.Enums;

namespace MissionControl.Domain.Entities;

public class TaskItem
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public TaskItemStatus Status { get; set; } = TaskItemStatus.Todo;
    public TaskPriority Priority { get; set; } = TaskPriority.Medium;
    public int? AssignedAgentId { get; set; }
    public Agent? AssignedAgent { get; set; }
    public int ProjectId { get; set; }
    public Project Project { get; set; } = null!;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    /// <summary>When this task entered its current status — used by the pipeline simulator</summary>
    public DateTime? StatusEnteredAt { get; set; }
    /// <summary>Number of times this task has been sent back for fixing</summary>
    public int RetryCount { get; set; } = 0;
    /// <summary>Number of consecutive review failures — triggers escalation at 2</summary>
    public int ReviewFailCount { get; set; } = 0;
    /// <summary>Notes from the last review cycle</summary>
    public string? ReviewNotes { get; set; }
    /// <summary>Estimated complexity for routing decisions (0–10)</summary>
    public int ComplexityScore { get; set; } = 0;

    // --- Subtask/parent support ---
    public int? ParentTaskId { get; set; }
    public TaskItem? ParentTask { get; set; }
    public ICollection<TaskItem> Subtasks { get; set; } = new List<TaskItem>();
}
