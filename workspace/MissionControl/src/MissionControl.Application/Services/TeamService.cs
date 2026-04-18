using MissionControl.Application.DTOs;
using MissionControl.Domain.Entities;
using MissionControl.Domain.Interfaces;

namespace MissionControl.Application.Services;

public class TeamService
{
    private readonly ITeamRepository _teamRepository;
    private readonly IAgentRepository _agentRepository;

    public TeamService(ITeamRepository teamRepository, IAgentRepository agentRepository)
    {
        _teamRepository = teamRepository;
        _agentRepository = agentRepository;
    }

    public async Task<IEnumerable<TeamDto>> GetByProjectIdAsync(int projectId)
    {
        var teams = await _teamRepository.GetByProjectIdAsync(projectId);
        return teams.Select(t => new TeamDto
        {
            Id = t.Id,
            Name = t.Name,
            Description = t.Description,
            ProjectId = t.ProjectId
        });
    }

    public async Task<TeamDto> CreateAsync(CreateTeamDto dto)
    {
        var team = new Team
        {
            Name = dto.Name,
            Description = dto.Description,
            ProjectId = dto.ProjectId
        };
        await _teamRepository.AddAsync(team);
        return new TeamDto
        {
            Id = team.Id,
            Name = team.Name,
            Description = team.Description,
            ProjectId = team.ProjectId
        };
    }

    public async Task AddAgentAsync(int teamId, int agentId)
    {
        var agent = await _agentRepository.GetByIdAsync(agentId);
        if (agent == null) throw new Exception("Agent not found");
        agent.TeamId = teamId;
        await _agentRepository.UpdateAsync(agent);
    }

    public async Task<IEnumerable<AgentDto>> GetAgentsAsync(int teamId)
    {
        var agents = await _agentRepository.GetByTeamIdAsync(teamId);
        return agents.Select(a => AgentService.MapToDto(a));
    }

    public async Task RemoveAgentAsync(int teamId, int agentId)
    {
        var agent = await _agentRepository.GetByIdAsync(agentId);
        if (agent == null) throw new Exception("Agent not found");
        if (agent.TeamId != teamId) throw new Exception("Agent is not in this team");
        agent.TeamId = null;
        await _agentRepository.UpdateAsync(agent);
    }
}
