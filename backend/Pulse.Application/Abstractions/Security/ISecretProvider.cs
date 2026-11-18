using Pulse.Domain;

namespace Pulse.Application.Abstractions.Security;

public interface ISecretProvider
{
    string Get(string name);
}
