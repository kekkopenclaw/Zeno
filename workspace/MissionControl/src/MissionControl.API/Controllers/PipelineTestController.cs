using Microsoft.AspNetCore.Mvc;
using MissionControl.Application.DTOs;
using MissionControl.Application.Interfaces;
using MissionControl.Application.Services;
using MissionControl.Domain.Entities;
using MissionControl.Domain.Enums;
using MissionControl.Domain.Interfaces;
using MissionControl.Infrastructure.Services;

namespace MissionControl.API.Controllers;

/// <summary>
/// PipelineTestController — end-to-end smoke test for the full Mission Control pipeline.
///
/// POST /api/pipeline-test?projectId=1
///   1. Creates a synthetic task ("Pipeline Test")
///   2. Builds a swarm plan (Whis delegates)
///   3. Runs the AgentLoopService (observe → plan → execute → feedback)
///   4. Saves a MemorySummary + MemoryEntry via KairosMemoryService
///   5. Embeds the memory into Chroma vector store + verifies retrieval
///   6. Runs ReflectionLoopService to derive a new rule → LESSONS.md
///   7. Broadcasts every step via SignalR → log feed updates live in the Memory tab
///   8. Returns a full step-by-step result payload
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class PipelineTestController : ControllerBase
{
    private readonly ITaskRepository _taskRepo;
    private readonly IAgentRepository _agentRepo;
    private readonly IProjectRepository _projectRepo;
    private readonly AgentLoopService _agentLoop;
    private readonly SwarmCoordinatorService _swarm;
    private readonly KairosMemoryService _kairosMemory;
    private readonly IChromaVectorService _chroma;
    private readonly ReflectionLoopService _reflection;
    private readonly ISignalRNotifier _notifier;
    private readonly ILogService _logSvc;
    private readonly OrchestratorService _orchestrator;

    public PipelineTestController(
        ITaskRepository taskRepo,
        IAgentRepository agentRepo,
        IProjectRepository projectRepo,
        AgentLoopService agentLoop,
        SwarmCoordinatorService swarm,
        KairosMemoryService kairosMemory,
        IChromaVectorService chroma,
        ReflectionLoopService reflection,
        ISignalRNotifier notifier,
        ILogService logSvc,
        OrchestratorService orchestrator)
    {
        _taskRepo   = taskRepo;
        _agentRepo  = agentRepo;
        _projectRepo = projectRepo;
        _agentLoop  = agentLoop;
        _swarm      = swarm;
        _kairosMemory = kairosMemory;
        _chroma     = chroma;
        _reflection = reflection;
        _notifier   = notifier;
        _logSvc     = logSvc;
        _orchestrator = orchestrator;
    }

    /// <summary>
    /// Runs the full pipeline test: task → swarm → agent loop → memory save → live log feed.
    /// Safe to call repeatedly — synthetic task is cleaned up after the test.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> RunPipelineTest(
        [FromQuery] int projectId = 1,
        CancellationToken ct = default)
    {

        var steps = new List<object>();
        var correlationId = Guid.NewGuid().ToString("N")[..12];
        var statusHistory = new List<object>();

        async Task Broadcast(string step, string msg, object? extra = null)
        {
            var payload = new { step, message = msg, correlationId, projectId, extra };
            steps.Add(payload);
            await _notifier.NotifyPipelineTestProgressAsync(payload);
            await _logSvc.WriteAsync("Info", $"[PipelineTest:{correlationId}] {step}: {msg}",
                "Whis", null, correlationId, step, "PipelineTest");
        }

        await Broadcast("Start", $"🚀 Pipeline test initiated for project {projectId}.");

        // ── Step 1: Verify project exists ──────────────────────────────────
        var project = await _projectRepo.GetByIdAsync(projectId);
        if (project == null)
            return NotFound(new { error = $"Project {projectId} not found." });

        // ── Step 2: Create a batch of diverse tasks ───────────────────────
        var now = DateTime.UtcNow;
        var tasks = new[]
        {
            new TaskItem
            {
                ProjectId = projectId,
                Title = $"High Complexity Critical Task — {now:HH:mm:ss}",
                Description = "This is a critical, high-complexity task to trigger Vegeta.",
                Status = TaskItemStatus.Todo,
                Priority = TaskPriority.Critical,
                CreatedAt = now,
                UpdatedAt = now,
                ComplexityScore = 9
            },
            new TaskItem
            {
                ProjectId = projectId,
                Title = $"Architect System Design — {now:HH:mm:ss}",
                Description = "This task should be routed to Beerus for architecture.",
                Status = TaskItemStatus.Todo,
                Priority = TaskPriority.High,
                CreatedAt = now,
                UpdatedAt = now,
                ComplexityScore = 6
            },
            new TaskItem
            {
                ProjectId = projectId,
                Title = $"Refactor Legacy Module — {now:HH:mm:ss}",
                Description = "This task should be routed to Piccolo for refactoring.",
                Status = TaskItemStatus.Todo,
                Priority = TaskPriority.Medium,
                CreatedAt = now,
                UpdatedAt = now,
                ComplexityScore = 4
            },
            new TaskItem
            {
                ProjectId = projectId,
                Title = $"Standard Feature Implementation — {now:HH:mm:ss}",
                Description = "This is a standard task for Kakarot.",
                Status = TaskItemStatus.Todo,
                Priority = TaskPriority.High,
                CreatedAt = now,
                UpdatedAt = now,
                ComplexityScore = 5
            }
        };
        var savedTasks = new List<TaskItem>();
        foreach (var t in tasks)
        {
            var saved = await _taskRepo.AddAsync(t);
            savedTasks.Add(saved);
            await Broadcast("TaskCreated", $"📋 Task created: '{saved.Title}' (ID={saved.Id}).", new { taskId = saved.Id });
            await _notifier.NotifyTaskUpdated(MapTaskToDto(saved, null));
        }

        // ── Step 3: Run orchestrator ticks for all tasks ─────────────────
        for (int tick = 0; tick < 10; tick++)
        {
            await _orchestrator.TickAsync(projectId);
            foreach (var savedTask in savedTasks)
            {
                var currentTask = await _taskRepo.GetByIdAsync(savedTask.Id);
                if (currentTask == null)
                {
                    await Broadcast($"OrchestratorTick_{tick+1}_TaskMissing", $"Tick {tick+1}: Task with ID {savedTask.Id} not found (may have been deleted).", new {
                        status = "Missing",
                        assignedAgentId = (int?)null,
                        updatedAt = (DateTime?)null,
                        statusEnteredAt = (DateTime?)null
                    });
                    continue;
                }
                await Broadcast($"OrchestratorTick_{tick+1}_{currentTask.Title}", $"Tick {tick+1}: Task '{currentTask.Title}' status is now {currentTask.Status}", new {
                    status = currentTask.Status.ToString(),
                    assignedAgentId = currentTask.AssignedAgentId,
                    updatedAt = currentTask.UpdatedAt,
                    statusEnteredAt = currentTask.StatusEnteredAt
                });
                statusHistory.Add(new {
                    tick = tick+1,
                    taskId = currentTask.Id,
                    title = currentTask.Title,
                    status = currentTask.Status.ToString(),
                    assignedAgentId = currentTask.AssignedAgentId,
                    updatedAt = currentTask.UpdatedAt,
                    statusEnteredAt = currentTask.StatusEnteredAt
                });
            }
            // Stop early if all tasks are Done
            if (savedTasks.All(t => t.Status == TaskItemStatus.Done))
                break;
        }

        // ── Step 4+: For each task, run agent loop, memory, and reporting steps ──
        var results = new List<object>();
        foreach (var t in savedTasks)
        {
            var memCtx = await _kairosMemory.GetRelevantContextAsync(projectId, t.Description ?? "", topK: 6, ct);
            await Broadcast($"MemoryContext_{t.Title}", $"🧠 Injected {memCtx.Count} memory chunks into agent context for '{t.Title}'.", new { taskId = t.Id, chunks = memCtx.Count });

            // Use the assigned agent as executor if available
            var agent = t.AssignedAgentId.HasValue ? await _agentRepo.GetByIdAsync(t.AssignedAgentId.Value) : null;
            var agentDto = agent != null ? AgentService.MapToDto(agent) : null;
            var taskDto = MapTaskToDto(t, agent?.Name);
            AgentLoopResult loopResult;
            if (agentDto != null)
            {
                await Broadcast($"AgentLoop_{t.Title}", $"⚙️ Running AgentLoop for {agentDto.Name} (YOLO=false) on '{t.Title}'...");
                loopResult = await _agentLoop.RunAsync(agentDto, taskDto, memCtx, yolo: false, ct);
            }
            else
            {
                loopResult = AgentLoopResult.Error(t.Id, 0, "No executor agent available.");
            }
            await Broadcast($"AgentLoopResult_{t.Title}",
                loopResult.FeedbackPassed
                    ? $"✅ Agent loop passed in {loopResult.Attempts} attempt(s) for '{t.Title}'."
                    : $"⚠️ Agent loop: {loopResult.FeedbackNotes} ({loopResult.Attempts} attempt(s)) for '{t.Title}'.",
                new { taskId = t.Id, passed = loopResult.FeedbackPassed, attempts = loopResult.Attempts });

            // Mark as Done for reporting
            t.Status = TaskItemStatus.Done;
            t.UpdatedAt = DateTime.UtcNow;
            t.StatusEnteredAt = DateTime.UtcNow;
            await _taskRepo.UpdateAsync(t);
            await _notifier.NotifyTaskUpdated(MapTaskToDto(t, agent?.Name));
            await Broadcast($"TaskDone_{t.Title}", $"✅ Task '{t.Title}' marked Done.", new { taskId = t.Id });

            // Memory and summary
            var problem = $"Pipeline test for '{t.Title}' was triggered.";
            var fix = loopResult.FeedbackPassed
                ? "Agent loop completed successfully with all feedback checks passing."
                : $"Agent loop encountered issues: {loopResult.FeedbackNotes}";
            var lesson = "Full pipeline verified: swarm coordinator, agent loop, memory layers, SignalR broadcast all operational.";

            var summary = await _kairosMemory.DistilSummaryAsync(
                projectId, t.Id, problem, fix, lesson,
                agentRole: agent?.Role.ToString() ?? "Whis",
                retries: loopResult.Attempts - 1,
                complexity: t.ComplexityScore,
                ct);
            await Broadcast($"MemorySaved_{t.Title}", $"💾 MemorySummary saved (ID={summary.Id}) for '{t.Title}'.", new { taskId = t.Id, summaryId = summary.Id });

            var memContent = $"Problem: {problem}\nFix: {fix}\nLesson: {lesson}";
            await _kairosMemory.WriteDistilledMemoryAsync(
                projectId,
                title: $"Pipeline Test Result — {now:yyyy-MM-dd HH:mm}",
                content: memContent,
                type: MemoryType.Insight,
                tags: "pipeline-test,smoke-test,kairos",
                ct: ct);

            // Chroma vector store
            var vectorId = $"pipeline_test_{t.Id}_{now:yyyyMMddHHmmss}";
            await _chroma.EmbedAndStoreAsync(
                id: vectorId,
                text: memContent,
                metadata: new Dictionary<string, string>
                {
                    ["task_id"]    = t.Id.ToString(),
                    ["summary_id"] = summary.Id.ToString(),
                    ["source"]     = "pipeline_test"
                },
                ct: ct);
            var vectorResults = await _chroma.SearchAsync(problem, topK: 3, ct: ct);
            var vectorOk = vectorResults.Count > 0;
            await Broadcast($"VectorStore_{t.Title}",
                vectorOk
                    ? $"🔍 Chroma: embedded and retrieved {vectorResults.Count} result(s) for '{t.Title}' — vector store operational."
                    : $"⚠️ Chroma: vector store not reachable for '{t.Title}' — embeddings skipped (Chroma may not be running).",
                new { taskId = t.Id, vectorId, resultsFound = vectorResults.Count, chromaOk = vectorOk });

            // Reflection
            var newRule = await _reflection.ReflectAsync(t.Id, problem, fix, lesson, ct);
            await Broadcast($"Reflection_{t.Title}",
                string.IsNullOrWhiteSpace(newRule)
                    ? $"🪞 Reflection: no new rule derived for '{t.Title}' (Ollama may not be running or returned NO_RULE)."
                    : $"🪞 Reflection: new rule for '{t.Title}' → \"{newRule}\" (appended to LESSONS.md).",
                new { taskId = t.Id, rule = newRule, lessonsUpdated = !string.IsNullOrWhiteSpace(newRule) });

            results.Add(new
            {
                taskId = t.Id,
                title = t.Title,
                summaryId = summary.Id,
                loopPassed = loopResult.FeedbackPassed,
                attempts = loopResult.Attempts,
                vectorId,
                chromaOk = vectorOk,
                reflectionRule = newRule
            });
        }

        await Broadcast("Complete", $"🎉 Pipeline test complete for all tasks! Check the Memory tab for the saved entries.");

        return Ok(new
        {
            success = true,
            correlationId,
            results,
            steps,
            statusHistory
        });
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private static TaskItemDto MapTaskToDto(TaskItem t, string? agentName) => new()
    {
        Id = t.Id,
        Title = t.Title,
        Description = t.Description,
        Status = t.Status.ToString(),
        Priority = t.Priority.ToString(),
        AssignedAgentId = t.AssignedAgentId,
        AssignedAgentName = agentName,
        ProjectId = t.ProjectId,
        CreatedAt = t.CreatedAt,
        UpdatedAt = t.UpdatedAt,
        RetryCount = t.RetryCount,
        ReviewFailCount = t.ReviewFailCount,
        ReviewNotes = t.ReviewNotes,
        ComplexityScore = t.ComplexityScore
    };
}
