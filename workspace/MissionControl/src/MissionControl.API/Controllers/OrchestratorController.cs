using Microsoft.AspNetCore.Mvc;
using MissionControl.Application.DTOs;
using MissionControl.Application.Services;

namespace MissionControl.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class OrchestratorController : ControllerBase
{
    private readonly OrchestratorService _orchestrator;
    private readonly ReviewService _review;

    public OrchestratorController(OrchestratorService orchestrator, ReviewService review)
    {
        _orchestrator = orchestrator;
        _review = review;
    }

    /// <summary>Manually trigger an orchestration tick for a project</summary>
    [HttpPost("tick/{projectId}")]
    public async Task<IActionResult> Tick(int projectId)
    {
        await _orchestrator.TickAsync(projectId);
        return Ok(new { message = "Orchestration tick completed", projectId });
    }

    /// <summary>Pass a task review — moves task to Done</summary>
    [HttpPost("review/{taskId}/pass")]
    public async Task<IActionResult> PassReview(int taskId, [FromQuery] int projectId)
    {
        var result = await _review.PassAsync(taskId, projectId);
        return Ok(result);
    }

    /// <summary>Fail a task review — sends back for fixing or escalates</summary>
    [HttpPost("review/{taskId}/fail")]
    public async Task<IActionResult> FailReview(int taskId, [FromQuery] int projectId, [FromBody] ReviewFeedbackDto dto)
    {
        var result = await _review.FailAsync(taskId, projectId, dto.Notes);
        return Ok(result);
    }
}

public record ReviewFeedbackDto(string Notes);
