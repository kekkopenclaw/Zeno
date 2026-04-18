namespace MissionControl.Domain.Entities;

/// <summary>
/// Structured memory summary — never stores full conversation,
/// only distilled learnings from completed task cycles.
/// </summary>
public class MemorySummary
{
    public int Id { get; set; }
    public int ProjectId { get; set; }
    public Project Project { get; set; } = null!;
    /// <summary>The task this summary relates to</summary>
    public int? TaskItemId { get; set; }
    /// <summary>What was the original problem</summary>
    public string Problem { get; set; } = string.Empty;
    /// <summary>How was it solved</summary>
    public string Fix { get; set; } = string.Empty;
    /// <summary>Key lesson for future routing/execution</summary>
    public string Lesson { get; set; } = string.Empty;
    /// <summary>Agent role that performed the fix</summary>
    public string AgentRole { get; set; } = string.Empty;
    /// <summary>How many retries were needed (used by learning service)</summary>
    public int RetriesRequired { get; set; } = 0;
    /// <summary>Complexity score of the task (used to refine routing)</summary>
    public int ComplexityScore { get; set; } = 0;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
