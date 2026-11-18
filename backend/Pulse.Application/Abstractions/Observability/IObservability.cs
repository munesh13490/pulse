using Pulse.Domain;

namespace Pulse.Application.Abstractions.Observability;

public interface IObservability
{
    void Count(string operation);
}
