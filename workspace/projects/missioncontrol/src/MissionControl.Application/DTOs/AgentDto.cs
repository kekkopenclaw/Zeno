namespace MissionControl.Application.DTOs;

public class AgentDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Skills { get; set; } = string.Empty;
    public string Emoji { get; set; } = "🤖";
    public string? Color { get; set; }
    public string? OpenClawAgentId { get; set; }
    public bool IsPaused { get; set; }
    public int ProjectId { get; set; }

    /// <summary>"Ollama" or "OpenClaw"</summary>
    public string ExecutionBackend { get; set; } = "OpenClaw";

    /// <summary>Whether this agent has tool/plugin support (OpenClaw only).</summary>
    public bool ToolsEnabled { get; set; }

    /// <summary>Whether this agent can review/deploy/publish.</summary>
    public bool PushRole { get; set; }

    /// <summary>Human-readable label for the UI, e.g. "llama3 via Ollama" or "qwen2.5-coder:14b-instruct-q4_K_M via OpenClaw".</summary>
    public string BackendLabel => $"{Model} via {ExecutionBackend}";
}

public class CreateAgentDto
{
    public string Name { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string Role { get; set; } = "Kakarot";
    public string Description { get; set; } = string.Empty;
    public string Skills { get; set; } = string.Empty;
    public string Emoji { get; set; } = "🤖";
    public string? Color { get; set; }
    public string? OpenClawAgentId { get; set; }
    public int ProjectId { get; set; }

    /// <summary>"Ollama" or "OpenClaw"</summary>
    public string ExecutionBackend { get; set; } = "OpenClaw";

    public bool ToolsEnabled { get; set; }
    public bool PushRole { get; set; }
}

public record SpawnAgentDto(string Model);
