using Pulse.Domain;

namespace Pulse.Application.Abstractions.Messaging;

public interface INotificationBus
{
    Task Publish(Guid tenant, string message);
}
