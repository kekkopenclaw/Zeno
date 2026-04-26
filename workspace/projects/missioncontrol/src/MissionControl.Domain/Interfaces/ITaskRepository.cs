using MissionControl.Domain.Entities;

namespace MissionControl.Domain.Interfaces;

public interface ITaskRepository : IRepository<TaskItem>
{
    Task<IReadOnlyList<TaskItem>> GetByProjectIdAsync(int projectId);
}
