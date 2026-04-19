using MissionControl.Application.DTOs;
using MissionControl.Application.Interfaces;
using MissionControl.Application.Options;
using MissionControl.Domain.Entities;
using MissionControl.Domain.Enums;
using MissionControl.Domain.Interfaces;
using Microsoft.Extensions.Options;

namespace MissionControl.Application.Services;

/// <summary>
/// Orchestrates tasks through the full 16-stage pipeline using skill-based dynamic routing.
/// No hardcoded role names — each stage selects agents by matching skill tags.
/// </summary>
public class OrchestratorService
{
    private readonly AgentService _agentService;
    private readonly ITaskRepository _taskRepository;
    private readonly IAgentRepository _agentRepository;
    private readonly IActivityLogRepository _logRepository;
    private readonly ISignalRNotifier _notifier;
    private readonly ILogService _logService;
    private readonly IOpenClawRunner? _openClawRunner;
    private readonly PipelineStageConfig _stageConfig;
    private static readonly Random _rng = new();

    // How long a task stays in Coding before auto-advancing in test mode (seconds)
    private const int CodingDurationSeconds = 45;

    public OrchestratorService(
        ITaskRepository taskRepository,
        IAgentRepository agentRepository,
        IActivityLogRepository logRepository,
        ISignalRNotifier notifier,
        ILogService logService,
        IOpenClawRunner? openClawRunner = null,
        IOptions<PipelineStageConfig>? stageConfig = null)
    {
        _taskRepository  = taskRepository;
        _agentRepository = agentRepository;
        _logRepository   = logRepository;
        _notifier        = notifier;
        _logService      = logService;
        _openClawRunner  = openClawRunner;
        _stageConfig     = stageConfig?.Value ?? new PipelineStageConfig();
        _agentService    = new AgentService(agentRepository, openClawRunner, notifier);
    }

    // ── Pipeline stage sequence ────────────────────────────────────────────────

    private static readonly (TaskItemStatus From, TaskItemStatus To, string Stage, string Emoji)[] PipelineTransitions =
    [
        (TaskItemStatus.Orchestration, TaskItemStatus.Architecture, "Architecture",  "😼"),
        (TaskItemStatus.Architecture,  TaskItemStatus.Tooling,      "Tooling",       "👩‍🔬"),
        (TaskItemStatus.Tooling,       TaskItemStatus.Coding,       "Coding",        "🦸‍♂️"),
        (TaskItemStatus.Security,      TaskItemStatus.Testing,      "Testing",       "🧑‍🦲"),
        (TaskItemStatus.Testing,       TaskItemStatus.Review,       "Review",        "👦"),
        (TaskItemStatus.Review,        TaskItemStatus.Compliance,   "Compliance",    "👽"),
        (TaskItemStatus.Compliance,    TaskItemStatus.Release,      "Release",       "🐉"),
        (TaskItemStatus.Release,       TaskItemStatus.Memory,       "Memory",        "💾"),
        (TaskItemStatus.Memory,        TaskItemStatus.Enforcement,  "Enforcement",   "💪"),
        (TaskItemStatus.Enforcement,   TaskItemStatus.Oversight,    "Oversight",     "👑"),
    ];

