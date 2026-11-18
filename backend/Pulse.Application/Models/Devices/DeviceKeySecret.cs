namespace Pulse.Application.Models.Devices;

public sealed record DeviceKeySecret(Guid Id, string Secret, byte[] Digest);
