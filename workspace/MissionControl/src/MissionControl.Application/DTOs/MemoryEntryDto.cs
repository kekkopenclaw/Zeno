namespace MissionControl.Application.DTOs;

public class MemoryEntryDto
{
    public int Id { get; set; }
    public int ProjectId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Tags { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public int? AgentId { get; set; }
    public double Confidence { get; set; }
    public int UsageCount { get; set; }
    public double SuccessRate { get; set; }
    public DateTime? LastUsed { get; set; }
    public bool IsRule { get; set; }
}

public class CreateMemoryEntryDto
{
    public int ProjectId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string Type { get; set; } = "LongTerm";
    public string Tags { get; set; } = string.Empty;
    public int? AgentId { get; set; }
}