    /// <summary>
    /// Main orchestration tick — advances tasks through all pipeline stages.
    /// </summary>
    public async Task TickAsync(int projectId)
    {
        bool isTestMode = projectId == 1;
        var allTasks = await _taskRepository.GetByProjectIdAsync(projectId);
        var agents   = (await _agentRepository.GetByProjectIdAsync(projectId)).ToList();
        var now      = DateTime.UtcNow;
        var correlationId = Guid.NewGuid().ToString("N");

        // ── 0a. Todo → Decomposition or Orchestration ─────────────────────────
        foreach (var task in allTasks.Where(t => t.Status == TaskItemStatus.Todo))
        {
            bool needsDecomposition = task.ComplexityScore >= 7 || (task.Description?.Length ?? 0) > 300;
            if (needsDecomposition)
            {
                task.Status = TaskItemStatus.Decomposition;
                task.StatusEnteredAt = now;
                task.UpdatedAt = now;
                await _taskRepository.UpdateAsync(task);
                await LogAndNotify(projectId, task.AssignedAgentId,
                    $"🔀 '{task.Title}' moved to Decomposition.", task, "Todo→Decomposition", correlationId: correlationId);
                continue;
            }

            var orchestrator = FindAgentForStage("Orchestration", agents)
                ?? await EnsureAgentCreatedAsync(projectId, "Orchestration", agents);
            if (orchestrator == null) continue;

            agents = await RefreshAgentsAsync(projectId);
            orchestrator = FindAgentForStage("Orchestration", agents) ?? agents.FirstOrDefault(a => a.Status == AgentStatus.Idle);
            if (orchestrator == null) continue;

            await AssignToNextStageAsync(task, orchestrator, TaskItemStatus.Orchestration, "orchestration",
                $"🧙‍♂️ '{task.Title}' started Orchestration.", projectId, agents, correlationId, now);
        }

        // ── 0b. Decomposition → wait for subtasks or create them ──────────────
        foreach (var task in allTasks.Where(t => t.Status == TaskItemStatus.Decomposition))
        {
            var subtasks = allTasks.Where(t => t.ParentTaskId == task.Id).ToList();
            if (subtasks.Count == 0)
            {
                int subCount = Math.Min(4, Math.Max(2, (task.ComplexityScore / 3) + 2));
                for (int i = 1; i <= subCount; i++)
                {
                    await _taskRepository.AddAsync(new TaskItem
                    {
                        Title           = $"{task.Title} — Part {i}",
                        Description     = $"Subtask {i} of '{task.Title}'. {task.Description}",
                        Status          = TaskItemStatus.Todo,
                        Priority        = task.Priority,
                        ProjectId       = task.ProjectId,
                        ComplexityScore = Math.Max(1, task.ComplexityScore / subCount),
                        ParentTaskId    = task.Id,
                        CreatedAt       = now
                    });
                }
                await LogAndNotify(projectId, null, $"🔀 '{task.Title}' split into {subCount} subtasks.", task,
                    "Decomposition→Subtasks", correlationId: correlationId);
                continue;
            }
            if (subtasks.All(st => st.Status == TaskItemStatus.Done))
            {
                task.Status = TaskItemStatus.Orchestration;
                task.StatusEnteredAt = now;
                task.UpdatedAt = now;
                await _taskRepository.UpdateAsync(task);
                await LogAndNotify(projectId, task.AssignedAgentId,
                    $"🔀 '{task.Title}' all subtasks complete → Orchestration.", task,
                    "Decomposition→Orchestration", correlationId: correlationId);
            }
        }

        // ── Standard pipeline transitions ─────────────────────────────────────
        foreach (var (from, to, stageName, emoji) in PipelineTransitions)
        {
            foreach (var task in allTasks.Where(t => t.Status == from))
            {
                // Release the current agent
                await ReleaseCurrentAgentAsync(task, agents);

                var next = FindAgentForStage(stageName, agents);
                if (next == null) continue;

                await AssignToNextStageAsync(task, next, to, stageName.ToLowerInvariant(),
                    $"{emoji} '{task.Title}' started {stageName}.", projectId, agents, correlationId, now);
            }
        }

        // ── Coding → Refactoring (needs completion check) ─────────────────────
        foreach (var task in allTasks.Where(t => t.Status == TaskItemStatus.Coding))
        {
            var coder = task.AssignedAgentId.HasValue ? agents.FirstOrDefault(a => a.Id == task.AssignedAgentId) : null;
            if (coder == null) continue;

            bool done = isTestMode
                || (_openClawRunner != null && await AgentCompletionChecker.IsAgentWorkCompletedAsync(task, coder, _openClawRunner));
            if (!done) continue;

            await ReleaseCurrentAgentAsync(task, agents);
            var refactorer = FindAgentForStage("Refactoring", agents);
            if (refactorer == null) continue;
            await AssignToNextStageAsync(task, refactorer, TaskItemStatus.Refactoring, "refactoring",
                $"🌿 '{task.Title}' started Refactoring.", projectId, agents, correlationId, now);
        }

        // ── Refactoring → Security (may auto-create Security agent) ───────────
        foreach (var task in allTasks.Where(t => t.Status == TaskItemStatus.Refactoring))
        {
            await ReleaseCurrentAgentAsync(task, agents);

            var sec = FindAgentForStage("Security", agents);
            if (sec == null)
            {
                sec = await EnsureAgentCreatedAsync(projectId, "Security", agents);
                if (sec == null) continue;
                agents = await RefreshAgentsAsync(projectId);
                sec = FindAgentForStage("Security", agents);
                if (sec == null) continue;
            }

            if (sec.Status != AgentStatus.Idle) continue;
            await AssignToNextStageAsync(task, sec, TaskItemStatus.Security, "security",
                $"🦗 '{task.Title}' started Security Audit.", projectId, agents, correlationId, now);
        }

        // ── Oversight → Done (evidence gate) ──────────────────────────────────
        foreach (var task in allTasks.Where(t => t.Status == TaskItemStatus.Oversight))
        {
            var agent = task.AssignedAgentId.HasValue ? agents.FirstOrDefault(a => a.Id == task.AssignedAgentId) : null;
            if (agent != null)
            {
                await OrchestratorServiceUtils.WriteAgentHandoffFileAsync(task, agent, "oversight");
                agent.Status = AgentStatus.Idle;
                await _agentRepository.UpdateAsync(agent);
                await _notifier.NotifyAgentStartedAsync(AgentService.MapToDto(agent));
            }

            if (agent != null && !await HasCompletionEvidenceAsync(task, agent, "oversight"))
                continue;

            await OrchestratorServiceUtils.WriteAgentHandoffFileAsync(
                task, agent ?? new Agent { Name = "Unknown" }, "done");
            task.Status = TaskItemStatus.Done;
            task.StatusEnteredAt = now;
            task.UpdatedAt = now;
            await _taskRepository.UpdateAsync(task);
            await LogAndNotify(projectId, task.AssignedAgentId,
                $"✅ '{task.Title}' is Done!", task, "Oversight→Done", correlationId: correlationId);
        }

        // ── Release Done agents ────────────────────────────────────────────────
        foreach (var task in allTasks.Where(t => t.Status == TaskItemStatus.Done))
        {
            var trunks = task.AssignedAgentId.HasValue
                ? agents.FirstOrDefault(a => a.Id == task.AssignedAgentId && a.Status == AgentStatus.Working)
                : null;
            if (trunks != null)
            {
                trunks.Status = AgentStatus.Idle;
                await _agentRepository.UpdateAsync(trunks);
                await _notifier.NotifyAgentStartedAsync(AgentService.MapToDto(trunks));
            }
        }
    }

