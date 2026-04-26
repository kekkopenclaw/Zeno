using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MissionControl.Application.Interfaces;
using MissionControl.Domain.Interfaces;

namespace MissionControl.Infrastructure.Services;

/// <summary>
/// Tails OpenClaw log files for all mc-* agents and broadcasts lines via SignalR.
/// Starts tailing on startup for any agent already in the DB whose name starts with "mc-".
/// </summary>
public sealed class LogTailHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOpenClawRunner _runner;
    private readonly ILogger<LogTailHostedService> _logger;
    private readonly Dictionary<string, CancellationTokenSource> _activeTails = new();
    private readonly SemaphoreSlim _lock = new(1, 1);

    public LogTailHostedService(
        IServiceScopeFactory scopeFactory,
        IOpenClawRunner runner,
        ILogger<LogTailHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _runner       = runner;
        _logger       = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Discover mc-* agents already in the DB and start tailing them
        try
        {
            await Task.Delay(3000, stoppingToken); // let EF migrations finish first
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown during startup delay
            return;
        }
        try
        {
            using var scope   = _scopeFactory.CreateScope();
            var agentRepo     = scope.ServiceProvider.GetRequiredService<MissionControl.Domain.Interfaces.IAgentRepository>();
            var allAgents     = await agentRepo.GetAllAsync();
            foreach (var agent in allAgents.Where(a => a.OpenClawAgentId != null))
            {
                await StartTailAsync(agent.OpenClawAgentId!, stoppingToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "LogTailHostedService startup discovery failed — will tail on demand only");
        }

        // Keep alive until host stops
        await Task.Delay(Timeout.Infinite, stoppingToken).ConfigureAwait(false);
    }

    public async Task StartTailAsync(string agentId, CancellationToken hostToken = default)
    {
        await _lock.WaitAsync(hostToken);
        try
        {
            if (_activeTails.ContainsKey(agentId)) return;
            var cts = CancellationTokenSource.CreateLinkedTokenSource(hostToken);
            _activeTails[agentId] = cts;
            _ = TailLoopAsync(agentId, cts.Token);
        }
        finally { _lock.Release(); }
    }

    public async Task StopTailAsync(string agentId)
    {
        await _lock.WaitAsync();
        try
        {
            if (!_activeTails.TryGetValue(agentId, out var cts)) return;
            await cts.CancelAsync();
            _activeTails.Remove(agentId);
        }
        finally { _lock.Release(); }
    }

    private async Task TailLoopAsync(string agentId, CancellationToken ct)
    {
        _logger.LogInformation("LogTail: starting tail for agent {AgentId}", agentId);
        try
        {
            await foreach (var line in _runner.TailLogStreamAsync(agentId, ct))
            {
                using var scope    = _scopeFactory.CreateScope();
                var notifier       = scope.ServiceProvider.GetRequiredService<ISignalRNotifier>();
                var logSvc         = scope.ServiceProvider.GetRequiredService<ILogService>();

                await notifier.NotifyAgentLogLineAsync(agentId, line);
                await logSvc.WriteAsync(
                    level:         "Info",
                    message:       line,
                    agentName:     $"mc-{agentId}",
                    taskId:        null,
                    correlationId: null,
                    action:        "LogTail",
                    source:        "OpenClaw");
            }
        }
        catch (OperationCanceledException) { /* normal shutdown */ }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "LogTail loop error for agent {AgentId}", agentId);
        }
        _logger.LogInformation("LogTail: stopped tail for agent {AgentId}", agentId);
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        foreach (var (_, cts) in _activeTails)
            await cts.CancelAsync();
        _activeTails.Clear();
        await base.StopAsync(cancellationToken);
    }
}
