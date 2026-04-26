using System.Threading.Tasks;
using MissionControl.Domain.Entities;

namespace MissionControl.Application.Services;

using System.IO;
using System.Threading;
using MissionControl.Domain.Interfaces;

public static class AgentCompletionChecker
{
    /// <summary>
    /// Checks the agent's log file for a completion marker for the given task.
    /// </summary>
    public static async Task<bool> IsAgentWorkCompletedAsync(TaskItem task, Agent? agent, IOpenClawRunner openClawRunner, CancellationToken ct = default)
    {
        if (agent == null || openClawRunner == null || string.IsNullOrEmpty(agent.Name)) return false;
        var logPath = openClawRunner.GetLogPath(agent.Name);
        if (!Directory.Exists(logPath)) return false;
        // Look for a file named "task_{id}_done.txt" or similar marker
        var markerFile = Path.Combine(logPath, $"task_{task.Id}_done.txt");
        if (File.Exists(markerFile)) return true;
        // Alternatively, scan a log file for a completion line
        var logFile = Path.Combine(logPath, "agent.log");
        if (File.Exists(logFile))
        {
            var lines = await File.ReadAllLinesAsync(logFile, ct);
            if (lines.Any(line => line.Contains($"Task {task.Id} completed", System.StringComparison.OrdinalIgnoreCase)))
                return true;
        }
        return false;
    }
}