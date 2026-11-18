namespace Pulse.Application.Abstractions.Services;

public interface IHealthProbe
{
    Task<bool> IsReadyAsync(CancellationToken ct);
}
