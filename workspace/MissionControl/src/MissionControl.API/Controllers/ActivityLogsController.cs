using Microsoft.AspNetCore.Mvc;
using MissionControl.Application.DTOs;
using MissionControl.Application.Services;

namespace MissionControl.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ActivityLogsController : ControllerBase
{
    private readonly ActivityLogService _service;

    public ActivityLogsController(ActivityLogService service)
    {
        _service = service;
    }

    [HttpGet("project/{projectId}")]
    public async Task<IActionResult> GetByProject(int projectId, [FromQuery] int limit = 50) =>
        Ok(await _service.GetByProjectIdAsync(projectId, limit));

    [HttpGet("agent/{agentId}")]
    public async Task<IActionResult> GetByAgent(int agentId, [FromQuery] int limit = 100) =>
        Ok(await _service.GetByAgentIdAsync(agentId, limit));

    /// <summary>External agents (openclaw, etc.) can POST their activity logs here</summary>
    [HttpPost]
    public async Task<IActionResult> Create(CreateActivityLogDto dto)
    {
        var result = await _service.CreateAsync(dto);
        return CreatedAtAction(nameof(GetByProject), new { projectId = dto.ProjectId }, result);
    }
}
