using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MissionControl.Application.Services;
using MissionControl.Domain.Interfaces;

namespace MissionControl.Infrastructure.Services;

/// <summary>
/// Background service that runs the orchestration loop continuously.
/// Ticks every 30 seconds — picks tasks, advances state machine, routes agents.
/// This is the heartbeat of the autonomous AI lab.
/// </summary>
public sealed class BackgroundOrchestrationService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<BackgroundOrchestrationService> _logger;
    private readonly TimeSpan _tickInterval;

    public BackgroundOrchestrationService(
        IServiceScopeFactory scopeFactory,
        ILogger<BackgroundOrchestrationService> logger,
        IConfiguration configuration)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        var intervalSeconds = configuration.GetValue<int>("Orchestration:TickIntervalSeconds", 2);
        _tickInterval = TimeSpan.FromSeconds(Math.Clamp(intervalSeconds, 1, 60));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("🚀 Whis (BackgroundOrchestrationService) started — ticking every {Interval}s.",
            _tickInterval.TotalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await TickAllProjectsAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Orchestration tick failed.");
            }

            await Task.Delay(_tickInterval, stoppingToken);
        }

        _logger.LogInformation("Whis stopped.");
    }

    private async Task TickAllProjectsAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var projectRepo = scope.ServiceProvider.GetRequiredService<IProjectRepository>();
        var orchestrator = scope.ServiceProvider.GetRequiredService<OrchestratorService>();

        var projects = await projectRepo.GetAllAsync();
        foreach (var project in projects)
        {
            _logger.LogDebug("Orchestration tick for project {ProjectId} ({ProjectName})", project.Id, project.Name);
            await orchestrator.TickAsync(project.Id);
        }
    }
}
