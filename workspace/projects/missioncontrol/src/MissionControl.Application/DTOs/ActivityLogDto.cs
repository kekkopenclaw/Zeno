namespace MissionControl.Application.DTOs;

public class ActivityLogDto
{
    public int Id { get; set; }
    public int? AgentId { get; set; }
    public string? AgentName { get; set; }
    public string? AgentEmoji { get; set; }
    public int ProjectId { get; set; }
    public string Message { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
}

public class CreateActivityLogDto
{
    public int ProjectId { get; set; }
    public int? AgentId { get; set; }
    public string Message { get; set; } = string.Empty;
}
