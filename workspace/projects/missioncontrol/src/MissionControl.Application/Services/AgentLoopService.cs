using Microsoft.Extensions.Logging;
using MissionControl.Application.DTOs;
using MissionControl.Application.Interfaces;
using MissionControl.Domain.Interfaces;

namespace MissionControl.Application.Services;

/// <summary>
/// AgentLoopService — ported from Claw-code agent loop patterns, rewritten in C#.
///
/// Implements the full autonomous agent cycle:
///   Observe → Plan → Execute → Feedback → Retry (with backoff)
///
/// Key features from Claw-code:
/// • YOLO sandbox: non-fatal errors are logged and execution continues
/// • Token cap: hard limit on tokens consumed per loop run
/// • Retry with exponential backoff (max 3 attempts by default)
/// • Context injection: top-6 memory chunks prepended to every prompt
/// • Correlation ID tracing through all log entries
/// </summary>
public sealed class AgentLoopService
{
    private readonly IOllamaClient _ollama;
    private readonly IOpenClawRunner _openClaw;
    private readonly ISignalRNotifier _notifier;
    private readonly ILogService _logService;
    private readonly ILogger<AgentLoopService> _logger;

    // Hard ceiling: abort the loop if we'd exceed this many tokens (approx. chars/4).
    private const int TokenCapChars = 32_000;

    // Maximum automatic retries per task execution.
    private const int MaxRetries = 3;

    public AgentLoopService(
        IOllamaClient ollama,
        IOpenClawRunner openClaw,
        ISignalRNotifier notifier,
        ILogService logService,
        ILogger<AgentLoopService> logger)
    {
        _ollama = ollama;
        _openClaw = openClaw;
        _notifier = notifier;
        _logService = logService;
        _logger = logger;
    }

