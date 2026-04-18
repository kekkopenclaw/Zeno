using MissionControl.Domain.Events;

namespace MissionControl.Domain.Interfaces;

public interface IEventPublisher
{
    Task PublishAsync<T>(T domainEvent) where T : BaseEvent;
}
