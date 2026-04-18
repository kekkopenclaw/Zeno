using MissionControl.Domain.Enums;

namespace MissionControl.Domain.Entities;

public class MemoryEntry
{
    public int Id { get; set; }
    public int ProjectId { get; set; }
    public Project Project { get; set; } = null!;
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public MemoryType Type { get; set; } = MemoryType.LongTerm;
    public string Tags { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public int? AgentId { get; set; }
    public Agent? Agent { get; set; }

    // ── Scoring & lifecycle fields (added for multi-factor retrieval) ──────
    /// <summary>Confidence level [0–1]. Higher = more trustworthy.</summary>
    public double Confidence { get; set; } = 0.5;

    /// <summary>How many times this entry was retrieved and injected into a prompt.</summary>
    public int UsageCount { get; set; } = 0;

    /// <summary>
    /// Ratio of successful outcomes after this memory was used [0–1].
    /// Updated via TrackUsageAsync after each task outcome.
    /// </summary>
    public double SuccessRate { get; set; } = 0.5;

    /// <summary>Timestamp of last retrieval (null = never used).</summary>
    public DateTime? LastUsed { get; set; }

    /// <summary>
    /// When true, this entry has been promoted to a Rule and is always
    /// prioritised in retrieval ahead of ordinary lessons.
    /// </summary>
    public bool IsRule { get; set; } = false;
}
