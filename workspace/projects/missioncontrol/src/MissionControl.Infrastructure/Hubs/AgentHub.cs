using Microsoft.AspNetCore.SignalR;

namespace MissionControl.Infrastructure.Hubs;

public sealed class AgentHub : Hub
{
    public async Task TestConnection()
    {
        await Clients.Caller.SendAsync("ConnectionTestResult", new
        {
            ok = true,
            time = DateTime.UtcNow,
            connectionId = Context.ConnectionId
        });
    }
}
