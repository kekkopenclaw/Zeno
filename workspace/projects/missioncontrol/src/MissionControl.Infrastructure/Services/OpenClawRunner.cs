using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MissionControl.Domain.Interfaces;

namespace MissionControl.Infrastructure.Services;

public sealed class OpenClawRunner : IOpenClawRunner
{
    // ...existing code...

    public async Task<bool> DeleteAgentAsync(string agentName, CancellationToken ct = default)
    {
        try
        {
            var (exitCode, output) = await RunProcessAsync(_exe, $"agents delete {agentName} --force", ct);
            if (exitCode == 0 && output.Contains("Deleted agent", StringComparison.OrdinalIgnoreCase))
                return true;
            if (output.Contains("not found", StringComparison.OrdinalIgnoreCase))
                return true; // Already gone
            _logger.LogWarning("Failed to delete OpenClaw agent {AgentName}: {Output}", agentName, output);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Exception deleting OpenClaw agent {AgentName}", agentName);
            return false;
        }
    }
    /// <summary>
    /// Gets the workspace path for an agent by querying the OpenClaw CLI config.
    /// </summary>
    public async Task<string?> GetWorkspacePathAsync(string agentName, CancellationToken ct = default)
    {
        try
        {
            var (exitCode, output) = await RunProcessAsync(_exe, $"config get --agent {agentName} workspace.root", ct);
            if (exitCode == 0)
            {
                var ws = output.Trim().Split('\n').FirstOrDefault();
                return string.IsNullOrWhiteSpace(ws) ? null : ws;
            }
            _logger.LogWarning("Failed to get workspace path for agent {AgentName}: {Output}", agentName, output);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Exception getting workspace path for agent {AgentName}", agentName);
            return null;
        }
    }
    public string WorkspaceRoot => _workspaceRoot;
    private readonly string _exe;
    private readonly string _workspaceRoot;
    private readonly string _logRoot;
    private readonly ILogger<OpenClawRunner> _logger;
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new();

    public OpenClawRunner(IConfiguration config, ILogger<OpenClawRunner> logger)
    {
        _logger = logger;
        _exe = config["OpenClaw:ExecutablePath"] ?? "openclaw";
        _workspaceRoot = ExpandHome(config["OpenClaw:WorkspaceRoot"] ?? "~/.openclaw/workspaces");
        _logRoot = ExpandHome(config["OpenClaw:LogRoot"] ?? "~/.openclaw/logs");
    }
    public async Task<bool> AgentExistsAsync(string agentName, CancellationToken ct = default)
    {
        try
        {
            var (listCode, listOut) = await RunProcessAsync(_exe, $"agents list", ct);
            var exists = listOut.Split('\n').Any(line => line.Trim().ToLower().StartsWith(agentName.ToLower() + " "));
            return exists;
        }
        catch
        {
            return false;
        }
    }

    // ── Public API ───────────────────────────────────────────────────────────

    public async Task<string> SpawnAgentAsync(string agentName, string model, string workspace, CancellationToken ct = default)
    {
        var sem = _locks.GetOrAdd(agentName, _ => new SemaphoreSlim(1, 1));
        await sem.WaitAsync(ct);
        try
        {
            var wsPath = Path.Combine(_workspaceRoot, agentName);
            _logger.LogInformation("Spawning OpenClaw agent {AgentName} model={Model} workspace={Workspace}", agentName, model, wsPath);

            // Check if OpenClaw CLI is available
            try
            {
                var whichResult = await RunProcessAsync("which", _exe, ct);
                if (whichResult.ExitCode != 0)
                {
                    _logger.LogError("OpenClaw CLI not found: {Exe}. Please install OpenClaw CLI and ensure it is in PATH.", _exe);
                    return agentName;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking OpenClaw CLI availability");
                return agentName;
            }

            // Check if agent exists (case-insensitive)
            var (listCode, listOut) = await RunProcessAsync(_exe, $"agents list", ct);
            var exists = listOut.Split('\n').Any(line => line.Trim().ToLower().StartsWith(agentName.ToLower() + " "));
            if (!exists)
            {
                var (addCode, addOut) = await RunProcessAsync(_exe, $"agents add {agentName} --workspace {wsPath} --model {model} --non-interactive", ct);
                _logger.LogInformation("OpenClaw agents add output: {Output}", addOut);
                if (addCode != 0)
                {
                    _logger.LogError("Failed to add OpenClaw agent {AgentName}: {Output}", agentName, addOut);
                }
            }
            else
            {
                _logger.LogInformation("OpenClaw agent {AgentName} already exists.", agentName);
            }

            _logger.LogInformation("OpenClaw agent {AgentName} ready. Workspace: {Workspace}", agentName, wsPath);
            return agentName;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "OpenClaw CLI unavailable — SpawnAgentAsync degraded for {AgentName}", agentName);
            return agentName;   // return the name anyway; UI can still track it
        }
        finally
        {
            sem.Release();
        }
    }

