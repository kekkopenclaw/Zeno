using MissionControl.Application.DTOs;

namespace MissionControl.Application.Interfaces;

public interface ISignalRNotifier
{
    Task NotifyTaskUpdated(TaskItemDto task);
    Task NotifyAgentActivity(ActivityLogDto log);
    Task NotifyAgentStarted(AgentDto agent);
    Task NotifyLogCreated(ActivityLogDto log);

    // Async aliases for use in background/orchestration services
    Task NotifyTaskUpdatedAsync(TaskItemDto task) => NotifyTaskUpdated(task);
    Task NotifyLogCreatedAsync(ActivityLogDto log) => NotifyLogCreated(log);
    Task NotifyAgentStartedAsync(AgentDto agent) => NotifyAgentStarted(agent);
    Task NotifyAgentUpdatedAsync(AgentDto agent) => NotifyAgentUpdated(agent);
    Task NotifyAgentUpdated(AgentDto agent);
    Task NotifyAgentLogLineAsync(string agentId, string line);

    /// <summary>Broadcasts a pipeline-test progress event to all connected clients.</summary>
    Task NotifyPipelineTestProgressAsync(object payload);

}
