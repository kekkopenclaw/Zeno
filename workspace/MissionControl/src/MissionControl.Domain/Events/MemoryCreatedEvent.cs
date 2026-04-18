using MissionControl.Domain.Entities;

namespace MissionControl.Domain.Events;

public class MemoryCreatedEvent : BaseEvent
{
    public MemoryEntry Memory { get; set; } = null!;
}
