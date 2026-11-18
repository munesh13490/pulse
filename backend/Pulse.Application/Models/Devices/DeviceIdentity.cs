using Pulse.Domain;

namespace Pulse.Application.Models.Devices;

public record DeviceIdentity(Guid TenantId, Guid DeviceId);
