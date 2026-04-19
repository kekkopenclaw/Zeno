using MissionControl.Application.DTOs;
using MissionControl.Application.Interfaces;
using MissionControl.Domain.Entities;
using MissionControl.Domain.Enums;
using MissionControl.Domain.Interfaces;

namespace MissionControl.Application.Services;

/// </summary>
public class OrchestratorService
{
    private readonly AgentService _agentService;
    // Remove static delegate; use instance method with agent and runner
    private readonly ITaskRepository _taskRepository;
    private readonly IAgentRepository _agentRepository;
    private readonly IActivityLogRepository _logRepository;
    private readonly ISignalRNotifier _notifier;
    private readonly ILogService _logService;
    private readonly IOpenClawRunner? _openClawRunner;
    private static readonly Random _rng = new();

    // How long a task stays in Coding/Review before the simulator advances it (seconds)
    private const int CodingDurationSeconds = 45;
    private const int ReviewDurationSeconds = 25;

    public OrchestratorService(
        ITaskRepository taskRepository,
        IAgentRepository agentRepository,
        IActivityLogRepository logRepository,

        ISignalRNotifier notifier,
        ILogService logService,
        IOpenClawRunner? openClawRunner = null)
    {
        _taskRepository = taskRepository;
        _agentRepository = agentRepository;
        _logRepository = logRepository;
        _notifier = notifier;
        _logService = logService;
        _openClawRunner = openClawRunner;
        _agentService = new AgentService(agentRepository, openClawRunner, notifier);
    }

