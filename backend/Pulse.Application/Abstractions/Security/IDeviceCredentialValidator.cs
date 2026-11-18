using Pulse.Domain;

namespace Pulse.Application.Abstractions.Security;

public interface IDeviceCredentialValidator
{
    Task<DeviceIdentity?> Validate(string key, CancellationToken ct);
}
