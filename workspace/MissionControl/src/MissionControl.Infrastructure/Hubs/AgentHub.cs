using Microsoft.AspNetCore.SignalR;
using MissionControl.Application.DTOs;

namespace MissionControl.Infrastructure.Hubs;

/// <summary>
/// AgentHub — the real-time nerve centre of Mission Control.
/// All clients subscribe here for live task, agent, memory, and log events.
/// </summary>
public class AgentHub : Hub
{
    public async Task SendTaskUpdated(TaskItemDto task) =>
        await Clients.All.SendAsync("TaskUpdated", task);

    public async Task SendAgentActivity(ActivityLogDto log) =>
        await Clients.All.SendAsync("AgentActivity", log);

    public async Task SendMemoryAdded(MemoryEntryDto memory) =>
        await Clients.All.SendAsync("MemoryAdded", memory);

    public async Task SendAgentStarted(AgentDto agent) =>
        await Clients.All.SendAsync("AgentStarted", agent);

    public async Task SendLogCreated(ActivityLogDto log) =>
        await Clients.All.SendAsync("LogCreated", log);

    public async Task SendAgentLogLine(string agentId, string line) =>
        await Clients.All.SendAsync("AgentLogLine", new { agentId, line });

    /// <summary>
    /// Client-callable method: verifies the hub round-trip is healthy.
    /// Angular calls hub.invoke('TestConnection') and listens on 'ConnectionTestResult'.
    /// </summary>
    public async Task TestConnection() =>
        await Clients.Caller.SendAsync("ConnectionTestResult", new
        {
            ok = true,
            time = DateTime.UtcNow,
            connectionId = Context.ConnectionId
        });
}
