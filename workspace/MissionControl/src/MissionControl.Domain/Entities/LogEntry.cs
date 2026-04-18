namespace MissionControl.Domain.Entities;

public class LogEntry
{
    public int Id { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string Level { get; set; } = "Info";
    public string? AgentName { get; set; }
    public string? TaskId { get; set; }
    public string? CorrelationId { get; set; }
    public string? Action { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? Exception { get; set; }
    public string Source { get; set; } = "Backend";
}
