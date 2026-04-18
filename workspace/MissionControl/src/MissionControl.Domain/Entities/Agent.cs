using MissionControl.Domain.Enums;

namespace MissionControl.Domain.Entities;

public class Agent
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public AgentStatus Status { get; set; } = AgentStatus.Idle;
    public AgentRole Role { get; set; } = AgentRole.Kakarot;
    public string Description { get; set; } = string.Empty;
    public string Skills { get; set; } = string.Empty;
    public string Emoji { get; set; } = "🤖";
    public string? Color { get; set; }
    public string? OpenClawAgentId { get; set; }
    public bool IsPaused { get; set; }
    public int ProjectId { get; set; }

    /// <summary>
    /// Which backend runs this agent's LLM inference.
    /// Ollama  — http://127.0.0.1:11434 — text in/out, no tool calling.
    /// OpenClaw — full agent runner with tools, skills, plugins, memory.
    /// </summary>
    public ExecutionBackend ExecutionBackend { get; set; } = ExecutionBackend.OpenClaw;

    /// <summary>Whether this agent has tool/plugin/function-call support (OpenClaw only).</summary>
    public bool ToolsEnabled { get; set; }

    /// <summary>Whether this agent is authorised to review, deploy, or publish (push role).</summary>
    public bool PushRole { get; set; }

    public Project Project { get; set; } = null!;
    public int? TeamId { get; set; }
    public Team? Team { get; set; }
    public ICollection<TaskItem> Tasks { get; set; } = new List<TaskItem>();
    public ICollection<ActivityLog> ActivityLogs { get; set; } = new List<ActivityLog>();
}
