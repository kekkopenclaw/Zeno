using MissionControl.Domain.Entities;

namespace MissionControl.Domain.Events;

public class TaskUpdatedEvent : BaseEvent
{
    public TaskItem Task { get; set; } = null!;
}
