using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using Pulse.Application;

namespace Pulse.Infrastructure.Configuration;

public sealed class EnvironmentSecretProvider(IConfiguration configuration) : ISecretProvider
{
    public string Get(string name) => configuration[$"Secrets:{name}"] is { Length: >= 32 } value ? value : throw new InvalidOperationException($"Secrets:{name} must contain at least 32 characters; configure environment or user-secrets.");
}
