using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MissionControl.Application.Interfaces;
using MissionControl.Application.Services;
using MissionControl.Domain.Interfaces;
using MissionControl.Infrastructure.Data;
using MissionControl.Infrastructure.Repositories;
using MissionControl.Infrastructure.Services;

namespace MissionControl.Infrastructure;

public static class InfrastructureServiceExtensions
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<AppDbContext>(options =>
            options.UseSqlite(connectionString));

        // Repositories
        services.AddScoped<IProjectRepository, ProjectRepository>();
        services.AddScoped<IAgentRepository, AgentRepository>();
        services.AddScoped<ITeamRepository, TeamRepository>();
        services.AddScoped<ITaskRepository, TaskRepository>();
        services.AddScoped<IActivityLogRepository, ActivityLogRepository>();
        services.AddScoped<ILogRepository, LogRepository>();
        services.AddScoped<TeamService>();
        services.AddScoped<ISignalRNotifier, SignalRNotifier>();
        services.AddScoped<ILogService, LogService>();
        services.AddSingleton<IOpenClawRunner, OpenClawRunner>();
        services.AddHttpClient<IOllamaClient, OllamaClient>((sp, client) =>
        {
            var config = sp.GetRequiredService<IConfiguration>();
            var baseUrl = config["Ollama:BaseUrl"] ?? "http://127.0.0.1:11434";
            client.BaseAddress = new Uri(baseUrl);
            client.Timeout = TimeSpan.FromSeconds(120);
        });
        // ChromaDB v2 vector store section skipped (now legacy)
        services.AddScoped<OrchestratorService>();
        services.AddScoped<ReviewService>();
        services.AddScoped<ContextLoaderService>();
        services.AddScoped<AgentLoopService>();
        services.AddScoped<SwarmCoordinatorService>();
        services.AddScoped<ReflectionLoopService>();
        services.AddHostedService<BackgroundOrchestrationService>();
        services.AddHostedService<LogTailHostedService>();
        return services;
    }
}