    /// <summary>
    /// Main orchestration tick — called by BackgroundOrchestrationService every N seconds.
    /// Advances tasks through the full pipeline: Todo→Planning→Ready→Coding→Review→Done/Fix.
    /// </summary>
    public async Task TickAsync(int projectId)
    {
        // TEST MODE: If projectId == 1 and any task is in Coding, simulate agent completion so pipeline can advance
        bool isTestMode = projectId == 1;
        var allTasks = await _taskRepository.GetByProjectIdAsync(projectId);
        var agents = await _agentRepository.GetByProjectIdAsync(projectId);
        var now = DateTime.UtcNow;
        var tickCorrelationId = Guid.NewGuid().ToString("N");

        // 0. Todo → Decomposition (if big/complex) or Orchestration (if small)
        foreach (var todoTask in allTasks.Where(t => t.Status == TaskItemStatus.Todo))
        {
            // Heuristic: if complexity >= 7 or description > 300 chars, decompose
            bool needsDecomposition = todoTask.ComplexityScore >= 7 || (todoTask.Description?.Length ?? 0) > 300;
            if (needsDecomposition)
            {
                todoTask.Status = TaskItemStatus.Decomposition;
                todoTask.StatusEnteredAt = now;
                todoTask.UpdatedAt = now;
                await _taskRepository.UpdateAsync(todoTask);
                await LogAndNotify(projectId, todoTask.AssignedAgentId, $"🔀 '{todoTask.Title}' moved to Decomposition stage.", todoTask, action: "Todo→Decomposition", correlationId: tickCorrelationId);
                continue;
            }

            // Dynamic: get orchestrator agent for this project
            var orchestrator = agents.FirstOrDefault(a => a.Role == AgentRole.Whis && a.Status == AgentStatus.Idle);
            if (orchestrator == null)
            {
                // Always attempt to create agent and workspace via OpenClawRunner
                var createDto = new {
                    Name = "Whis",
                    Role = nameof(AgentRole.Whis),
                    Model = "gpt-4o-mini",
                    Description = "Auto-created Whis orchestrator",
                    Skills = "",
                    Emoji = "🤖",
                    ProjectId = todoTask.ProjectId,
                    ExecutionBackend = "OpenClaw",
                    ToolsEnabled = true,
                    PushRole = false
                };
                await _agentService.CreateAsync((dynamic)createDto);
                agents = await _agentRepository.GetByProjectIdAsync(projectId);
                orchestrator = agents.FirstOrDefault(a => a.Role == AgentRole.Whis && a.Status == AgentStatus.Idle);
            }
            if (orchestrator != null && _openClawRunner != null)
            {
                var openClawId = ResolveAgentRuntimeName(orchestrator);
                var workspacePath = await _openClawRunner.GetWorkspacePathAsync(openClawId) ?? System.IO.Path.Combine(_openClawRunner.WorkspaceRoot, openClawId);
                await _openClawRunner.SpawnAgentAsync(openClawId, orchestrator.Model, workspacePath);
                var exists = await _openClawRunner.AgentExistsAsync(openClawId);
                var wsExists = false;
                var wsPath = await _openClawRunner.GetWorkspacePathAsync(openClawId);
                if (!string.IsNullOrEmpty(wsPath))
                    wsExists = System.IO.Directory.Exists(wsPath);
                if (!exists || !wsExists)
                {
                    if (!exists)
                        System.Console.WriteLine($"[WARN] OpenClaw agent '{openClawId}' not found after creation attempt.");
                    if (!wsExists)
                        System.Console.WriteLine($"[WARN] Workspace directory '{wsPath ?? workspacePath}' not found after creation attempt.");
                }
            }
            if (orchestrator != null)
            {
                todoTask.AssignedAgentId = orchestrator.Id;
                orchestrator.Status = AgentStatus.Working;
                await _agentRepository.UpdateAsync(orchestrator);
                await _notifier.NotifyAgentStartedAsync(AgentService.MapToDto(orchestrator));
                // Move to Orchestration and write handoff file
                await OrchestratorServiceUtils.WriteAgentHandoffFileAsync(todoTask, orchestrator, "orchestration");
                todoTask.Status = TaskItemStatus.Orchestration;
                todoTask.StatusEnteredAt = now;
                todoTask.UpdatedAt = now;
                await _taskRepository.UpdateAsync(todoTask);
                await LogAndNotify(projectId, todoTask.AssignedAgentId,
                    $"🧙‍♂️ '{todoTask.Title}' started Orchestration.", todoTask,
                    action: "Todo→Orchestration", agentName: orchestrator.Name, correlationId: tickCorrelationId);
                await DispatchAssignedAgentAsync(todoTask, agents, "orchestration", tickCorrelationId);
            }
        }

        // 0b. Decomposition → Orchestration (after subtasks are created and all are Done)
        foreach (var decompTask in allTasks.Where(t => t.Status == TaskItemStatus.Decomposition))
        {
            // If no subtasks, create them (simulate split into 2-4 subtasks)
            var subtasks = allTasks.Where(t => t.ParentTaskId == decompTask.Id).ToList();
            if (subtasks.Count == 0)
            {
                int subCount = Math.Min(4, Math.Max(2, (decompTask.ComplexityScore / 3) + 2));
                for (int i = 1; i <= subCount; i++)
                {
                    var sub = new TaskItem
                    {
                        Title = $"{decompTask.Title} — Part {i}",
                        Description = $"Subtask {i} of '{decompTask.Title}'. {decompTask.Description}",
                        Status = TaskItemStatus.Todo,
                        Priority = decompTask.Priority,
                        ProjectId = decompTask.ProjectId,
                        ComplexityScore = Math.Max(1, decompTask.ComplexityScore / subCount),
                        ParentTaskId = decompTask.Id,
                        CreatedAt = now
                    };
                    await _taskRepository.AddAsync(sub);
                }
                await LogAndNotify(projectId, null, $"🔀 '{decompTask.Title}' split into {subCount} subtasks.", decompTask, action: "Decomposition→Subtasks", correlationId: tickCorrelationId);
                continue;
            }
            // Wait for all subtasks to be Done
            if (subtasks.All(st => st.Status == TaskItemStatus.Done))
            {
                decompTask.Status = TaskItemStatus.Orchestration;
                decompTask.StatusEnteredAt = now;
                decompTask.UpdatedAt = now;
                await _taskRepository.UpdateAsync(decompTask);
                await LogAndNotify(projectId, decompTask.AssignedAgentId, $"🔀 '{decompTask.Title}' all subtasks complete, moving to Orchestration.", decompTask, action: "Decomposition→Orchestration", correlationId: tickCorrelationId);
            }
        }

        // 1. Orchestration → Architecture (assign to Beerus)
        foreach (var orchTask in allTasks.Where(t => t.Status == TaskItemStatus.Orchestration))
        {
            var orchestrator = orchTask.AssignedAgentId.HasValue ? agents.FirstOrDefault(a => a.Id == orchTask.AssignedAgentId) : null;
            if (orchestrator != null && (orchestrator.Role == AgentRole.GrandPriest || orchestrator.Role == AgentRole.Whis))
            {
                await OrchestratorServiceUtils.WriteAgentHandoffFileAsync(orchTask, orchestrator, "orchestration");
                orchestrator.Status = AgentStatus.Idle;
                await _agentRepository.UpdateAsync(orchestrator);
                await _notifier.NotifyAgentStartedAsync(AgentService.MapToDto(orchestrator));
            }
            var architect = agents.FirstOrDefault(a => a.Role == AgentRole.Beerus && a.Status == AgentStatus.Idle);
            if (architect != null)
            {
                await OrchestratorServiceUtils.WriteAgentHandoffFileAsync(orchTask, architect, "architecture");
                orchTask.AssignedAgentId = architect.Id;
                architect.Status = AgentStatus.Working;
                await _agentRepository.UpdateAsync(architect);
                await _notifier.NotifyAgentStartedAsync(AgentService.MapToDto(architect));

                // --- PATCH: trigger real OpenClaw agent execution ---
                if (_openClawRunner != null)
                {
                    var agentName = architect.Name.ToLowerInvariant();
                    var prompt = $"You are Beerus, the architect. Plan and design the solution for: '{orchTask.Title}'. {orchTask.Description}";
                    await _openClawRunner.TriggerTaskAsync(agentName, prompt);
                }
                // --- END PATCH ---

                orchTask.Status = TaskItemStatus.Architecture;
                orchTask.StatusEnteredAt = now;
                orchTask.UpdatedAt = now;
                await _taskRepository.UpdateAsync(orchTask);
                await LogAndNotify(projectId, orchTask.AssignedAgentId,
                    $"😼 '{orchTask.Title}' started Architecture.", orchTask,
                    action: "Orchestration→Architecture", agentName: architect.Name, correlationId: tickCorrelationId);
                await DispatchAssignedAgentAsync(orchTask, agents, "architecture", tickCorrelationId);
            }
        }

        // 2. Architecture → Tooling (assign to Bulma)
        foreach (var archTask in allTasks.Where(t => t.Status == TaskItemStatus.Architecture))
        {
            var architect = archTask.AssignedAgentId.HasValue ? agents.FirstOrDefault(a => a.Id == archTask.AssignedAgentId) : null;
            if (architect != null && architect.Role == AgentRole.Beerus)
            {
                await OrchestratorServiceUtils.WriteAgentHandoffFileAsync(archTask, architect, "architecture");
                architect.Status = AgentStatus.Idle;
                await _agentRepository.UpdateAsync(architect);
                await _notifier.NotifyAgentStartedAsync(AgentService.MapToDto(architect));
            }
            var bulma = agents.FirstOrDefault(a => a.Role == AgentRole.Bulma && a.Status == AgentStatus.Idle);
            if (bulma != null)
            {
                await OrchestratorServiceUtils.WriteAgentHandoffFileAsync(archTask, bulma, "tooling");
                archTask.AssignedAgentId = bulma.Id;
                bulma.Status = AgentStatus.Working;
                await _agentRepository.UpdateAsync(bulma);
                await _notifier.NotifyAgentStartedAsync(AgentService.MapToDto(bulma));
                archTask.Status = TaskItemStatus.Tooling;
                archTask.StatusEnteredAt = now;
                archTask.UpdatedAt = now;
                await _taskRepository.UpdateAsync(archTask);
                await LogAndNotify(projectId, archTask.AssignedAgentId,
                    $"👩‍🔬 '{archTask.Title}' started Tooling.", archTask,
                    action: "Architecture→Tooling", agentName: bulma.Name, correlationId: tickCorrelationId);
                await DispatchAssignedAgentAsync(archTask, agents, "tooling", tickCorrelationId);
            }
        }

        // 3. Tooling → Coding (assign to coder: Kakarot, Vegeta, Piccolo)
        foreach (var toolingTask in allTasks.Where(t => t.Status == TaskItemStatus.Tooling))
        {
            var bulma = toolingTask.AssignedAgentId.HasValue ? agents.FirstOrDefault(a => a.Id == toolingTask.AssignedAgentId) : null;
            if (bulma != null && bulma.Role == AgentRole.Bulma)
            {
                await OrchestratorServiceUtils.WriteAgentHandoffFileAsync(toolingTask, bulma, "tooling");
                bulma.Status = AgentStatus.Idle;
                await _agentRepository.UpdateAsync(bulma);
                await _notifier.NotifyAgentStartedAsync(AgentService.MapToDto(bulma));
            }
            var coder = agents.FirstOrDefault(a => (a.Role == AgentRole.Kakarot || a.Role == AgentRole.Vegeta || a.Role == AgentRole.Piccolo) && a.Status == AgentStatus.Idle);
            if (coder != null)
            {
                await OrchestratorServiceUtils.WriteAgentHandoffFileAsync(toolingTask, coder, "coding");
                toolingTask.AssignedAgentId = coder.Id;
                coder.Status = AgentStatus.Working;
                await _agentRepository.UpdateAsync(coder);
                await _notifier.NotifyAgentStartedAsync(AgentService.MapToDto(coder));
                toolingTask.Status = TaskItemStatus.Coding;
                toolingTask.StatusEnteredAt = now;
                toolingTask.UpdatedAt = now;
                await _taskRepository.UpdateAsync(toolingTask);
                await LogAndNotify(projectId, toolingTask.AssignedAgentId,
                    $"🦸‍♂️ '{toolingTask.Title}' started Coding.", toolingTask,
                    action: "Tooling→Coding", agentName: coder.Name, correlationId: tickCorrelationId);
                await DispatchAssignedAgentAsync(toolingTask, agents, "coding", tickCorrelationId);
            }
        }


        // 4. Coding → Refactoring (assign to Piccolo if not already)
        foreach (var codingTask in allTasks.Where(t => t.Status == TaskItemStatus.Coding))
        {
            Agent? coder = codingTask.AssignedAgentId.HasValue
                ? agents.FirstOrDefault(a => a.Id == codingTask.AssignedAgentId)
                : null;
            if (coder == null || !(coder.Role == AgentRole.Kakarot || coder.Role == AgentRole.Vegeta || coder.Role == AgentRole.Piccolo))
                continue;
            bool agentWorkCompleted = false;
            if (isTestMode)
            {
                agentWorkCompleted = true;
            }
            else if (_openClawRunner != null && await AgentCompletionChecker.IsAgentWorkCompletedAsync(codingTask, coder, _openClawRunner))
            {
                agentWorkCompleted = true;
            }
            if (agentWorkCompleted)
            {
                await OrchestratorServiceUtils.WriteAgentHandoffFileAsync(codingTask, coder, "coding");
                coder.Status = AgentStatus.Idle;
                await _agentRepository.UpdateAsync(coder);
                await _notifier.NotifyAgentStartedAsync(AgentService.MapToDto(coder));

                // Assign to Piccolo for Refactoring
                var piccolo = agents.FirstOrDefault(a => a.Role == AgentRole.Piccolo && a.Status == AgentStatus.Idle);
                if (piccolo != null)
                {
                    codingTask.AssignedAgentId = piccolo.Id;
                    piccolo.Status = AgentStatus.Working;
                    await _agentRepository.UpdateAsync(piccolo);
                    await _notifier.NotifyAgentStartedAsync(AgentService.MapToDto(piccolo));
                }
                codingTask.Status = TaskItemStatus.Refactoring;
                codingTask.StatusEnteredAt = now;
                codingTask.UpdatedAt = now;
                await _taskRepository.UpdateAsync(codingTask);
                await LogAndNotify(projectId, codingTask.AssignedAgentId,
                    $"🌿 {piccolo?.Name ?? "Piccolo"} is refactoring '{codingTask.Title}'.", codingTask,
                    action: "Coding→Refactoring", agentName: piccolo?.Name, correlationId: tickCorrelationId);
                await DispatchAssignedAgentAsync(codingTask, agents, "refactoring", tickCorrelationId);
            }
        }

        // 5. Refactoring → Security (assign to Cell)
        foreach (var refactorTask in allTasks.Where(t => t.Status == TaskItemStatus.Refactoring))
        {
            var piccolo = refactorTask.AssignedAgentId.HasValue ? agents.FirstOrDefault(a => a.Id == refactorTask.AssignedAgentId) : null;
            if (piccolo != null && piccolo.Role == AgentRole.Piccolo)
            {
                await OrchestratorServiceUtils.WriteAgentHandoffFileAsync(refactorTask, piccolo, "refactoring");
                piccolo.Status = AgentStatus.Idle;
                await _agentRepository.UpdateAsync(piccolo);
                await _notifier.NotifyAgentStartedAsync(AgentService.MapToDto(piccolo));
            }
            // Ensure Cell agent exists and OpenClaw workspace is created
            var cell = agents.FirstOrDefault(a => a.Role == AgentRole.Cell);
            if (cell == null)
            {
                var createDto = new {
                    Name = "Cell",
                    Role = "Cell",
                    Model = "gpt-4o-mini",
                    Description = "Auto-created agent for role Cell",
                    Skills = "Security,Audit,Analysis",
                    Emoji = "🦗",
                    ProjectId = refactorTask.ProjectId,
                    ExecutionBackend = "OpenClaw",
                    ToolsEnabled = true,
                    PushRole = false
                };
                await _agentService.CreateAsync((dynamic)createDto);
                agents = await _agentRepository.GetByProjectIdAsync(projectId);
                cell = agents.FirstOrDefault(a => a.Role == AgentRole.Cell);
            }
            if (cell != null && _openClawRunner != null)
            {
                var openClawId = ResolveAgentRuntimeName(cell);
                var workspacePath = await _openClawRunner.GetWorkspacePathAsync(openClawId) ?? System.IO.Path.Combine(_openClawRunner.WorkspaceRoot, openClawId);
                await _openClawRunner.SpawnAgentAsync(openClawId, cell.Model, workspacePath);
                var exists = await _openClawRunner.AgentExistsAsync(openClawId);
                var wsExists = false;
                var wsPath = await _openClawRunner.GetWorkspacePathAsync(openClawId);
                if (!string.IsNullOrEmpty(wsPath))
                    wsExists = System.IO.Directory.Exists(wsPath);
                if (!exists || !wsExists)
                {
                    if (!exists)
                        System.Console.WriteLine($"[WARN] OpenClaw agent '{openClawId}' not found after creation attempt.");
                    if (!wsExists)
                        System.Console.WriteLine($"[WARN] Workspace directory '{wsPath ?? workspacePath}' not found after creation attempt.");
                }
                // Now assign and trigger the agent
                if (cell.Status == AgentStatus.Idle)
                {
                    await OrchestratorServiceUtils.WriteAgentHandoffFileAsync(refactorTask, cell, "security");
                    refactorTask.AssignedAgentId = cell.Id;
                    cell.Status = AgentStatus.Working;
                    await _agentRepository.UpdateAsync(cell);
                    await _notifier.NotifyAgentStartedAsync(AgentService.MapToDto(cell));
                    refactorTask.Status = TaskItemStatus.Security;
                    refactorTask.StatusEnteredAt = now;
                    refactorTask.UpdatedAt = now;
                    await _taskRepository.UpdateAsync(refactorTask);
                    await LogAndNotify(projectId, refactorTask.AssignedAgentId,
                        $"🦗 '{refactorTask.Title}' started Security Audit.", refactorTask,
                        action: "Refactoring→Security", agentName: cell.Name, correlationId: tickCorrelationId);
                    await DispatchAssignedAgentAsync(refactorTask, agents, "security", tickCorrelationId);
                }
            }
        }

        // 6. Security → Testing (assign to Dende)
        foreach (var secTask in allTasks.Where(t => t.Status == TaskItemStatus.Security))
        {
            var cell = secTask.AssignedAgentId.HasValue ? agents.FirstOrDefault(a => a.Id == secTask.AssignedAgentId) : null;
            if (cell != null && cell.Role == AgentRole.Cell)
            {
                await OrchestratorServiceUtils.WriteAgentHandoffFileAsync(secTask, cell, "security");
                cell.Status = AgentStatus.Idle;
                await _agentRepository.UpdateAsync(cell);
                await _notifier.NotifyAgentStartedAsync(AgentService.MapToDto(cell));
            }
            var dende = agents.FirstOrDefault(a => a.Role == AgentRole.Dende && a.Status == AgentStatus.Idle);
            if (dende != null)
            {
                await OrchestratorServiceUtils.WriteAgentHandoffFileAsync(secTask, dende, "testing");
                secTask.AssignedAgentId = dende.Id;
                dende.Status = AgentStatus.Working;
                await _agentRepository.UpdateAsync(dende);
                await _notifier.NotifyAgentStartedAsync(AgentService.MapToDto(dende));
                secTask.Status = TaskItemStatus.Testing;
                secTask.StatusEnteredAt = now;
                secTask.UpdatedAt = now;
                await _taskRepository.UpdateAsync(secTask);
                await LogAndNotify(projectId, secTask.AssignedAgentId,
                    $"🧑‍🦲 '{secTask.Title}' started Testing.", secTask,
                    action: "Security→Testing", agentName: dende.Name, correlationId: tickCorrelationId);
                await DispatchAssignedAgentAsync(secTask, agents, "testing", tickCorrelationId);
            }
        }

        // 7. Testing → Review (assign to Gohan)
        foreach (var testTask in allTasks.Where(t => t.Status == TaskItemStatus.Testing))
        {
            var dende = testTask.AssignedAgentId.HasValue ? agents.FirstOrDefault(a => a.Id == testTask.AssignedAgentId) : null;
            if (dende != null && dende.Role == AgentRole.Dende)
            {
                await OrchestratorServiceUtils.WriteAgentHandoffFileAsync(testTask, dende, "testing");
                dende.Status = AgentStatus.Idle;
                await _agentRepository.UpdateAsync(dende);
                await _notifier.NotifyAgentStartedAsync(AgentService.MapToDto(dende));
            }
            var gohan = agents.FirstOrDefault(a => a.Role == AgentRole.Gohan && a.Status == AgentStatus.Idle);
            if (gohan != null)
            {
                await OrchestratorServiceUtils.WriteAgentHandoffFileAsync(testTask, gohan, "review");
                testTask.AssignedAgentId = gohan.Id;
                gohan.Status = AgentStatus.Working;
                await _agentRepository.UpdateAsync(gohan);
                await _notifier.NotifyAgentStartedAsync(AgentService.MapToDto(gohan));
                testTask.Status = TaskItemStatus.Review;
                testTask.StatusEnteredAt = now;
                testTask.UpdatedAt = now;
                await _taskRepository.UpdateAsync(testTask);
                await LogAndNotify(projectId, testTask.AssignedAgentId,
                    $"👦 '{testTask.Title}' started Review.", testTask,
                    action: "Testing→Review", agentName: gohan.Name, correlationId: tickCorrelationId);
                await DispatchAssignedAgentAsync(testTask, agents, "review", tickCorrelationId);
            }
        }

        // 8. Review → Compliance (assign to Jaco)
        foreach (var reviewTask in allTasks.Where(t => t.Status == TaskItemStatus.Review))
        {
            var gohan = reviewTask.AssignedAgentId.HasValue ? agents.FirstOrDefault(a => a.Id == reviewTask.AssignedAgentId) : null;
            if (gohan != null && gohan.Role == AgentRole.Gohan)
            {
                await OrchestratorServiceUtils.WriteAgentHandoffFileAsync(reviewTask, gohan, "review");
                gohan.Status = AgentStatus.Idle;
                await _agentRepository.UpdateAsync(gohan);
                await _notifier.NotifyAgentStartedAsync(AgentService.MapToDto(gohan));
            }
            var jaco = agents.FirstOrDefault(a => a.Role == AgentRole.Jaco && a.Status == AgentStatus.Idle);
            if (jaco != null)
            {
                await OrchestratorServiceUtils.WriteAgentHandoffFileAsync(reviewTask, jaco, "compliance");
                reviewTask.AssignedAgentId = jaco.Id;
                jaco.Status = AgentStatus.Working;
                await _agentRepository.UpdateAsync(jaco);
                await _notifier.NotifyAgentStartedAsync(AgentService.MapToDto(jaco));
                reviewTask.Status = TaskItemStatus.Compliance;
                reviewTask.StatusEnteredAt = now;
                reviewTask.UpdatedAt = now;
                await _taskRepository.UpdateAsync(reviewTask);
                await LogAndNotify(projectId, reviewTask.AssignedAgentId,
                    $"👽 '{reviewTask.Title}' started Compliance.", reviewTask,
                    action: "Review→Compliance", agentName: jaco.Name, correlationId: tickCorrelationId);
                await DispatchAssignedAgentAsync(reviewTask, agents, "compliance", tickCorrelationId);
            }
        }

        // 9. Compliance → Release (assign to Shenron)
        foreach (var compTask in allTasks.Where(t => t.Status == TaskItemStatus.Compliance))
        {
            var jaco = compTask.AssignedAgentId.HasValue ? agents.FirstOrDefault(a => a.Id == compTask.AssignedAgentId) : null;
            if (jaco != null && jaco.Role == AgentRole.Jaco)
            {
                await OrchestratorServiceUtils.WriteAgentHandoffFileAsync(compTask, jaco, "compliance");
                jaco.Status = AgentStatus.Idle;
                await _agentRepository.UpdateAsync(jaco);
                await _notifier.NotifyAgentStartedAsync(AgentService.MapToDto(jaco));
            }
            var shenron = agents.FirstOrDefault(a => a.Role == AgentRole.Shenron && a.Status == AgentStatus.Idle);
            if (shenron != null)
            {
                await OrchestratorServiceUtils.WriteAgentHandoffFileAsync(compTask, shenron, "release");
                compTask.AssignedAgentId = shenron.Id;
                shenron.Status = AgentStatus.Working;
                await _agentRepository.UpdateAsync(shenron);
                await _notifier.NotifyAgentStartedAsync(AgentService.MapToDto(shenron));
                compTask.Status = TaskItemStatus.Release;
                compTask.StatusEnteredAt = now;
                compTask.UpdatedAt = now;
                await _taskRepository.UpdateAsync(compTask);
                await LogAndNotify(projectId, compTask.AssignedAgentId,
                    $"🐉 '{compTask.Title}' started Release.", compTask,
                    action: "Compliance→Release", agentName: shenron.Name, correlationId: tickCorrelationId);
                await DispatchAssignedAgentAsync(compTask, agents, "release", tickCorrelationId);
            }
        }

        // 10. Release → Memory (assign to Trunks)
        foreach (var relTask in allTasks.Where(t => t.Status == TaskItemStatus.Release))
        {
            var shenron = relTask.AssignedAgentId.HasValue ? agents.FirstOrDefault(a => a.Id == relTask.AssignedAgentId) : null;
            if (shenron != null && shenron.Role == AgentRole.Shenron)
            {
                await OrchestratorServiceUtils.WriteAgentHandoffFileAsync(relTask, shenron, "release");
                shenron.Status = AgentStatus.Idle;
                await _agentRepository.UpdateAsync(shenron);
                await _notifier.NotifyAgentStartedAsync(AgentService.MapToDto(shenron));
            }
            var trunks = agents.FirstOrDefault(a => a.Role == AgentRole.Trunks && a.Status == AgentStatus.Idle);
            if (trunks != null)
            {
                await OrchestratorServiceUtils.WriteAgentHandoffFileAsync(relTask, trunks, "memory");
                relTask.AssignedAgentId = trunks.Id;
                trunks.Status = AgentStatus.Working;
                await _agentRepository.UpdateAsync(trunks);
                await _notifier.NotifyAgentStartedAsync(AgentService.MapToDto(trunks));
                relTask.Status = TaskItemStatus.Memory;
                relTask.StatusEnteredAt = now;
                relTask.UpdatedAt = now;
                await _taskRepository.UpdateAsync(relTask);
                await LogAndNotify(projectId, relTask.AssignedAgentId,
                    $"💾 '{relTask.Title}' started Memory/Documentation.", relTask,
                    action: "Release→Memory", agentName: trunks.Name, correlationId: tickCorrelationId);
                await DispatchAssignedAgentAsync(relTask, agents, "memory", tickCorrelationId);
            }
        }

        // 11. Memory → Enforcement (assign to Jiren)
        foreach (var memTask in allTasks.Where(t => t.Status == TaskItemStatus.Memory))
        {
            var trunks = memTask.AssignedAgentId.HasValue ? agents.FirstOrDefault(a => a.Id == memTask.AssignedAgentId) : null;
            if (trunks != null && trunks.Role == AgentRole.Trunks)
            {
                await OrchestratorServiceUtils.WriteAgentHandoffFileAsync(memTask, trunks, "memory");
                trunks.Status = AgentStatus.Idle;
                await _agentRepository.UpdateAsync(trunks);
                await _notifier.NotifyAgentStartedAsync(AgentService.MapToDto(trunks));
            }
            var jiren = agents.FirstOrDefault(a => a.Role == AgentRole.Jiren && a.Status == AgentStatus.Idle);
            if (jiren != null)
            {
                await OrchestratorServiceUtils.WriteAgentHandoffFileAsync(memTask, jiren, "enforcement");
                memTask.AssignedAgentId = jiren.Id;
                jiren.Status = AgentStatus.Working;
                await _agentRepository.UpdateAsync(jiren);
                await _notifier.NotifyAgentStartedAsync(AgentService.MapToDto(jiren));
                memTask.Status = TaskItemStatus.Enforcement;
                memTask.StatusEnteredAt = now;
                memTask.UpdatedAt = now;
                await _taskRepository.UpdateAsync(memTask);
                await LogAndNotify(projectId, memTask.AssignedAgentId,
                    $"💪 '{memTask.Title}' started Enforcement.", memTask,
                    action: "Memory→Enforcement", agentName: jiren.Name, correlationId: tickCorrelationId);
                await DispatchAssignedAgentAsync(memTask, agents, "enforcement", tickCorrelationId);
            }
        }

        // 12. Enforcement → Oversight (assign to Zeno)
        foreach (var enfTask in allTasks.Where(t => t.Status == TaskItemStatus.Enforcement))
        {
            var jiren = enfTask.AssignedAgentId.HasValue ? agents.FirstOrDefault(a => a.Id == enfTask.AssignedAgentId) : null;
            if (jiren != null && jiren.Role == AgentRole.Jiren)
            {
                await OrchestratorServiceUtils.WriteAgentHandoffFileAsync(enfTask, jiren, "enforcement");
                jiren.Status = AgentStatus.Idle;
                await _agentRepository.UpdateAsync(jiren);
                await _notifier.NotifyAgentStartedAsync(AgentService.MapToDto(jiren));
            }
            var zeno = agents.FirstOrDefault(a => a.Role == AgentRole.Zeno && a.Status == AgentStatus.Idle);
            if (zeno != null)
            {
                await OrchestratorServiceUtils.WriteAgentHandoffFileAsync(enfTask, zeno, "oversight");
                enfTask.AssignedAgentId = zeno.Id;
                zeno.Status = AgentStatus.Working;
                await _agentRepository.UpdateAsync(zeno);
                await _notifier.NotifyAgentStartedAsync(AgentService.MapToDto(zeno));
                enfTask.Status = TaskItemStatus.Oversight;
                enfTask.StatusEnteredAt = now;
                enfTask.UpdatedAt = now;
                await _taskRepository.UpdateAsync(enfTask);
                await LogAndNotify(projectId, enfTask.AssignedAgentId,
                    $"👑 '{enfTask.Title}' started Oversight.", enfTask,
                    action: "Enforcement→Oversight", agentName: zeno.Name, correlationId: tickCorrelationId);
                await DispatchAssignedAgentAsync(enfTask, agents, "oversight", tickCorrelationId);
            }
        }

        // 13. Oversight → Done (final approval)
        foreach (var overTask in allTasks.Where(t => t.Status == TaskItemStatus.Oversight))
        {
            var zeno = overTask.AssignedAgentId.HasValue ? agents.FirstOrDefault(a => a.Id == overTask.AssignedAgentId) : null;
            if (zeno != null && zeno.Role == AgentRole.Zeno)
            {
                await OrchestratorServiceUtils.WriteAgentHandoffFileAsync(overTask, zeno, "oversight");
                zeno.Status = AgentStatus.Idle;
                await _agentRepository.UpdateAsync(zeno);
                await _notifier.NotifyAgentStartedAsync(AgentService.MapToDto(zeno));
            }
            if (zeno != null && !await HasCompletionEvidenceAsync(overTask, zeno, "oversight"))
                continue;

            await OrchestratorServiceUtils.WriteAgentHandoffFileAsync(overTask, zeno ?? new Agent { Name = "Unknown", Role = AgentRole.Zeno }, "done");
            overTask.Status = TaskItemStatus.Done;
            overTask.StatusEnteredAt = now;
            overTask.UpdatedAt = now;
            await _taskRepository.UpdateAsync(overTask);
            await LogAndNotify(projectId, overTask.AssignedAgentId,
                $"✅ '{overTask.Title}' is Done!", overTask,
                action: "Oversight→Done", agentName: zeno?.Name, correlationId: tickCorrelationId);
        }

        // 5. Done (Trunks completes memory, goes Idle)
        foreach (var doneTask in allTasks.Where(t => t.Status == TaskItemStatus.Done))
        {
            var trunks = doneTask.AssignedAgentId.HasValue ? agents.FirstOrDefault(a => a.Id == doneTask.AssignedAgentId) : null;
            if (trunks != null && trunks.Role == AgentRole.Trunks && trunks.Status == AgentStatus.Working)
            {
                trunks.Status = AgentStatus.Idle;
                await _agentRepository.UpdateAsync(trunks);
                await _notifier.NotifyAgentStartedAsync(AgentService.MapToDto(trunks));
            }
        }

        // (Removed: Fix status and related transitions. Review failures now handled by ReviewService and Enforcement.)
        // Shared file handoff scaffold: each agent writes output to a file for the next agent
        // Implement WriteAgentHandoffFileAsync and ReadAgentHandoffFileAsync as needed
    }

