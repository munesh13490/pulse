using Pulse.Domain;

namespace Pulse.Application.Abstractions.Security;

public interface ITokenIssuer
{
    string Issue(UserIdentity user);
}
