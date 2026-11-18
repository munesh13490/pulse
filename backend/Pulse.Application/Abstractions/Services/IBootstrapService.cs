namespace Pulse.Application.Abstractions.Services;

public interface IBootstrapService
{
    Task BootstrapAsync(string name, string password, CancellationToken ct);
}
