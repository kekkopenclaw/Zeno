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
        services.AddScoped<IMemoryRepository, MemoryRepository>();
        services.AddScoped<IMemorySummaryRepository, MemorySummaryRepository>();
        services.AddScoped<IActivityLogRepository, ActivityLogRepository>();
        services.AddScoped<ILogRepository, LogRepository>();
    services.AddScoped<TeamService>();

        // Infrastructure services
        services.AddScoped<ISignalRNotifier, SignalRNotifier>();

        // Observability — central log service
        services.AddScoped<ILogService, LogService>();

        // OpenClaw CLI runner (singleton — stateless, safe to share)
        services.AddSingleton<IOpenClawRunner, OpenClawRunner>();

        // Ollama HTTP client — direct local inference (http://127.0.0.1:11434)
        services.AddHttpClient<IOllamaClient, OllamaClient>((sp, client) =>
        {
            var config = sp.GetRequiredService<IConfiguration>();
            var baseUrl = config["Ollama:BaseUrl"] ?? "http://127.0.0.1:11434";
            client.BaseAddress = new Uri(baseUrl);
            client.Timeout = TimeSpan.FromSeconds(120);
        });

        // ChromaDB v2 vector store — CRN: default_tenant:default_database:mission_memories
        // Start Chroma with: chroma run --path ./chroma_data --port 8000
        // Degrades gracefully when ChromaDB is not running.
        services.AddHttpClient<IChromaVectorService, ChromaMemoryVectorService>((sp, client) =>
        {
            var config = sp.GetRequiredService<IConfiguration>();
            var baseUrl = config["Chroma:BaseUrl"] ?? "http://localhost:8000";
            client.BaseAddress = new Uri(baseUrl);
            client.Timeout = TimeSpan.FromSeconds(30);
        });

        // Core application services
        services.AddScoped<OrchestratorService>();
        services.AddScoped<ReviewService>();
        services.AddScoped<ContextLoaderService>();

        // ── New: Agent Loop + Swarm + Memory layers ─────────────────────────
        // AgentLoopService: observe→plan→execute→feedback with retries + YOLO sandbox
        services.AddScoped<AgentLoopService>();

        // SwarmCoordinatorService: Whis delegates tasks to specialized agents
        services.AddScoped<SwarmCoordinatorService>();

        // KairosSessionStore: singleton hot-layer for in-process session memory
        services.AddSingleton<KairosSessionStore>();

        // KairosMemoryService: 3-layer memory orchestrator (scoped — uses repos normally)
        services.AddScoped<KairosMemoryService>();

        // MemoryController: central retrieval + scoring + rule promotion
        services.AddScoped<IMemoryController, MemoryController>();

        // ReflectionLoopService: post-task Ollama reflection → LESSONS.md
        services.AddScoped<ReflectionLoopService>();

        // ── Background services ──────────────────────────────────────────────
        services.AddHostedService<BackgroundOrchestrationService>();
        services.AddHostedService<LogTailHostedService>();

        // KAIROS 2 AM daemon — nightly memory distillation
        services.AddHostedService<KairosDaemonService>();

        // Memory consolidation — prunes stale/low-value entries every 6 hours
        services.AddHostedService<MemoryConsolidationService>();

        return services;
    }
}

