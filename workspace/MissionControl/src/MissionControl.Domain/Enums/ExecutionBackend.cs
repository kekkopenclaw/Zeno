namespace MissionControl.Domain.Enums;

/// <summary>
/// Distinguishes where an agent's LLM inference is executed.
/// Ollama  — fast local inference via http://127.0.0.1:11434; no tool/function calling.
/// OpenClaw — advanced agent runner with tool/skill/plugin support, agent-to-agent comms, memory, debate.
/// </summary>
public enum ExecutionBackend
{
    Ollama,
    OpenClaw
}