    // ── Dynamic agent selection ────────────────────────────────────────────────

    /// <summary>
    /// Finds the first idle, unpaused agent whose Skills contain any required keyword for the given stage.
    /// Falls back to any idle unpaused agent if no skill match is found.
    /// </summary>
    private Agent? FindAgentForStage(string stageName, List<Agent> agents)
    {
        var idle = agents.Where(a => !a.IsPaused && a.Status == AgentStatus.Idle).ToList();
        if (idle.Count == 0) return null;

        if (_stageConfig.StageSkillRequirements.TryGetValue(stageName, out var required) && required.Count > 0)
        {
            var match = idle.FirstOrDefault(a =>
            {
                var skills = (a.Skills ?? "").Split(',').Select(s => s.Trim()).ToArray();
                return required.Any(req =>
                    skills.Any(sk => sk.Contains(req, StringComparison.OrdinalIgnoreCase)));
            });
            if (match != null) return match;
        }

        // Fallback: any idle agent
        return idle.FirstOrDefault();
    }

    /// <summary>
    /// Auto-creates a minimal agent for the given stage when none is found.
    /// </summary>
    private async Task<Agent?> EnsureAgentCreatedAsync(int projectId, string stageName, List<Agent> agents)
    {
        if (FindAgentForStage(stageName, agents) != null) return null; // already exists

        var skillMap = new Dictionary<string, string>
        {
            ["Orchestration"] = "Orchestration,Routing,Planning",
            ["Architecture"]  = "Architecture,SystemDesign,Planning",
            ["Tooling"]       = "Tooling,CI/CD,DevOps",
            ["Security"]      = "Security,Audit,Analysis",
            ["Oversight"]     = "Oversight,Supervision,Approval"
        };

        var dto = new CreateAgentDto
        {
            Name             = stageName,
            Role             = stageName,
            Model            = "gpt-4o-mini",
            Description      = $"Auto-created agent for {stageName} stage",
            Skills           = skillMap.GetValueOrDefault(stageName, stageName),
            Emoji            = "🤖",
            ProjectId        = projectId,
            ExecutionBackend = "OpenClaw",
            ToolsEnabled     = true,
            PushRole         = false
        };
        await _agentService.CreateAsync(dto);
        return null; // caller must re-query
    }

