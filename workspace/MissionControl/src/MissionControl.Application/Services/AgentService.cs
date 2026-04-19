using MissionControl.Application.DTOs;
using MissionControl.Application.Interfaces;
using MissionControl.Domain.Entities;
using MissionControl.Domain.Enums;
using MissionControl.Domain.Interfaces;

namespace MissionControl.Application.Services;

public class AgentService
{
    private readonly IAgentRepository _repository;
    private readonly IOpenClawRunner? _openClawRunner;
    private readonly ISignalRNotifier? _notifier;

    public AgentService(IAgentRepository repository, IOpenClawRunner? openClawRunner = null, ISignalRNotifier? notifier = null)
    {
        _repository      = repository;
        _openClawRunner  = openClawRunner;
        _notifier        = notifier;
    }

    public async Task<IReadOnlyList<AgentDto>> GetAllAsync()
    {
        var agents = await _repository.GetAllAsync();
        return agents.Select(MapToDto).ToList();
    }

    public async Task<IReadOnlyList<AgentDto>> GetByProjectIdAsync(int projectId)
    {
        var agents = await _repository.GetByProjectIdAsync(projectId);
        return agents.Select(MapToDto).ToList();
    }

    public async Task<AgentDto?> GetByIdAsync(int id)
    {
        var agent = await _repository.GetByIdAsync(id);
        return agent == null ? null : MapToDto(agent);
    }

    public async Task<AgentDto> CreateAsync(CreateAgentDto dto)
    {
        var role    = Enum.TryParse<AgentRole>(dto.Role, out var r) ? r : AgentRole.Kakarot;
        var backend = Enum.TryParse<ExecutionBackend>(dto.ExecutionBackend, out var b) ? b : ExecutionBackend.Ollama;
        var agent = new Agent
        {
            Name             = dto.Name,
            Model            = dto.Model,
            Role             = role,
            Description      = dto.Description,
            Skills           = dto.Skills,
            Emoji            = dto.Emoji,
            ProjectId        = dto.ProjectId,
            Color            = dto.Color,
            Status           = AgentStatus.Idle,
            ExecutionBackend = backend,
            ToolsEnabled     = dto.ToolsEnabled,
            PushRole         = dto.PushRole,
        };
        // Sanitize name: lowercase, dashes, alphanumeric only
        string Sanitize(string name) => string.Concat(name.ToLowerInvariant().Replace(" ", "-").Where(c => char.IsLetterOrDigit(c) || c == '-'));
        // 1. Add to DB to get Id
        var created = await _repository.AddAsync(agent);
        var sanitized = Sanitize(created.Name);
        created.OpenClawAgentId = $"mc-{created.Id}-{sanitized}";
        await _repository.UpdateAsync(created);
        // 2. Create agent in OpenClaw CLI (source of truth)
        if (_openClawRunner != null)
        {
            var ws = _openClawRunner.WorkspaceRoot + $"/{created.OpenClawAgentId}";
            var cliResult = await _openClawRunner.SpawnAgentAsync(created.OpenClawAgentId, created.Model, ws);
            if (string.IsNullOrWhiteSpace(cliResult))
                throw new Exception("Failed to create agent in OpenClaw CLI. DB will not be updated.");
        }
        return MapToDto(created);
    }

