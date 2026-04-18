using Microsoft.Extensions.Logging;
using MissionControl.Application.DTOs;
using MissionControl.Application.Interfaces;
using MissionControl.Domain.Enums;
using MissionControl.Domain.Interfaces;

namespace MissionControl.Application.Services;

/// <summary>
/// SwarmCoordinatorService — ported from Claw-code multi-agent swarm patterns.
///
/// Whis (the brain) analyses each task and delegates to the most suitable specialist:
///   Beerus   → Architecture, system design, high-level planning
///   Kakarot  → Standard feature coding, straightforward tasks
///   Vegeta   → Advanced/complex coding, escalated retries, critical priority
///   Piccolo  → Refactoring, cleanup, technical debt
///   Gohan    → Code review, quality gates, test coverage
///   Trunks   → Memory distillation, learning extraction, lessons
///   Bulma    → CI/CD, deployments, tooling, external integrations
///
/// The coordinator returns a delegation plan (which agent handles which role)
/// and fires SignalR broadcasts at each decision point.
/// </summary>
public sealed class SwarmCoordinatorService
{
    private readonly IAgentRepository _agentRepo;
    private readonly ITaskRepository _taskRepo;
    private readonly ISignalRNotifier _notifier;
    private readonly ILogService _log;
    private readonly ILogger<SwarmCoordinatorService> _logger;

    public SwarmCoordinatorService(
        IAgentRepository agentRepo,
        ITaskRepository taskRepo,
        ISignalRNotifier notifier,
        ILogService log,
        ILogger<SwarmCoordinatorService> logger)
    {
        _agentRepo = agentRepo;
        _taskRepo = taskRepo;
        _notifier = notifier;
        _log = log;
        _logger = logger;
    }

    /// <summary>
    /// Whis analyses the task and produces a SwarmPlan — the ordered list of agents
    /// that should execute, in which role.
    /// </summary>
    public async Task<SwarmPlan> BuildPlanAsync(int projectId, TaskItemDto task, CancellationToken ct = default)
    {
        var agents = (await _agentRepo.GetByProjectIdAsync(projectId)).ToList();
        var correlationId = Guid.NewGuid().ToString("N")[..12];

        var plan = new SwarmPlan
        {
            TaskId = task.Id,
            TaskTitle = task.Title,
            CorrelationId = correlationId
        };

        var title = task.Title.ToLowerInvariant();
        var desc = (task.Description ?? "").ToLowerInvariant();
        var combined = title + " " + desc;

        // ── Step 1: pick executor (who does the work) ──────────────────────
        AgentDto? executor = null;

        bool needsTool = ContainsAny(combined,
            "deploy", "publish", "push", "release", "ci/cd", "pipeline",
            "connect", "external", "webhook", "api key", "secret", "docker");

        bool isArchitectural = ContainsAny(combined, "architect", "design", "system", "infrastructure");
        bool isRefactor = ContainsAny(combined, "refactor", "cleanup", "clean up", "tech debt", "rework");
        bool isMemory = ContainsAny(combined, "memory", "distill", "learn", "lesson", "summarise", "summarize");

        if (needsTool)
        {
            // Bulma has tool access for deployments/integrations
            executor = FindAgent(agents, AgentRole.Bulma)
                ?? FindByToolsEnabled(agents, true);
        }
        else if (isArchitectural)
        {
            executor = FindAgent(agents, AgentRole.Beerus)
                ?? FindAgent(agents, AgentRole.Vegeta);
        }
        else if (isRefactor)
        {
            executor = FindAgent(agents, AgentRole.Piccolo)
                ?? FindAgent(agents, AgentRole.Kakarot);
        }
        else if (isMemory)
        {
            executor = FindAgent(agents, AgentRole.Trunks)
                ?? FindAgent(agents, AgentRole.Piccolo);
        }
        else if (task.ComplexityScore >= 7 || task.Priority == "Critical")
        {
            executor = FindAgent(agents, AgentRole.Vegeta)
                ?? FindAgent(agents, AgentRole.Kakarot);
        }
        else
        {
            executor = FindAgent(agents, AgentRole.Kakarot)
                ?? FindAgent(agents, AgentRole.Vegeta)
                ?? agents.Where(a => a.Status == AgentStatus.Idle).Select(a => AgentService.MapToDto(a)).FirstOrDefault();
        }

        if (executor != null)
            plan.Executor = executor;

        // ── Step 2: pick reviewer ───────────────────────────────────────────
        plan.Reviewer = FindAgent(agents, AgentRole.Gohan)
            ?? FindAgent(agents, AgentRole.Vegeta);

        // ── Step 3: pick memory agent ───────────────────────────────────────
        plan.MemoryAgent = FindAgent(agents, AgentRole.Trunks);

        // ── Broadcast plan to UI ────────────────────────────────────────────
        var summary = $"🌐 Whis swarm plan for '{task.Title}': " +
            $"Executor={plan.Executor?.Name ?? "none"}, " +
            $"Reviewer={plan.Reviewer?.Name ?? "none"}, " +
            $"Memory={plan.MemoryAgent?.Name ?? "none"}";

        _logger.LogInformation(summary);
        await _log.WriteAsync("Info", summary, "Whis", task.Id.ToString(), correlationId, "SwarmPlan", source: "SwarmCoordinator");
        await _notifier.NotifyPipelineTestProgressAsync(new
        {
            step = "SwarmPlan",
            taskId = task.Id,
            message = summary,
            executor = plan.Executor?.Name,
            reviewer = plan.Reviewer?.Name,
            memoryAgent = plan.MemoryAgent?.Name,
            correlationId
        });

        return plan;
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private static AgentDto? FindAgent(List<Domain.Entities.Agent> agents, AgentRole role)
    {
        var a = agents.FirstOrDefault(x => x.Role == role);
        return a == null ? null : AgentService.MapToDto(a);
    }

    private static AgentDto? FindByToolsEnabled(List<Domain.Entities.Agent> agents, bool toolsEnabled)
    {
        var a = agents.FirstOrDefault(x => x.ToolsEnabled == toolsEnabled && x.Status == AgentStatus.Idle);
        return a == null ? null : AgentService.MapToDto(a);
    }

    private static bool ContainsAny(string text, params string[] keywords) =>
        keywords.Any(k => text.Contains(k, StringComparison.OrdinalIgnoreCase));
}

// ── Plan value object ─────────────────────────────────────────────────────────

/// <summary>
/// Describes which agent handles each role in a swarm execution run.
/// </summary>
public sealed class SwarmPlan
{
    public int TaskId { get; init; }
    public string TaskTitle { get; init; } = string.Empty;
    public string CorrelationId { get; init; } = string.Empty;

    /// <summary>Primary executor — writes/implements the work.</summary>
    public AgentDto? Executor { get; set; }

    /// <summary>Quality reviewer — validates the executor's output.</summary>
    public AgentDto? Reviewer { get; set; }

    /// <summary>Memory agent — distils lessons from the execution outcome.</summary>
    public AgentDto? MemoryAgent { get; set; }
}
