namespace Pulse.Infrastructure.Security;

public sealed class DeviceKeyGenerator(ISecretProvider secrets) : IDeviceKeyGenerator
{
    public DeviceKeySecret Generate()
    {
        var secret = KeyMaterial.Random();
        return new(Guid.NewGuid(), secret, KeyMaterial.Digest(secret, secrets.Get("ApiKeyHmacKey")));
    }
}
