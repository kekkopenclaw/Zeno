using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MissionControl.Application.Interfaces;
using MissionControl.Application.Services;
using MissionControl.Domain.Enums;
using MissionControl.Domain.Interfaces;

namespace MissionControl.Infrastructure.Services;

/// <summary>
/// KairosDaemonService — ported from Claw-code KAIROS background autonomy pattern.
///
/// Wakes every day at 02:00 (local time) and performs the nightly memory refactor:
///   1. Scans ~/.openclaw/workspace/memory/*.md for daily log files.
///   2. Reads each file and chunks content into ≤5 high-entropy sentences.
///   3. Uses Ollama (llama3) to distil each chunk → task_id / problem / fix / lesson.
///   4. Determines the best MemoryType (LongTerm, Insight, Decision) from the content.
///   5. Saves structured MemorySummary (layer 2) + MemoryEntry (layer 3) to SQLite.
///   6. Broadcasts each new memory via SignalR so the Memory tab updates live.
///
/// Config (appsettings.json / env vars):
///   Kairos:MemoryDir   — path to scan (default: ~/.openclaw/workspace/memory)
///   Kairos:RunHour     — hour to fire (default: 2 = 02:00)
///   Kairos:Model       — Ollama model for distillation (default: llama3)
///   Kairos:ProjectId   — target project ID (default: 1)
/// </summary>
public sealed class KairosDaemonService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<KairosDaemonService> _logger;
    private readonly string _memoryDir;
    private readonly int _runHour;
    private readonly string _model;
    private readonly int _projectId;

    public KairosDaemonService(
        IServiceScopeFactory scopeFactory,
        IConfiguration config,
        ILogger<KairosDaemonService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;

        _memoryDir = ExpandHome(
            config["Kairos:MemoryDir"] ?? "~/.openclaw/workspace/memory");
        _runHour = config.GetValue<int>("Kairos:RunHour", 2);
        _model = config["Kairos:Model"] ?? "llama3";
        _projectId = config.GetValue<int>("Kairos:ProjectId", 1);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "⏰ KAIROS daemon started — fires daily at {Hour:D2}:00, memory dir: {Dir}",
            _runHour, _memoryDir);

        while (!stoppingToken.IsCancellationRequested)
        {
            var delay = TimeUntilNextRun(_runHour);
            _logger.LogDebug("KAIROS sleeping for {Hours:F1} hours until next run.", delay.TotalHours);

            try
            {
                await Task.Delay(delay, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            await RunMemoryRefactorAsync(stoppingToken);
        }

        _logger.LogInformation("KAIROS daemon stopped.");
    }

    // ── Main nightly routine ──────────────────────────────────────────────

    private async Task RunMemoryRefactorAsync(CancellationToken ct)
    {
        _logger.LogInformation("🧠 KAIROS 2 AM memory refactor running...");

        if (!Directory.Exists(_memoryDir))
        {
            _logger.LogWarning("KAIROS: memory dir not found at {Dir} — skipping.", _memoryDir);
            return;
        }

        var mdFiles = Directory.GetFiles(_memoryDir, "*.md", SearchOption.TopDirectoryOnly);
        if (mdFiles.Length == 0)
        {
            _logger.LogInformation("KAIROS: no .md files found — nothing to distil.");
            return;
        }

        _logger.LogInformation("KAIROS: found {Count} memory file(s) to process.", mdFiles.Length);

        // Process last 5 daily logs max per run
        var latest = mdFiles
            .Select(f => new FileInfo(f))
            .OrderByDescending(fi => fi.LastWriteTimeUtc)
            .Take(5)
            .ToList();

        foreach (var file in latest)
        {
            if (ct.IsCancellationRequested) break;

            // Create a fresh scope for each file so repos get a clean DbContext + OllamaClient
            using var scope = _scopeFactory.CreateScope();
            var kairosMemory = scope.ServiceProvider.GetRequiredService<KairosMemoryService>();
            var ollama       = scope.ServiceProvider.GetRequiredService<IOllamaClient>();
            var chroma       = scope.ServiceProvider.GetRequiredService<IChromaVectorService>();
            var reflection   = scope.ServiceProvider.GetRequiredService<ReflectionLoopService>();
            await ProcessMemoryFileAsync(file, kairosMemory, ollama, chroma, reflection, ct);
        }

        _logger.LogInformation("✅ KAIROS nightly memory refactor complete.");
    }

    private async Task ProcessMemoryFileAsync(
        FileInfo file,
        KairosMemoryService kairosMemory,
        IOllamaClient ollama,
        IChromaVectorService chroma,
        ReflectionLoopService reflection,
        CancellationToken ct)
    {
        _logger.LogInformation("KAIROS: processing {FileName}", file.Name);

        string raw;
        try
        {
            raw = await File.ReadAllTextAsync(file.FullName, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "KAIROS: failed to read {FileName} — skipping.", file.Name);
            return;
        }

        if (string.IsNullOrWhiteSpace(raw)) return;

        // Chunk into ≤5 high-entropy sentences
        var chunk = KairosMemoryService.ChunkToSentences(raw, maxSentences: 5);

        // Ask Ollama to distil the chunk into structured text
        var distillationPrompt = $"""
You are Trunks, a memory distillation agent.
Read this raw memory chunk from a development session log and extract structured information.

CHUNK:
{chunk}

Respond in this EXACT format (no markdown, no extra text):
TASK_ID: <integer or 0 if unknown>
PROBLEM: <one sentence describing the core problem>
FIX: <one sentence describing the solution>
LESSON: <one sentence of generalizable learning>
TYPE: <LongTerm | Insight | Decision>
TAGS: <comma-separated keywords>
""";

        string distilled;
        try
        {
            distilled = await ollama.GenerateAsync(_model, distillationPrompt, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "KAIROS: Ollama distillation failed for {FileName} — using fallback.", file.Name);
            distilled = $"TASK_ID: 0\nPROBLEM: (from log {file.Name})\nFIX: See attached\nLESSON: {chunk}\nTYPE: Insight\nTAGS: kairos,auto";
        }

        // Parse the structured response
        var parsed = ParseDistilled(distilled);
        var title = $"KAIROS: {file.Name.Replace(".md", "")} — {DateTime.UtcNow:yyyy-MM-dd}";

        // Save layer 2: MemorySummary
        await kairosMemory.DistilSummaryAsync(
            projectId: _projectId,
            taskItemId: parsed.TaskId > 0 ? parsed.TaskId : null,
            problem: parsed.Problem,
            fix: parsed.Fix,
            lesson: parsed.Lesson,
            agentRole: "Trunks",
            retries: 0,
            complexity: 5,
            ct);

        // Save layer 3: MemoryEntry (long-term distilled)
        var content = $"Problem: {parsed.Problem}\nFix: {parsed.Fix}\nLesson: {parsed.Lesson}";
        await kairosMemory.WriteDistilledMemoryAsync(
            projectId: _projectId,
            title: title,
            content: content,
            type: parsed.Type,
            tags: parsed.Tags,
            ct: ct);

        // ── Layer 4: Embed into Chroma vector store ───────────────────────
        var vectorId = $"kairos_{file.Name.Replace(".md", "").Replace(" ", "_")}_{DateTime.UtcNow:yyyyMMdd}";
        await chroma.EmbedAndStoreAsync(
            id: vectorId,
            text: content,
            metadata: new Dictionary<string, string>
            {
                ["task_id"] = parsed.TaskId.ToString(),
                ["type"]    = parsed.Type.ToString(),
                ["tags"]    = parsed.Tags,
                ["source"]  = "kairos_daemon"
            },
            ct: ct);

        // ── Reflection: derive new rule from this distillation ────────────
        await reflection.ReflectAsync(parsed.TaskId, parsed.Problem, parsed.Fix, parsed.Lesson, ct);
    }

    // ── Parsing helpers ───────────────────────────────────────────────────

    private static DistilledMemory ParseDistilled(string response)
    {
        var result = new DistilledMemory();
        foreach (var rawLine in response.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var line = rawLine.Trim();
            if (line.StartsWith("TASK_ID:", StringComparison.OrdinalIgnoreCase))
                int.TryParse(line[8..].Trim(), out result.TaskId);
            else if (line.StartsWith("PROBLEM:", StringComparison.OrdinalIgnoreCase))
                result.Problem = line[8..].Trim();
            else if (line.StartsWith("FIX:", StringComparison.OrdinalIgnoreCase))
                result.Fix = line[4..].Trim();
            else if (line.StartsWith("LESSON:", StringComparison.OrdinalIgnoreCase))
                result.Lesson = line[7..].Trim();
            else if (line.StartsWith("TYPE:", StringComparison.OrdinalIgnoreCase))
                result.Type = ParseMemoryType(line[5..].Trim());
            else if (line.StartsWith("TAGS:", StringComparison.OrdinalIgnoreCase))
                result.Tags = line[5..].Trim();
        }

        if (string.IsNullOrWhiteSpace(result.Problem)) result.Problem = "Unstructured memory entry.";
        if (string.IsNullOrWhiteSpace(result.Fix)) result.Fix = "See content.";
        if (string.IsNullOrWhiteSpace(result.Lesson)) result.Lesson = "Review and apply.";
        if (string.IsNullOrWhiteSpace(result.Tags)) result.Tags = "kairos,auto";

        return result;
    }

    private static MemoryType ParseMemoryType(string raw) => raw.ToLowerInvariant() switch
    {
        "insight" => MemoryType.Insight,
        "decision" => MemoryType.Decision,
        _ => MemoryType.LongTerm
    };

    // ── Scheduling ────────────────────────────────────────────────────────

    /// <summary>Returns the TimeSpan until the next occurrence of <paramref name="hour"/>:00 local time.</summary>
    internal static TimeSpan TimeUntilNextRun(int hour)
    {
        var now = DateTime.Now;
        var next = now.Date.AddHours(hour);
        if (next <= now) next = next.AddDays(1);
        return next - now;
    }

    private static string ExpandHome(string path) =>
        path.StartsWith("~/", StringComparison.Ordinal)
            ? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), path[2..])
            : path;

    // ── Value type ────────────────────────────────────────────────────────

    private sealed class DistilledMemory
    {
        public int TaskId;
        public string Problem = string.Empty;
        public string Fix = string.Empty;
        public string Lesson = string.Empty;
        public MemoryType Type = MemoryType.Insight;
        public string Tags = string.Empty;
    }
}