    /// <summary>
    /// Runs the full agent loop for one task execution cycle.
    /// Returns the raw agent response (or the last successful partial if YOLO triggered).
    /// </summary>
    /// <param name="agentDto">The agent executing the task.</param>
    /// <param name="task">The task to execute.</param>
    /// <param name="memoryContext">Top-6 memory chunks injected as context (from KairosMemoryService).</param>
    /// <param name="yolo">When true, continues past non-fatal errors rather than aborting.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task<AgentLoopResult> RunAsync(
        AgentDto agentDto,
        TaskItemDto task,
        IReadOnlyList<string> memoryContext,
        bool yolo = false,
        CancellationToken ct = default)
    {
        var correlationId = Guid.NewGuid().ToString("N")[..12];
        var attempt = 0;
        var totalChars = 0;
        AgentLoopResult? lastResult = null;

        await LogInfo($"[Loop:{correlationId}] Starting for agent={agentDto.Name} task={task.Id} yolo={yolo}",
            agentDto.Name, task.Id, correlationId);

        while (attempt < MaxRetries)
        {
            attempt++;
            try
            {
                // ── 1. OBSERVE ──────────────────────────────────────────────
                // Build context: memory chunks + task description
                var observation = Observe(task, memoryContext);
                totalChars += observation.Length;

                if (totalChars > TokenCapChars)
                {
                    await LogWarn($"[Loop:{correlationId}] Token cap exceeded ({totalChars} chars) — aborting.", agentDto.Name, task.Id, correlationId);
                    return lastResult ?? AgentLoopResult.TokenCapExceeded(task.Id, attempt);
                }

                // ── 2. PLAN ──────────────────────────────────────────────────
                var plan = Plan(agentDto, task, observation);
                await LogInfo($"[Loop:{correlationId}] Attempt {attempt}/{MaxRetries} — plan composed ({plan.Length} chars).",
                    agentDto.Name, task.Id, correlationId);

                totalChars += plan.Length;

                // ── 3. EXECUTE ───────────────────────────────────────────────
                string rawResponse;
                if (agentDto.ExecutionBackend == "OpenClaw" && agentDto.ToolsEnabled)
                {
                    // Route to OpenClaw CLI for agents that need tool access
                    await _openClaw.TriggerTaskAsync(agentDto.Id.ToString(), plan, ct);
                    rawResponse = $"[OpenClaw] Task {task.Id} dispatched to agent mc-{agentDto.Id}.";
                }
                else
                {
                    // Default: Ollama local inference
                    rawResponse = await _ollama.GenerateAsync(agentDto.Model, plan, ct);
                }

                await LogInfo($"[Loop:{correlationId}] Execute complete — response {rawResponse.Length} chars.",
                    agentDto.Name, task.Id, correlationId);

                totalChars += rawResponse.Length;

                // ── 4. FEEDBACK ──────────────────────────────────────────────
                var feedback = Feedback(rawResponse, task);
                lastResult = new AgentLoopResult
                {
                    TaskId = task.Id,
                    AgentName = agentDto.Name,
                    RawResponse = rawResponse,
                    FeedbackPassed = feedback.Passed,
                    FeedbackNotes = feedback.Notes,
                    Attempts = attempt,
                    CorrelationId = correlationId
                };

                if (feedback.Passed)
                {
                    await LogInfo($"[Loop:{correlationId}] ✅ Feedback passed on attempt {attempt}.",
                        agentDto.Name, task.Id, correlationId);
                    return lastResult;
                }

                await LogWarn($"[Loop:{correlationId}] ⚠️ Feedback failed: {feedback.Notes} — retry {attempt}/{MaxRetries}.",
                    agentDto.Name, task.Id, correlationId);

                // Exponential backoff before retry (100ms, 200ms, 400ms…)
                await Task.Delay(TimeSpan.FromMilliseconds(100 * Math.Pow(2, attempt - 1)), ct);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                await LogError($"[Loop:{correlationId}] Exception on attempt {attempt}: {ex.Message}",
                    agentDto.Name, task.Id, correlationId);

                if (!yolo || attempt >= MaxRetries)
                    return lastResult ?? AgentLoopResult.Error(task.Id, attempt, ex.Message);

                // YOLO: log and continue to next attempt
                await LogWarn($"[Loop:{correlationId}] YOLO mode — swallowing error and retrying.",
                    agentDto.Name, task.Id, correlationId);
            }
        }

        return lastResult ?? AgentLoopResult.MaxRetriesExceeded(task.Id, attempt);
    }

    // ── Private helpers ────────────────────────────────────────────────────

    /// <summary>
    /// Observe phase: assemble task context + injected memory into a concise observation string.
    /// </summary>
    private static string Observe(TaskItemDto task, IReadOnlyList<string> memoryContext)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"TASK: [{task.Priority}] {task.Title}");
        sb.AppendLine($"STATUS: {task.Status}");
        if (!string.IsNullOrWhiteSpace(task.Description))
            sb.AppendLine($"DESCRIPTION: {task.Description}");
        if (task.ReviewNotes is { Length: > 0 })
            sb.AppendLine($"REVIEW NOTES: {task.ReviewNotes}");
        if (memoryContext.Count > 0)
        {
            sb.AppendLine("\n--- RELEVANT MEMORY CONTEXT (top-6) ---");
            for (int i = 0; i < memoryContext.Count; i++)
                sb.AppendLine($"[{i + 1}] {memoryContext[i]}");
        }
        return sb.ToString();
    }

    /// <summary>
    /// Plan phase: compose the full prompt for the agent from the observation.
    /// </summary>
    private static string Plan(AgentDto agent, TaskItemDto task, string observation)
    {
        return $"""
You are {agent.Name}, role: {agent.Role}.
Skills: {agent.Skills}

{observation}

Your job:
- Analyse the task fully.
- Produce a complete, production-ready solution.
- Output ONLY the solution — no preamble, no markdown code fences unless the content is code.
- If the task is impossible or ambiguous, explain precisely why.
""";
    }

    /// <summary>
    /// Feedback phase: simple heuristic quality check on the raw response.
    /// Extend this to call a reviewer agent for more sophisticated validation.
    /// </summary>
    private static (bool Passed, string Notes) Feedback(string response, TaskItemDto task)
    {
        if (string.IsNullOrWhiteSpace(response))
            return (false, "Empty response — agent produced no output.");

        // Reject boilerplate non-answers
        var lowered = response.ToLowerInvariant();
        if (lowered.Contains("i cannot") || lowered.Contains("i am unable") ||
            lowered.Contains("as an ai") || lowered.Contains("i don't have access"))
            return (false, "Agent refused the task — response contains refusal language.");

        if (response.Length < 30)
            return (false, $"Response too short ({response.Length} chars) — likely incomplete.");

        return (true, "Response meets quality threshold.");
    }

    private Task LogInfo(string msg, string agentName, int taskId, string correlationId) =>
        _logService.WriteAsync("Info", msg, agentName, taskId.ToString(), correlationId, "AgentLoop", source: "AgentLoopService");

    private Task LogWarn(string msg, string agentName, int taskId, string correlationId) =>
        _logService.WriteAsync("Warning", msg, agentName, taskId.ToString(), correlationId, "AgentLoop", source: "AgentLoopService");

    private Task LogError(string msg, string agentName, int taskId, string correlationId) =>
        _logService.WriteAsync("Error", msg, agentName, taskId.ToString(), correlationId, "AgentLoop", source: "AgentLoopService");
}

// ── Result types ──────────────────────────────────────────────────────────────

/// <summary>Encapsulates the outcome of a single AgentLoopService.RunAsync call.</summary>
public sealed class AgentLoopResult
{
    public int TaskId { get; init; }
    public string AgentName { get; init; } = string.Empty;
    public string RawResponse { get; init; } = string.Empty;
    public bool FeedbackPassed { get; init; }
    public string FeedbackNotes { get; init; } = string.Empty;
    public int Attempts { get; init; }
    public string CorrelationId { get; init; } = string.Empty;
    public string? ErrorMessage { get; init; }

    public static AgentLoopResult TokenCapExceeded(int taskId, int attempts) => new()
    {
        TaskId = taskId, Attempts = attempts, FeedbackPassed = false,
        ErrorMessage = "Token cap exceeded.", FeedbackNotes = "Token cap exceeded."
    };

    public static AgentLoopResult MaxRetriesExceeded(int taskId, int attempts) => new()
    {
        TaskId = taskId, Attempts = attempts, FeedbackPassed = false,
        ErrorMessage = "Max retries exceeded.", FeedbackNotes = "Max retries exceeded."
    };

    public static AgentLoopResult Error(int taskId, int attempts, string error) => new()
    {
        TaskId = taskId, Attempts = attempts, FeedbackPassed = false,
        ErrorMessage = error, FeedbackNotes = error
    };
}
