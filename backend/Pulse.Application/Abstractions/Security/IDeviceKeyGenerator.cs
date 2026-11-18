namespace Pulse.Application.Abstractions.Security;

public interface IDeviceKeyGenerator
{
    DeviceKeySecret Generate();
}
