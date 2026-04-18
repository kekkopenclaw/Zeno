using Microsoft.AspNetCore.SignalR;
using MissionControl.Application.DTOs;
using MissionControl.Application.Interfaces;
using MissionControl.Infrastructure.Hubs;

namespace MissionControl.Infrastructure.Services;

public class SignalRNotifier : ISignalRNotifier
{
    private readonly IHubContext<AgentHub> _hubContext;

    public SignalRNotifier(IHubContext<AgentHub> hubContext)
    {
        _hubContext = hubContext;
    }

    public Task NotifyTaskUpdated(TaskItemDto task) =>
        _hubContext.Clients.All.SendAsync("TaskUpdated", task);

    public Task NotifyAgentActivity(ActivityLogDto log) =>
        _hubContext.Clients.All.SendAsync("AgentActivity", log);

    public Task NotifyMemoryAdded(MemoryEntryDto memory) =>
        _hubContext.Clients.All.SendAsync("MemoryAdded", memory);

    public Task NotifyAgentStarted(AgentDto agent) =>
        _hubContext.Clients.All.SendAsync("AgentStarted", agent);

    public Task NotifyLogCreated(ActivityLogDto log) =>
        _hubContext.Clients.All.SendAsync("LogCreated", log);

    public Task NotifyAgentLogLineAsync(string agentId, string line) =>
        _hubContext.Clients.All.SendAsync("AgentLogLine", new { agentId, line });

    public Task NotifyPipelineTestProgressAsync(object payload) =>
        _hubContext.Clients.All.SendAsync("PipelineTestProgress", payload);

    public Task NotifyMemorySummaryAddedAsync(MemorySummaryDto summary) =>
        _hubContext.Clients.All.SendAsync("MemorySummaryAdded", summary);
}
