using Microsoft.AspNetCore.Mvc;
using MissionControl.API.Middleware;
using MissionControl.Application.DTOs;
using MissionControl.Application.Interfaces;

namespace MissionControl.API.Controllers;

/// <summary>
/// Observability log API.
/// GET  /api/logs                       — latest 200 entries
/// GET  /api/logs/taskId/{taskId}       — logs for a specific task
/// GET  /api/logs/agent/{agentName}     — logs for a specific agent
/// GET  /api/logs/level/{level}         — logs filtered by level (Info/Warning/Error)
/// POST /api/logs/frontend              — receive telemetry from Angular
/// </summary>
[ApiController]
[Route("api/logs")]
public class LogsController : ControllerBase
{
    private readonly ILogService _logService;

    public LogsController(ILogService logService)
    {
        _logService = logService;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] int limit = 200) =>
        Ok(await _logService.GetAllAsync(limit));

    [HttpGet("taskId/{taskId}")]
    public async Task<IActionResult> GetByTask(string taskId, [FromQuery] int limit = 100) =>
        Ok(await _logService.GetByTaskIdAsync(taskId, limit));

    [HttpGet("agent/{agentName}")]
    public async Task<IActionResult> GetByAgent(string agentName, [FromQuery] int limit = 100) =>
        Ok(await _logService.GetByAgentNameAsync(agentName, limit));

    [HttpGet("level/{level}")]
    public async Task<IActionResult> GetByLevel(string level, [FromQuery] int limit = 100) =>
        Ok(await _logService.GetByLevelAsync(level, limit));

    /// <summary>Receive frontend telemetry (Angular GlobalErrorHandler + interceptor logs)</summary>
    [HttpPost("frontend")]
    public async Task<IActionResult> Frontend([FromBody] FrontendLogDto dto)
    {
        try
        {
            var correlationId = dto.CorrelationId
                ?? HttpContext.Items[CorrelationIdMiddleware.CorrelationIdHeader]?.ToString();

            await _logService.WriteAsync(
                level:         dto.Level,
                message:       dto.Message,
                agentName:     null,
                taskId:        null,
                correlationId: correlationId,
                action:        dto.Url != null ? $"URL:{dto.Url}" : "FrontendError",
                exception:     dto.StackTrace,
                source:        "Frontend");

            return Ok();
        }
        catch (Exception ex)
        {
            // Log the error to the backend logs for diagnostics
            await _logService.WriteAsync(
                level: "Error",
                message: $"Failed to process frontend log: {ex.Message}",
                exception: ex.ToString(),
                source: "FrontendEndpoint");
            return BadRequest(new { error = ex.Message, details = ex.ToString() });
        }
    }
}
