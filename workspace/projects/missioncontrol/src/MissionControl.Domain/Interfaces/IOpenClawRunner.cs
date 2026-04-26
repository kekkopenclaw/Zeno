namespace MissionControl.Domain.Interfaces;

public interface IOpenClawRunner
{
    Task<bool> DeleteAgentAsync(string agentName, CancellationToken ct = default);
    /// <summary>
    /// Checks if an agent is registered in OpenClaw (via CLI).
    /// </summary>
    /// <summary>
    /// Checks if an agent is registered in OpenClaw (via CLI).
    /// </summary>
    Task<bool> AgentExistsAsync(string agentName, CancellationToken ct = default);
    string WorkspaceRoot { get; }
    Task<string> SpawnAgentAsync(string agentId, string model, string workspace, CancellationToken ct = default);
    Task TriggerTaskAsync(string agentId, string message, CancellationToken ct = default);
    Task PauseAgentAsync(string agentId, CancellationToken ct = default);
    string GetLogPath(string agentId);
    IAsyncEnumerable<string> TailLogStreamAsync(string agentId, CancellationToken ct = default);

    /// <summary>
    /// Gets the workspace path for an agent by querying the OpenClaw CLI config.
    /// </summary>
    Task<string?> GetWorkspacePathAsync(string agentName, CancellationToken ct = default);
}
