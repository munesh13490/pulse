namespace Pulse.Application.Models.Requests;

public sealed record RuleRequest(Guid DeviceId, string Name, string Kind, string Metric, double Threshold, int WindowSeconds, int SuppressionSeconds);
