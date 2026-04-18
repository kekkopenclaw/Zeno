using Microsoft.AspNetCore.Mvc;
using MissionControl.Application.DTOs;
using MissionControl.Application.Services;
using MissionControl.Domain.Entities;

namespace MissionControl.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TeamsController : ControllerBase
{
    private readonly TeamService _teamService;

    public TeamsController(TeamService teamService)
    {
        _teamService = teamService;
    }

    [HttpGet("by-project/{projectId}")]
    public async Task<IActionResult> GetByProject(int projectId)
    {
        var teams = await _teamService.GetByProjectIdAsync(projectId);
        return Ok(teams);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateTeamDto dto)
    {
        var team = await _teamService.CreateAsync(dto);
        return Ok(team);
    }

    [HttpPost("{teamId}/add-agent")]
    public async Task<IActionResult> AddAgent(int teamId, [FromBody] int agentId)
    {
        await _teamService.AddAgentAsync(teamId, agentId);
        return NoContent();
    }

    [HttpPost("{teamId}/remove-agent")]
    public async Task<IActionResult> RemoveAgent(int teamId, [FromBody] int agentId)
    {
        await _teamService.RemoveAgentAsync(teamId, agentId);
        return NoContent();
    }

    [HttpGet("{teamId}/agents")]
    public async Task<IActionResult> GetAgents(int teamId)
    {
        var agents = await _teamService.GetAgentsAsync(teamId);
        return Ok(agents);
    }
}
