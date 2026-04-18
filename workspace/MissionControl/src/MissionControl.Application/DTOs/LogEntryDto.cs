namespace MissionControl.Application.DTOs;

public class LogEntryDto
{
    public int Id { get; set; }
    public DateTime Timestamp { get; set; }
    public string Level { get; set; } = "Info";
    public string? AgentName { get; set; }
    public string? TaskId { get; set; }
    public string? CorrelationId { get; set; }
    public string? Action { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? Exception { get; set; }
    public string Source { get; set; } = "Backend";
}

public class CreateLogEntryDto
{
    public string Level { get; set; } = "Info";
    public string? AgentName { get; set; }
    public string? TaskId { get; set; }
    public string? CorrelationId { get; set; }
    public string? Action { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? Exception { get; set; }
    public string Source { get; set; } = "Backend";
}

public class FrontendLogDto
{
    public string Level { get; set; } = "Error";
    public string Message { get; set; } = string.Empty;
    public string? CorrelationId { get; set; }
    public string? Url { get; set; }
    public string? StackTrace { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
