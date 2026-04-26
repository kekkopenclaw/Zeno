using Microsoft.AspNetCore.Mvc;
using MissionControl.Application.DTOs;
using MissionControl.Application.Services;

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

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        var team = await _teamService.GetByIdAsync(id);
        return team == null ? NotFound() : Ok(team);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateTeamDto dto)
    {
        var team = await _teamService.CreateAsync(dto);
        return CreatedAtAction(nameof(GetById), new { id = team.Id }, team);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] CreateTeamDto dto)
    {
        var result = await _teamService.UpdateAsync(id, dto);
        return result == null ? NotFound() : Ok(result);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var success = await _teamService.DeleteAsync(id);
        return success ? NoContent() : NotFound();
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
