using Microsoft.AspNetCore.Mvc;
using MissionControl.Application.DTOs;
using MissionControl.Application.Interfaces;
using MissionControl.Application.Services;

namespace MissionControl.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MemoryController : ControllerBase
{
    private readonly MemoryService _service;
    private readonly IChromaVectorService _chroma;

    public MemoryController(MemoryService service, IChromaVectorService chroma)
    {
        _service = service;
        _chroma  = chroma;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll() => Ok(await _service.GetAllAsync());

    [HttpGet("project/{projectId}")]
    public async Task<IActionResult> GetByProject(int projectId) =>
        Ok(await _service.GetByProjectIdAsync(projectId));

    [HttpGet("project/{projectId}/search")]
    public async Task<IActionResult> Search(int projectId, [FromQuery] string q) =>
        Ok(await _service.SearchAsync(projectId, q ?? string.Empty));

    /// <summary>
    /// Semantic vector search across all embedded memories via ChromaDB v2.
    /// Returns the top-K closest matches by cosine similarity from the Chroma collection.
    /// Requires ChromaDB running at the configured base URL (default: http://localhost:8000).
    /// </summary>
    [HttpGet("semantic")]
    public async Task<IActionResult> SemanticSearch(
        [FromQuery] string query,
        [FromQuery] int topK = 6,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(query))
            return BadRequest(new { error = "query parameter is required." });

        var results = await _chroma.SearchAsync(query, topK, ct);
        return Ok(results);
    }

    /// <summary>
    /// Project-scoped semantic search — returns MemoryEntry rows ranked by similarity.
    /// Falls back to SQLite text search when ChromaDB is unavailable.
    /// </summary>
    [HttpGet("project/{projectId}/semantic-search")]
    public async Task<IActionResult> ProjectSemanticSearch(int projectId, [FromQuery] string q, [FromQuery] int n = 10) =>
        Ok(await _service.SemanticSearchAsync(projectId, q ?? string.Empty, n, HttpContext.RequestAborted));

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        var result = await _service.GetByIdAsync(id);
        return result == null ? NotFound() : Ok(result);
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateMemoryEntryDto dto)
    {
        var result = await _service.CreateAsync(dto);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, CreateMemoryEntryDto dto)
    {
        var result = await _service.UpdateAsync(id, dto);
        return result == null ? NotFound() : Ok(result);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var success = await _service.DeleteAsync(id);
        return success ? NoContent() : NotFound();
    }
}