    public async Task<AgentDto?> UpdateAsync(int id, CreateAgentDto dto)
    {
        var agent = await _repository.GetByIdAsync(id);
        if (agent == null) return null;
        var role    = Enum.TryParse<AgentRole>(dto.Role, out var r) ? r : AgentRole.Kakarot;
        var backend = Enum.TryParse<ExecutionBackend>(dto.ExecutionBackend, out var b) ? b : ExecutionBackend.Ollama;
        agent.Name             = dto.Name;
        agent.Model            = dto.Model;
        agent.Role             = role;
        agent.Description      = dto.Description;
        agent.Skills           = dto.Skills;
        agent.Emoji            = dto.Emoji;
        agent.ProjectId        = dto.ProjectId;
        agent.OpenClawAgentId  = dto.OpenClawAgentId;
        agent.ExecutionBackend = backend;
        agent.ToolsEnabled     = dto.ToolsEnabled;
        agent.PushRole         = dto.PushRole;
        agent.Color            = dto.Color;
        await _repository.UpdateAsync(agent);
        if (_notifier != null)
            await _notifier.NotifyAgentUpdatedAsync(MapToDto(agent));
        return MapToDto(agent);
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var agent = await _repository.GetByIdAsync(id);
        if (agent == null) return false;
        // Remove from OpenClaw first (source of truth)
        if (_openClawRunner != null && !string.IsNullOrWhiteSpace(agent.OpenClawAgentId))
        {
            var ok = await _openClawRunner.DeleteAgentAsync(agent.OpenClawAgentId);
            if (!ok) return false; // Only remove from DB if OpenClaw removal succeeded
        }
        await _repository.DeleteAsync(agent);
        return true;
    }

    // ── OpenClaw lifecycle ───────────────────────────────────────────────────

    public async Task<AgentDto?> SpawnOpenClawAgentAsync(int agentId, string model)
    {
        var agent = await _repository.GetByIdAsync(agentId);
        if (agent == null) return null;

        // Ensure OpenClawAgentId is set and sanitized
        string Sanitize(string name) => string.Concat(name.ToLowerInvariant().Replace(" ", "-").Where(c => char.IsLetterOrDigit(c) || c == '-'));
        var sanitized = Sanitize(agent.Name);
        agent.OpenClawAgentId = $"mc-{agent.Id}-{sanitized}";
        agent.Status          = AgentStatus.Working;
        agent.IsPaused        = false;

        if (_openClawRunner != null)
        {
            var ws = _openClawRunner.WorkspaceRoot + $"/{agent.OpenClawAgentId}";
            await _openClawRunner.SpawnAgentAsync(agent.OpenClawAgentId, model, ws);
        }

        await _repository.UpdateAsync(agent);
        var dto = MapToDto(agent);
        if (_notifier != null) await _notifier.NotifyAgentStarted(dto);
        return dto;
    }

    public async Task<AgentDto?> PauseAgentAsync(int agentId)
    {
        var agent = await _repository.GetByIdAsync(agentId);
        if (agent == null) return null;

        if (_openClawRunner != null && !string.IsNullOrWhiteSpace(agent.OpenClawAgentId))
            await _openClawRunner.PauseAgentAsync(agent.OpenClawAgentId);

        agent.IsPaused = true;
        agent.Status   = AgentStatus.Paused;
        await _repository.UpdateAsync(agent);
        var dto = MapToDto(agent);
        if (_notifier != null) await _notifier.NotifyAgentStarted(dto);
        return dto;
    }

    public async Task<AgentDto?> ResumeAgentAsync(int agentId)
    {
        var agent = await _repository.GetByIdAsync(agentId);
        if (agent == null) return null;

        agent.IsPaused = false;
        agent.Status   = AgentStatus.Idle;
        await _repository.UpdateAsync(agent);
        var dto = MapToDto(agent);
        if (_notifier != null) await _notifier.NotifyAgentStarted(dto);
        return dto;
    }

    public static AgentDto MapToDto(Agent a) => new()
    {
        Id              = a.Id,
        Name            = a.Name,
        Model           = a.Model,
        Status          = a.Status.ToString(),
        Role            = a.Role.ToString(),
        Description     = a.Description,
        Skills          = a.Skills,
        Emoji           = a.Emoji,
        OpenClawAgentId = a.OpenClawAgentId,
        IsPaused        = a.IsPaused,
        ProjectId       = a.ProjectId,
        ExecutionBackend = a.ExecutionBackend.ToString(),
        ToolsEnabled    = a.ToolsEnabled,
        PushRole        = a.PushRole,
    };
}
