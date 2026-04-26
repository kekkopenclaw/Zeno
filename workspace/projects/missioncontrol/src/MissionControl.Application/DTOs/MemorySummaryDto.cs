namespace MissionControl.Application.DTOs;

public class MemorySummaryDto
{
    public int Id { get; set; }
    public int ProjectId { get; set; }
    public int? TaskItemId { get; set; }
    public string Problem { get; set; } = string.Empty;
    public string Fix { get; set; } = string.Empty;
    public string Lesson { get; set; } = string.Empty;
    public string AgentRole { get; set; } = string.Empty;
    public int RetriesRequired { get; set; }
    public int ComplexityScore { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CreateMemorySummaryDto
{
    public int ProjectId { get; set; }
    public int? TaskItemId { get; set; }
    public string Problem { get; set; } = string.Empty;
    public string Fix { get; set; } = string.Empty;
    public string Lesson { get; set; } = string.Empty;
    public string AgentRole { get; set; } = string.Empty;
    public int RetriesRequired { get; set; }
    public int ComplexityScore { get; set; }
}
