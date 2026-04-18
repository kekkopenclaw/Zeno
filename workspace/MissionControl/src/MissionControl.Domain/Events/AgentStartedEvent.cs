using MissionControl.Domain.Entities;

namespace MissionControl.Domain.Events;

public class AgentStartedEvent : BaseEvent
{
    public Agent Agent { get; set; } = null!;
}
