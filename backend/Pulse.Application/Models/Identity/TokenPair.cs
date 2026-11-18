using Pulse.Domain;

namespace Pulse.Application.Models.Identity;

public record TokenPair(string AccessToken, string RefreshToken, DateTimeOffset ExpiresAt);