    public async Task TriggerTaskAsync(string agentName, string message, CancellationToken ct = default)
    {
        try
        {
            var safe = message.Replace("\"", "\\\"");
            await RunProcessAsync(_exe, $"agent --agent {agentName} --message \"{safe}\"", ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "TriggerTask degraded for {AgentName}", agentName);
        }
    }

    public async Task PauseAgentAsync(string agentName, CancellationToken ct = default)
    {
        try
        {
            // OpenClaw CLI: pausing an agent is done via chat command
            await RunProcessAsync(_exe, $"agent --agent {agentName} --message \"/pause\"", ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "PauseAgent degraded for {AgentName}", agentName);
        }
    }

    public string GetLogPath(string agentName) =>
        Path.Combine(_workspaceRoot, agentName, "memory");

    public async IAsyncEnumerable<string> TailLogStreamAsync(
        string agentName,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var logFile = Path.Combine(_logRoot, $"{agentName}.log");

        // Wait up to 10 s for the file to appear
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (!File.Exists(logFile) && DateTime.UtcNow < deadline)
        {
            await Task.Delay(500, ct);
        }

        if (!File.Exists(logFile))
        {
            _logger.LogDebug("Log file not found for {AgentName}, tailing skipped", agentName);
            yield break;
        }

        using var fs = new FileStream(logFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = new StreamReader(fs);

        // Seek to end so we only tail new lines
        fs.Seek(0, SeekOrigin.End);

        using var watcher = new FileSystemWatcher(Path.GetDirectoryName(logFile)!, Path.GetFileName(logFile))
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size,
            EnableRaisingEvents = true
        };

        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        watcher.Changed += (_, _) => tcs.TrySetResult(true);

        while (!ct.IsCancellationRequested)
        {
            string? line;
            while ((line = await reader.ReadLineAsync(ct)) != null)
            {
                yield return line;
            }

            // Reset and wait for next write event (with a max 5 s timeout)
            tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            watcher.Changed += (_, _) => tcs.TrySetResult(true);
            try
            {
                await tcs.Task.WaitAsync(TimeSpan.FromSeconds(5), ct);
            }
            catch (TimeoutException) { /* normal — just loop */ }
        }
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private async Task<(int ExitCode, string Output)> RunProcessAsync(string exe, string args, CancellationToken ct = default)
    {
        var psi = new ProcessStartInfo(exe, args)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        using var process = new Process { StartInfo = psi, EnableRaisingEvents = true };
        var output = new System.Text.StringBuilder();

        process.OutputDataReceived += (_, e) => { if (e.Data != null) output.AppendLine(e.Data); };
        process.ErrorDataReceived += (_, e) => { if (e.Data != null) output.AppendLine(e.Data); };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        await process.WaitForExitAsync(ct);

        var result = output.ToString();
        if (process.ExitCode != 0)
        {
            // Special case: 'agents add' returns error if agent already exists, treat as non-fatal
            if (args.StartsWith("agents add") && result.Contains("already exists", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogInformation("Agent already exists, skipping error for: {Args}", args);
                return (process.ExitCode, result);
            }
            _logger.LogWarning("openclaw {Args} exited {Code}: {Output}", args, process.ExitCode, result.Trim());
            throw new InvalidOperationException($"openclaw {args} failed (exit {process.ExitCode}): {result.Trim()}");
        }
        return (process.ExitCode, result);
    }

    private static string ExpandHome(string path) =>
        path.StartsWith("~/", StringComparison.Ordinal)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), path[2..])
            : path;
}
