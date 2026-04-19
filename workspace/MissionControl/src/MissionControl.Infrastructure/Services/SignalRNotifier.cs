using MissionControl.Application.DTOs;
using MissionControl.Application.Interfaces;
using MissionControl.Infrastructure.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace MissionControl.Infrastructure.Services;

public class SignalRNotifier : ISignalRNotifier
{
    private readonly IHubContext<AgentHub> _hub;

    public SignalRNotifier(IHubContext<AgentHub> hub)
    {
        _hub = hub;
    }

    public Task NotifyTaskUpdated(TaskItemDto task) =>
        _hub.Clients.All.SendAsync("TaskUpdated", task);

    public Task NotifyAgentActivity(ActivityLogDto log) =>
        _hub.Clients.All.SendAsync("AgentActivityUpdated", log);

    public Task NotifyAgentStarted(AgentDto agent) =>
        _hub.Clients.All.SendAsync("AgentStarted", agent);

    public Task NotifyLogCreated(ActivityLogDto log) =>
        _hub.Clients.All.SendAsync("LogCreated", log);

    public Task NotifyAgentLogLineAsync(string agentId, string line) =>
        _hub.Clients.All.SendAsync("AgentLogLine", new { agentId, line });

    public Task NotifyPipelineTestProgressAsync(object payload) =>
        _hub.Clients.All.SendAsync("PipelineTestProgress", payload);
}