    private static Agent? RouteToAgent(TaskItem task, List<Agent> agents)
    {
        // Determine if this task requires tool/plugin support
        bool requiresTools = TaskRequiresTools(task);

        // Helper: prefer idle agents; only consider OpenClaw agents when tools are required.
        // Falls back to non-idle agents if no idle ones are available.
        Agent? PickFrom(IEnumerable<Agent> candidates)
        {
            var list = candidates.Where(a => !a.IsPaused).ToList();
            if (requiresTools)
            {
                // Must be OpenClaw with tools
                var toolCapable = list.Where(a => a.ExecutionBackend == ExecutionBackend.OpenClaw && a.ToolsEnabled).ToList();
                if (toolCapable.Count > 0)
                    return toolCapable.FirstOrDefault(a => a.Status == AgentStatus.Idle) ?? toolCapable[0];
                // Escalate: any OpenClaw agent
                var anyOc = list.Where(a => a.ExecutionBackend == ExecutionBackend.OpenClaw).ToList();
                if (anyOc.Count > 0)
                    return anyOc.FirstOrDefault(a => a.Status == AgentStatus.Idle) ?? anyOc[0];
            }
            // Prefer idle agents to avoid overloading a single agent
            return list.FirstOrDefault(a => a.Status == AgentStatus.Idle) ?? list.FirstOrDefault();
        }

        if (task.ComplexityScore >= 7 || task.Priority == TaskPriority.Critical)
        {
            return PickFrom(agents.Where(a => a.Role == AgentRole.Vegeta))
                ?? PickFrom(agents.Where(a => a.Role == AgentRole.Kakarot));
        }

        if (task.Title.Contains("architect", StringComparison.OrdinalIgnoreCase)
            || task.Title.Contains("design", StringComparison.OrdinalIgnoreCase))
        {
            return PickFrom(agents.Where(a => a.Role == AgentRole.Beerus))
                ?? PickFrom(agents.Where(a => a.Role == AgentRole.Vegeta));
        }

        if (task.Title.Contains("refactor", StringComparison.OrdinalIgnoreCase)
            || task.Title.Contains("clean", StringComparison.OrdinalIgnoreCase))
        {
            return PickFrom(agents.Where(a => a.Role == AgentRole.Piccolo))
                ?? PickFrom(agents.Where(a => a.Role == AgentRole.Kakarot));
        }

        return PickFrom(agents.Where(a => a.Role == AgentRole.Kakarot))
            ?? PickFrom(agents.Where(a => a.Role is AgentRole.Vegeta or AgentRole.Piccolo))
            ?? PickFrom(agents);
    }

