namespace Pulse.Application.Models.Requests;

public sealed record LimitsRequest(int RequestsPerMinute, int EventsPerMinute);
