using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MissionControl.Domain.Interfaces;

namespace MissionControl.Infrastructure.Services;

/// <summary>
/// MemoryConsolidationService — background job that periodically prunes and
/// de-duplicates low-value MemoryEntry records.
///
/// Runs every 6 hours. On each run:
///   1. Identifies entries older than 30 days with zero usage and SuccessRate ≤ 0.3
///      → these are stale, low-signal entries.  Deletes up to 20 per run.
///   2. Caps total non-rule entries per project at 200
///      → removes the lowest-scored (SuccessRate × UsageCount) entries first.
///
/// Rules (IsRule = true) are never touched by this service.
///
/// Config:
///   Consolidation:IntervalHours  — how often to run (default: 6)
///   Consolidation:StaleAgeDays   — min age before an unused entry is stale (default: 30)
///   Consolidation:MaxPerProject  — non-rule entry cap per project (default: 200)
/// </summary>
public sealed class MemoryConsolidationService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<MemoryConsolidationService> _logger;
    private readonly TimeSpan _interval;
    private readonly int _staleAgeDays;
    private readonly int _maxPerProject;

    public MemoryConsolidationService(
        IServiceScopeFactory scopeFactory,
        IConfiguration config,
        ILogger<MemoryConsolidationService> logger)
    {
        _scopeFactory  = scopeFactory;
        _logger        = logger;
        _interval      = TimeSpan.FromHours(config.GetValue<int>("Consolidation:IntervalHours", 6));
        _staleAgeDays  = config.GetValue<int>("Consolidation:StaleAgeDays",  30);
        _maxPerProject = config.GetValue<int>("Consolidation:MaxPerProject", 200);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "🧹 MemoryConsolidationService started — interval {Hours}h, stale after {Days}d, cap {Max}/project.",
            _interval.TotalHours, _staleAgeDays, _maxPerProject);

        // Run once immediately on startup so stale entries are cleaned up without
        // waiting a full interval cycle.
        await ConsolidateAsync(stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try { await Task.Delay(_interval, stoppingToken); }
            catch (OperationCanceledException) { break; }

            await ConsolidateAsync(stoppingToken);
        }

        _logger.LogInformation("MemoryConsolidationService stopped.");
    }

    private async Task ConsolidateAsync(CancellationToken ct)
    {
        _logger.LogInformation("🧹 MemoryConsolidation: starting pass...");

        using var scope = _scopeFactory.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IMemoryRepository>();

        var all = await repo.GetAllAsync();
        if (all.Count == 0) return;

        var cutoff = DateTime.UtcNow.AddDays(-_staleAgeDays);
        int removed = 0;

        // 1. Remove stale, zero-use, low-success entries (skip rules)
        var stale = all
            .Where(e => !e.IsRule
                     && e.UsageCount == 0
                     && e.SuccessRate <= 0.3
                     && e.CreatedAt < cutoff)
            .Take(20)
            .ToList();

        foreach (var e in stale)
        {
            if (ct.IsCancellationRequested) break;
            try
            {
                await repo.DeleteAsync(e);
                removed++;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Consolidation: failed to delete entry {Id}", e.Id);
            }
        }

        // 2. Per-project cap — remove lowest-scored non-rule entries beyond the cap
        var byProject = all
            .Except(stale)  // already removed
            .Where(e => !e.IsRule)
            .GroupBy(e => e.ProjectId);

        foreach (var group in byProject)
        {
            if (ct.IsCancellationRequested) break;

            var nonRules = group
                .OrderByDescending(e => e.SuccessRate * Math.Log(1 + e.UsageCount))
                .ToList();

            if (nonRules.Count <= _maxPerProject) continue;

            var toTrim = nonRules.Skip(_maxPerProject).ToList();
            foreach (var e in toTrim)
            {
                if (ct.IsCancellationRequested) break;
                try
                {
                    await repo.DeleteAsync(e);
                    removed++;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Consolidation: failed to cap-trim entry {Id}", e.Id);
                }
            }
        }

        _logger.LogInformation("🧹 MemoryConsolidation: removed {Count} entries.", removed);
    }
}