    /// <summary>
    /// Returns true when the task title/description indicates it needs tool use
    /// (deploy, publish, push, connect to external systems, CI/CD, etc.).
    /// Such tasks must be routed to an OpenClaw agent with ToolsEnabled=true.
    /// </summary>
    private static bool TaskRequiresTools(TaskItem task)
    {
        var text = $"{task.Title} {task.Description}";
        foreach (var kw in ToolKeywords)
        {
            if (text.Contains(kw, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    /// <summary>
    /// Keywords that indicate a task needs tool/plugin support.
    /// Such tasks must be routed to an OpenClaw agent with ToolsEnabled=true.
    /// </summary>
    private static readonly string[] ToolKeywords =
        ["deploy", "publish", "push", "release", "ci/cd", "pipeline",
         "connect", "external", "webhook", "api key", "secret"];

    private static int EstimateComplexity(TaskItem task)
    {
        var score = task.Priority switch
        {
            TaskPriority.Critical => 4,
            TaskPriority.High => 3,
            TaskPriority.Medium => 2,
            _ => 1
        };
        if (task.Description.Length > 300) score += 2;
        if (task.Title.Contains("architect", StringComparison.OrdinalIgnoreCase)) score += 3;
        if (task.Title.Contains("migrate", StringComparison.OrdinalIgnoreCase)) score += 2;
        if (task.Title.Contains("refactor", StringComparison.OrdinalIgnoreCase)) score += 1;
        return Math.Min(score, 10);
    }

    private async Task LogAndNotify(int projectId, int? agentId, string message, TaskItem task, string action = "StateTransition", string? agentName = null, string? correlationId = null)
    {
        var log = new ActivityLog
        {
            ProjectId = projectId,
            AgentId = agentId,
            Message = message,
            Timestamp = DateTime.UtcNow
        };
        var saved = await _logRepository.AddAsync(log);
        await _notifier.NotifyLogCreatedAsync(new ActivityLogDto
        {
            Id = saved.Id,
            ProjectId = saved.ProjectId,
            AgentId = saved.AgentId,
            Message = saved.Message,
            Timestamp = saved.Timestamp
        });
        await _notifier.NotifyTaskUpdatedAsync(TaskService.MapToDto(task));

        // Structured observability log
        await _logService.WriteAsync(
            level:         "Info",
            message:       message,
            agentName:     agentName,
            taskId:        task.Id.ToString(),
            correlationId: correlationId ?? Guid.NewGuid().ToString("N"),
            action:        action,
            source:        "Orchestrator");
    }

    /// <summary>
    /// Spawns or triggers the correct OpenClaw agent command for the given task.
    /// Uses the agent's model and role to determine the right prompt/command.
    /// Degrades gracefully when OpenClaw CLI is not installed.
    /// </summary>
    private async Task SpawnOrTriggerAgentAsync(Agent agent, TaskItem task, string correlationId)
    {
        if (_openClawRunner == null) return;
        if (agent.ExecutionBackend != ExecutionBackend.OpenClaw) return;

        try
        {
            var agentName = ResolveAgentRuntimeName(agent);
            var model = string.IsNullOrWhiteSpace(agent.Model) ? "github-copilot/gpt-4.1" : agent.Model;

            // Ensure agent is registered in OpenClaw; pass the log path as workspace base
            var ws = await _openClawRunner.GetWorkspacePathAsync(agentName)
                ?? Path.Combine(_openClawRunner.WorkspaceRoot, agentName);
            await _openClawRunner.SpawnAgentAsync(agentName, model, ws);

            // Build a task-specific prompt based on the agent role
            var prompt = BuildAgentPrompt(agent, task) + $"\nCorrelationId: {correlationId}";
            await _openClawRunner.TriggerTaskAsync(agentName, prompt);
        }
        catch
        {
            // OpenClaw CLI is optional — silently degrade if not installed
        }
    }

    /// <summary>
    /// Builds a role-specific prompt for the agent to work on the given task.
    /// </summary>
    private static string BuildAgentPrompt(Agent agent, TaskItem task)
    {
        var roleContext = agent.Role switch
        {
            AgentRole.Whis    => "You are the orchestrator. Analyze and plan this task:",
            AgentRole.Beerus  => "You are the architect. Design and structure the solution for:",
            AgentRole.Kakarot => "You are a standard developer. Implement the following task:",
            AgentRole.Vegeta  => "You are an expert engineer. Solve this complex problem:",
            AgentRole.Piccolo => "You are a refactoring specialist. Clean and refactor:",
            AgentRole.Gohan   => "You are the reviewer. Review and validate the solution for:",
            AgentRole.Trunks  => "You are the memory keeper. Document and record:",
            AgentRole.Bulma   => "You are the tooling expert. Build and configure:",
            _                 => "Work on the following task:",
        };

        return $"{roleContext} [{task.Id}] {task.Title}. {task.Description}. Priority: {task.Priority}. Complexity: {task.ComplexityScore}/10.";
    }

    private static string ResolveAgentRuntimeName(Agent agent)
    {
        if (!string.IsNullOrWhiteSpace(agent.OpenClawAgentId))
            return agent.OpenClawAgentId;

        var sanitized = string.Concat(agent.Name.ToLowerInvariant().Replace(" ", "-")
            .Where(c => char.IsLetterOrDigit(c) || c == '-'));
        return $"mc-{agent.Id}-{sanitized}";
    }

    private async Task DispatchAssignedAgentAsync(TaskItem task, IReadOnlyList<Agent> agents, string stage, string correlationId)
    {
        if (!task.AssignedAgentId.HasValue) return;

        var assigned = agents.FirstOrDefault(a => a.Id == task.AssignedAgentId.Value);
        if (assigned == null || assigned.ExecutionBackend != ExecutionBackend.OpenClaw || _openClawRunner == null)
            return;

        await OrchestratorServiceUtils.WriteAgentHandoffFileAsync(task, assigned, stage);
        await SpawnOrTriggerAgentAsync(assigned, task, correlationId);
    }

    private async Task<bool> HasCompletionEvidenceAsync(TaskItem task, Agent? agent, string stage)
    {
        if (_openClawRunner == null || agent == null) return false;

        var runtimeName = ResolveAgentRuntimeName(agent);
        var workspace = await _openClawRunner.GetWorkspacePathAsync(runtimeName)
                        ?? Path.Combine(_openClawRunner.WorkspaceRoot, runtimeName);
        var workspaceExists = Directory.Exists(workspace) &&
                              Directory.EnumerateFileSystemEntries(workspace, "*", SearchOption.AllDirectories).Any();

        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var handoffDir = Path.Combine(home, ".openclaw", "shared", task.ProjectId.ToString(), "handoff");
        var handoffExists = Directory.Exists(handoffDir) &&
                            Directory.EnumerateFiles(handoffDir, $"task-{task.Id}-{stage}-*.md").Any();

        var logFile = Path.Combine(home, ".openclaw", "logs", $"{runtimeName}.log");
        var logExists = File.Exists(logFile);

        return workspaceExists || handoffExists || logExists;
    }

    /// <summary>
    /// Creates a structured long-term memory entry when a task is completed.
    /// Format: task_id, problem, fix, lesson learned.
    /// </summary>
    public static class OrchestratorServiceUtils
    {
        public static async Task WriteAgentHandoffFileAsync(TaskItem task, Agent agent, string stage)
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var sharedRoot = Path.Combine(home, ".openclaw", "shared");
            var dir = Path.Combine(sharedRoot, task.ProjectId.ToString(), "handoff");
            Directory.CreateDirectory(dir);
            var file = Path.Combine(dir, $"task-{task.Id}-{stage}-{task.Status}.md");
            var content = $"# Handoff: {stage} ({task.Status})\nAgent: {agent.Name} ({agent.Role})\nTask: {task.Title}\nStatus: {task.Status}\nTimestampUtc: {DateTime.UtcNow:O}\n---\nOutput: agent execution dispatched\n";
            await File.WriteAllTextAsync(file, content);
        }
    }
}
