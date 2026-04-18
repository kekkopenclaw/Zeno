namespace MissionControl.Domain.Events;

public abstract class BaseEvent
{
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
}