    private async Task<List<Agent>> RefreshAgentsAsync(int projectId) =>
        (await _agentRepository.GetByProjectIdAsync(projectId)).ToList();

    // ── Agent lifecycle helpers ────────────────────────────────────────────────

    private async Task ReleaseCurrentAgentAsync(TaskItem task, List<Agent> agents)
    {
        if (!task.AssignedAgentId.HasValue) return;
        var prev = agents.FirstOrDefault(a => a.Id == task.AssignedAgentId.Value);
        if (prev == null || prev.Status != AgentStatus.Working) return;

        prev.Status = AgentStatus.Idle;
        await _agentRepository.UpdateAsync(prev);
        await _notifier.NotifyAgentStartedAsync(AgentService.MapToDto(prev));
    }

    private async Task AssignToNextStageAsync(
        TaskItem task, Agent agent, TaskItemStatus newStatus,
        string handoffStage, string logMessage,
        int projectId, List<Agent> agents, string correlationId, DateTime now)
    {
        await OrchestratorServiceUtils.WriteAgentHandoffFileAsync(task, agent, handoffStage);
        task.AssignedAgentId = agent.Id;
        agent.Status = AgentStatus.Working;
        await _agentRepository.UpdateAsync(agent);
        await _notifier.NotifyAgentStartedAsync(AgentService.MapToDto(agent));
        task.Status          = newStatus;
        task.StatusEnteredAt = now;
        task.UpdatedAt       = now;
        await _taskRepository.UpdateAsync(task);
        await LogAndNotify(projectId, task.AssignedAgentId, logMessage, task,
            $"→{newStatus}", agentName: agent.Name, correlationId: correlationId);
        await DispatchAssignedAgentAsync(task, agents, handoffStage, correlationId);
    }

    // ── Logging ────────────────────────────────────────────────────────────────

    private async Task LogAndNotify(int projectId, int? agentId, string message, TaskItem task,
        string action = "StateTransition", string? agentName = null, string? correlationId = null)
    {
        var log = new ActivityLog
        {
            ProjectId = projectId,
            AgentId   = agentId,
            Message   = message,
            Timestamp = DateTime.UtcNow
        };
        var saved = await _logRepository.AddAsync(log);
        await _notifier.NotifyLogCreatedAsync(new ActivityLogDto
        {
            Id        = saved.Id,
            ProjectId = saved.ProjectId,
            AgentId   = saved.AgentId,
            Message   = saved.Message,
            Timestamp = saved.Timestamp
        });
        await _notifier.NotifyTaskUpdatedAsync(TaskService.MapToDto(task));
        await _logService.WriteAsync(
            level: "Info", message: message, agentName: agentName,
            taskId: task.Id.ToString(), correlationId: correlationId ?? Guid.NewGuid().ToString("N"),
            action: action, source: "Orchestrator");
    }

    // ── OpenClaw integration ───────────────────────────────────────────────────

    private async Task SpawnOrTriggerAgentAsync(Agent agent, TaskItem task, string correlationId)
    {
        if (_openClawRunner == null || agent.ExecutionBackend != ExecutionBackend.OpenClaw) return;
        try
        {
            var agentName = ResolveAgentRuntimeName(agent);
            var model     = string.IsNullOrWhiteSpace(agent.Model) ? "github-copilot/gpt-4.1" : agent.Model;
            var ws        = await _openClawRunner.GetWorkspacePathAsync(agentName)
                            ?? Path.Combine(_openClawRunner.WorkspaceRoot, agentName);
            await _openClawRunner.SpawnAgentAsync(agentName, model, ws);
            var prompt = BuildAgentPrompt(agent, task) + $"\nCorrelationId: {correlationId}";
            await _openClawRunner.TriggerTaskAsync(agentName, prompt);
        }
        catch
        {
            // OpenClaw CLI is optional — degrade gracefully
        }
    }

