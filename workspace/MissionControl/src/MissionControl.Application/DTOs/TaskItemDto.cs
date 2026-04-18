namespace MissionControl.Application.DTOs;

public class TaskItemDto
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public int? AssignedAgentId { get; set; }
    public string? AssignedAgentName { get; set; }
    public string? AssignedAgentEmoji { get; set; }
    public int ProjectId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public DateTime? StatusEnteredAt { get; set; }
    public int RetryCount { get; set; }
    public int ReviewFailCount { get; set; }
    public string? ReviewNotes { get; set; }
    public int ComplexityScore { get; set; }

    // --- Subtask/parent support ---
    public int? ParentTaskId { get; set; }
    public List<TaskItemDto>? Subtasks { get; set; }
}

public class CreateTaskItemDto
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Priority { get; set; } = "Medium";
    public int? AssignedAgentId { get; set; }
    public int ProjectId { get; set; }
    public int ComplexityScore { get; set; } = 0;
    public string Status { get; set; } = string.Empty; // New: allows specifying initial status
    public int? ParentTaskId { get; set; } // New: for subtask creation
}

public class UpdateTaskStatusDto
{
    public string Status { get; set; } = string.Empty;
    public string? ReviewNotes { get; set; }
}
