namespace Pulse.Application.Models.Requests;

public sealed record UserRequest(Guid? TenantId, string Username, string Password, string Role);
