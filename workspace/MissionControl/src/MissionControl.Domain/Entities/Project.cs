namespace MissionControl.Domain.Entities;

public class Project
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public ICollection<Agent> Agents { get; set; } = new List<Agent>();
    public ICollection<Team> Teams { get; set; } = new List<Team>();
    public ICollection<TaskItem> Tasks { get; set; } = new List<TaskItem>();
    public ICollection<MemoryEntry> MemoryEntries { get; set; } = new List<MemoryEntry>();
    public ICollection<MemorySummary> MemorySummaries { get; set; } = new List<MemorySummary>();
    public ICollection<ActivityLog> ActivityLogs { get; set; } = new List<ActivityLog>();
}
