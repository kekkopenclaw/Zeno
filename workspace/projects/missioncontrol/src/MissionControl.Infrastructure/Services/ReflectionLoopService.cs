using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MissionControl.Domain.Interfaces;

namespace MissionControl.Infrastructure.Services;

/// <summary>
/// ReflectionLoopService — post-task autonomous reflection pattern.
///
/// After each task completion:
///   1. Passes the task outcome (problem + fix + lesson) to Ollama.
///   2. Asks: "Based on this outcome, what new general rule should be applied to future tasks?"
///   3. If Ollama returns a non-trivial rule, appends it to LESSONS.md.
///   4. Returns the rule text so callers can inject it into future agent prompts.
///
/// Config (appsettings.json):
///   Reflection:LessonsFile  — path to LESSONS.md (default: ./LESSONS.md in working dir)
///   Reflection:Model        — Ollama model for reflection (default: Kairos:Model or "llama3")
///   Reflection:Enabled      — set false to disable (default: true)
/// </summary>
public sealed class ReflectionLoopService
{
    private readonly IOllamaClient _ollama;
    private readonly ILogger<ReflectionLoopService> _logger;
    private readonly string _lessonsFile;
    private readonly string _model;
    private readonly bool _enabled;

    public ReflectionLoopService(
        IOllamaClient ollama,
        IConfiguration config,
        ILogger<ReflectionLoopService> logger)
    {
        _ollama      = ollama;
        _logger      = logger;
        _enabled     = config.GetValue<bool>("Reflection:Enabled", true);
        _model       = config["Reflection:Model"] ?? config["Kairos:Model"] ?? "llama3";
        _lessonsFile = config["Reflection:LessonsFile"]
                       ?? Path.Combine(Directory.GetCurrentDirectory(), "LESSONS.md");
    }

    /// <summary>
    /// Reflects on a completed task outcome.  If a new rule is derived, appends it to LESSONS.md
    /// and returns the rule text; otherwise returns an empty string.
    /// </summary>
    /// <param name="taskId">Numeric task ID for traceability.</param>
    /// <param name="problem">What went wrong / what was attempted.</param>
    /// <param name="fix">What resolved the problem.</param>
    /// <param name="lesson">The pre-distilled lesson from KairosMemoryService.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The new rule appended to LESSONS.md, or empty string if none was produced.</returns>
    public async Task<string> ReflectAsync(
        int taskId,
        string problem,
        string fix,
        string lesson,
        CancellationToken ct = default)
    {
        if (!_enabled)
        {
            _logger.LogDebug("ReflectionLoopService: disabled via config.");
            return string.Empty;
        }

        var prompt = $"""
You are Trunks, a meta-learning agent responsible for extracting durable engineering rules.

A task just completed with the following outcome:
TASK_ID: {taskId}
PROBLEM: {problem}
FIX: {fix}
LESSON: {lesson}

Based solely on this outcome, state ONE new general rule that should be applied to ALL future tasks.
The rule must be:
- Actionable (starts with a verb: "Always", "Never", "Prefer", "When X, do Y")
- General (not specific to this task's domain)
- Not a duplicate of common sense

If no new rule can be derived, respond exactly with: NO_RULE

Respond with only the rule text or NO_RULE.
""";

        string raw;
        try
        {
            raw = await _ollama.GenerateAsync(_model, prompt, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "ReflectionLoopService: Ollama call failed for task {TaskId}.", taskId);
            return string.Empty;
        }

        var rule = raw.Trim();
        if (string.IsNullOrWhiteSpace(rule) || rule.Equals("NO_RULE", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogDebug("ReflectionLoopService: no new rule for task {TaskId}.", taskId);
            return string.Empty;
        }

        await AppendToLessonsAsync(taskId, rule, ct);
        _logger.LogInformation("ReflectionLoopService: new rule appended for task {TaskId}: {Rule}", taskId, rule);
        return rule;
    }

    /// <summary>
    /// Returns all rules currently stored in LESSONS.md, for injection into agent prompts.
    /// Returns an empty list if the file does not exist or is empty.
    /// </summary>
    public async Task<IReadOnlyList<string>> LoadLessonsAsync(CancellationToken ct = default)
    {
        if (!File.Exists(_lessonsFile)) return [];

        try
        {
            var text = await File.ReadAllTextAsync(_lessonsFile, ct);
            return text
                .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(l => l.StartsWith("- "))
                .Select(l => l[2..].Trim())
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "ReflectionLoopService: failed to read LESSONS.md at {Path}.", _lessonsFile);
            return [];
        }
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    private async Task AppendToLessonsAsync(int taskId, string rule, CancellationToken ct)
    {
        try
        {
            var dir = Path.GetDirectoryName(_lessonsFile);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            // Write header on first creation
            if (!File.Exists(_lessonsFile))
            {
                await File.WriteAllTextAsync(_lessonsFile,
                    "# LESSONS\n\nAutonomously derived rules from agent reflection loops.\n\n", ct);
            }

            var entry = $"- [{DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC | task {taskId}] {rule}\n";
            await File.AppendAllTextAsync(_lessonsFile, entry, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "ReflectionLoopService: failed to write LESSONS.md at {Path}.", _lessonsFile);
        }
    }
}
