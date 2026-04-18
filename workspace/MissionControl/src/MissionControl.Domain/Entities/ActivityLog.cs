namespace MissionControl.Domain.Entities;

public class ActivityLog
{
    public int Id { get; set; }
    public int? AgentId { get; set; }
    public Agent? Agent { get; set; }
    public int ProjectId { get; set; }
    public Project Project { get; set; } = null!;
    public string Message { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
