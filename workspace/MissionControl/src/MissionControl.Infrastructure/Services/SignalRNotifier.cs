using MissionControl.Application.DTOs;
using MissionControl.Application.Interfaces;

namespace MissionControl.Infrastructure.Services;

public class SignalRNotifier : ISignalRNotifier
{
    // AgentHub deleted. All methods stubbed as no-ops
    public Task NotifyTaskUpdated(TaskItemDto task) => Task.CompletedTask;
    public Task NotifyAgentActivity(ActivityLogDto log) => Task.CompletedTask;
    public Task NotifyAgentStarted(AgentDto agent) => Task.CompletedTask;
    public Task NotifyLogCreated(ActivityLogDto log) => Task.CompletedTask;
    public Task NotifyAgentLogLineAsync(string agentId, string line) => Task.CompletedTask;
    public Task NotifyPipelineTestProgressAsync(object payload) => Task.CompletedTask;
}