    private static string BuildAgentPrompt(Agent agent, TaskItem task)
    {
        // Build prompt using skills rather than role name
        var skills = string.IsNullOrWhiteSpace(agent.Skills) ? agent.Role.ToString() : agent.Skills;
        return $"You are {agent.Name}, an AI agent with skills: {skills}. " +
               $"Work on task [{task.Id}]: {task.Title}. {task.Description} " +
               $"Priority: {task.Priority}. Complexity: {task.ComplexityScore}/10.";
    }

    private static string ResolveAgentRuntimeName(Agent agent)
    {
        if (!string.IsNullOrWhiteSpace(agent.OpenClawAgentId))
            return agent.OpenClawAgentId;
        var sanitized = string.Concat(agent.Name.ToLowerInvariant().Replace(" ", "-")
            .Where(c => char.IsLetterOrDigit(c) || c == '-'));
        return $"mc-{agent.Id}-{sanitized}";
    }

    private async Task DispatchAssignedAgentAsync(TaskItem task, List<Agent> agents, string stage, string correlationId)
    {
        if (!task.AssignedAgentId.HasValue) return;
        var assigned = agents.FirstOrDefault(a => a.Id == task.AssignedAgentId.Value);
        if (assigned == null || assigned.ExecutionBackend != ExecutionBackend.OpenClaw || _openClawRunner == null) return;
        await OrchestratorServiceUtils.WriteAgentHandoffFileAsync(task, assigned, stage);
        await SpawnOrTriggerAgentAsync(assigned, task, correlationId);
    }

    private async Task<bool> HasCompletionEvidenceAsync(TaskItem task, Agent? agent, string stage)
    {
        if (_openClawRunner == null || agent == null) return false;
        var runtimeName = ResolveAgentRuntimeName(agent);
        var workspace   = await _openClawRunner.GetWorkspacePathAsync(runtimeName)
                          ?? Path.Combine(_openClawRunner.WorkspaceRoot, runtimeName);
        var workspaceExists = Directory.Exists(workspace) &&
                              Directory.EnumerateFileSystemEntries(workspace, "*", SearchOption.TopDirectoryOnly).Any();
        var home        = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var handoffDir  = Path.Combine(home, ".openclaw", "shared", task.ProjectId.ToString(), "handoff");
        var handoffExists = Directory.Exists(handoffDir) &&
                            Directory.EnumerateFiles(handoffDir, $"task-{task.Id}-{stage}-*.md").Any();
        var logFile     = Path.Combine(home, ".openclaw", "logs", $"{runtimeName}.log");
        return workspaceExists || handoffExists || File.Exists(logFile);
    }

    // ── Utility ────────────────────────────────────────────────────────────────

    private static bool TaskRequiresTools(TaskItem task)
    {
        var text = $"{task.Title} {task.Description}";
        string[] kws = ["deploy", "publish", "push", "release", "ci/cd", "pipeline",
                         "connect", "external", "webhook", "api key", "secret"];
        return kws.Any(kw => text.Contains(kw, StringComparison.OrdinalIgnoreCase));
    }

    public static class OrchestratorServiceUtils
    {
        public static async Task WriteAgentHandoffFileAsync(TaskItem task, Agent agent, string stage)
        {
            var home       = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var dir        = Path.Combine(home, ".openclaw", "shared", task.ProjectId.ToString(), "handoff");
            Directory.CreateDirectory(dir);
            var file    = Path.Combine(dir, $"task-{task.Id}-{stage}-{task.Status}.md");
            var content = $"# Handoff: {stage} ({task.Status})\n" +
                          $"Agent: {agent.Name}\n" +
                          $"Task: {task.Title}\n" +
                          $"Status: {task.Status}\n" +
                          $"TimestampUtc: {DateTime.UtcNow:O}\n---\nOutput: agent execution dispatched\n";
            await File.WriteAllTextAsync(file, content);
        }
    }
}
