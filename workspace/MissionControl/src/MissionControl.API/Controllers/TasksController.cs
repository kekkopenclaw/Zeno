using Microsoft.AspNetCore.Mvc;
using MissionControl.Application.DTOs;
using MissionControl.Application.Services;

namespace MissionControl.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TasksController : ControllerBase
{
    private readonly TaskService _service;

    public TasksController(TaskService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll() => Ok(await _service.GetAllAsync());

    [HttpGet("project/{projectId}")]
    public async Task<IActionResult> GetByProject(int projectId) =>
        Ok(await _service.GetByProjectIdAsync(projectId));

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        var result = await _service.GetByIdAsync(id);
        return result == null ? NotFound() : Ok(result);
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateTaskItemDto dto)
    {
        var result = await _service.CreateAsync(dto);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, CreateTaskItemDto dto)
    {
        var result = await _service.UpdateAsync(id, dto);
        return result == null ? NotFound() : Ok(result);
    }

    // Support both PUT and PATCH for status updates
    [HttpPut("{id}/status")]
    public async Task<IActionResult> UpdateStatus(int id, UpdateTaskStatusDto dto)
    {
        var result = await _service.UpdateStatusAsync(id, dto);
        return result == null ? NotFound() : Ok(result);
    }

    [HttpPatch("{id}/status")]
    public async Task<IActionResult> PatchStatus(int id, UpdateTaskStatusDto dto)
    {
        var result = await _service.UpdateStatusAsync(id, dto);
        return result == null ? NotFound() : Ok(result);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var success = await _service.DeleteAsync(id);
        return success ? NoContent() : NotFound();
    }
}
