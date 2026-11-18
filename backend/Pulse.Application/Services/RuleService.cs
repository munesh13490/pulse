namespace Pulse.Application.Services;

public sealed class RuleService(IRuleRepository repository) : IRuleService
{
    public Task<List<Dictionary<string, object?>>> ListAsync(Guid tenant, CancellationToken ct) => repository.ListAsync(tenant, ct);
    public Task<List<Dictionary<string, object?>>> VersionsAsync(Guid tenant, Guid rule, CancellationToken ct) => repository.VersionsAsync(tenant, rule, ct);
    public async Task<Guid> CreateAsync(Guid tenant, RuleRequest request, CancellationToken ct)
    {
        Validate(request);
        var id = Guid.NewGuid();
        await repository.CreateAsync(tenant, id, request, ct);
        return id;
    }

    public Task<bool> AppendVersionAsync(Guid tenant, Guid id, RuleRequest request, CancellationToken ct)
    {
        Validate(request);
        return repository.AppendVersionAsync(tenant, id, request, ct);
    }

    private static void Validate(RuleRequest req)
    {
        NameValidator.Validate(req.Name);
        if (req.Kind is not ("threshold" or "rate-of-change") || string.IsNullOrWhiteSpace(req.Metric) || req.Metric.Length > 128 || !double.IsFinite(req.Threshold) || req.WindowSeconds is < 1 or > 259200 || req.SuppressionSeconds is < 0 or > 259200)
            throw new ArgumentException("Invalid rule configuration.");
    }
}
