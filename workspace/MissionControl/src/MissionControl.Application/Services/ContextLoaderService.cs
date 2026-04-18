using MissionControl.Application.DTOs;
using MissionControl.Domain.Enums;
using MissionControl.Domain.Interfaces;

namespace MissionControl.Application.Services;

/// <summary>
/// ContextLoader — loads and CACHES agent identity/rules context.
/// NEVER injects full .md files into every prompt.
/// Conditionally loads standards only when the task type requires it.
/// </summary>
public class ContextLoaderService
{
    // Cached static context — loaded once, reused indefinitely
    private static readonly string _soulContext = """
        You are an autonomous AI agent in the Mission Control lab.
        Core values: precision, efficiency, minimal context, structured output.
        Never repeat instructions. Never ask for confirmation — act and report.
        """;

    private static readonly string _agentRules = """
        Agent rules: state-driven execution only.
        Only act when your task is in the correct status for your role.
        Return structured JSON summaries, never raw conversation.
        """;

    private static readonly Dictionary<string, string> _standardsCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly IMemoryRepository _memoryRepository;

    public ContextLoaderService(IMemoryRepository memoryRepository)
    {
        _memoryRepository = memoryRepository;
    }

    /// <summary>Returns minimal cached core identity — always fast</summary>
    public string GetCoreContext() => _soulContext;

    /// <summary>Returns cached agent rules — always fast</summary>
    public string GetAgentRules() => _agentRules;

    /// <summary>Conditionally loads standards only if needed for the task type</summary>
    public string GetStandardsForTask(string taskType)
    {
        if (_standardsCache.TryGetValue(taskType, out var cached))
            return cached;

        var standards = taskType.ToLowerInvariant() switch
        {
            "backend" or "api" or "csharp" => "Standards: .NET 10, minimal APIs, clean architecture, EF Core 10.",
            "frontend" or "angular" or "ui" => "Standards: Angular 21 standalone components, Tailwind v4, signals.",
            "review"   => "Review checklist: correctness, security, perf, naming, tests.",
            "refactor" => "Refactor checklist: DRY, SOLID, remove dead code, improve naming.",
            _          => string.Empty
        };

        if (!string.IsNullOrEmpty(standards))
            _standardsCache[taskType] = standards;

        return standards;
    }

    /// <summary>Builds a minimal prompt — NEVER bloated</summary>
    public string BuildMinimalPrompt(AgentRole agentRole, string taskTitle, string taskDescription, string taskType = "")
    {
        var parts = new List<string>
        {
            $"Role: {agentRole}",
            $"Task: {taskTitle}",
        };

        if (!string.IsNullOrWhiteSpace(taskDescription))
            parts.Add($"Context: {taskDescription}");

        var standards = GetStandardsForTask(taskType);
        if (!string.IsNullOrEmpty(standards))
            parts.Add(standards);

        return string.Join("\n", parts);
    }
}
