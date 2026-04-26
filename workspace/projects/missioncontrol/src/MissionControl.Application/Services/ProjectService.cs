using MissionControl.Application.DTOs;
using MissionControl.Domain.Entities;
using MissionControl.Domain.Interfaces;

namespace MissionControl.Application.Services;

public class ProjectService
{
    private readonly IProjectRepository _repository;

    public ProjectService(IProjectRepository repository)
    {
        _repository = repository;
    }

    public async Task<IReadOnlyList<ProjectDto>> GetAllAsync()
    {
        var projects = await _repository.GetAllAsync();
        return projects.Select(MapToDto).ToList();
    }

    public async Task<ProjectDto?> GetByIdAsync(int id)
    {
        var project = await _repository.GetByIdAsync(id);
        return project == null ? null : MapToDto(project);
    }

    public async Task<ProjectDto> CreateAsync(CreateProjectDto dto)
    {
        var project = new Project
        {
            Name = dto.Name,
            Description = dto.Description,
            CreatedAt = DateTime.UtcNow
        };
        var created = await _repository.AddAsync(project);
        return MapToDto(created);
    }

    public async Task<ProjectDto?> UpdateAsync(int id, CreateProjectDto dto)
    {
        var project = await _repository.GetByIdAsync(id);
        if (project == null) return null;
        project.Name = dto.Name;
        project.Description = dto.Description;
        await _repository.UpdateAsync(project);
        return MapToDto(project);
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var project = await _repository.GetByIdAsync(id);
        if (project == null) return false;
        await _repository.DeleteAsync(project);
        return true;
    }

    private static ProjectDto MapToDto(Project p) => new()
    {
        Id = p.Id,
        Name = p.Name,
        Description = p.Description,
        CreatedAt = p.CreatedAt
    };
}
