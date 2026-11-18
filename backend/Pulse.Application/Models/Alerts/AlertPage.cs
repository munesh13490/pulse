namespace Pulse.Application.Models.Alerts;

public sealed record AlertPage(List<Dictionary<string, object?>> Items, AlertCursor? Next);
