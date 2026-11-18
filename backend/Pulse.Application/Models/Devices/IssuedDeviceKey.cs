namespace Pulse.Application.Models.Devices;

public sealed record IssuedDeviceKey(Guid Id, string ApiKey, DateTimeOffset ExpiresAt);
