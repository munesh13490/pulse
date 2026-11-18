using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using Pulse.Application;

namespace Pulse.Infrastructure.Identity;

public sealed class JwtTokenIssuer(ISecretProvider secrets) : ITokenIssuer
{
    public string Issue(UserIdentity user)
    {
        var claims = new List<Claim>
        {
            new("sub", user.Id.ToString()),
            new("name", user.Name)
        };
        if (user.TenantId is { } tenant)
            claims.Add(new("tenant_id", tenant.ToString()));
        claims.AddRange(user.Roles.Select(r => new Claim("role", r)));
        return new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken("pulse-local", "pulse-dashboard", claims, DateTime.UtcNow, DateTime.UtcNow.AddMinutes(15), new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secrets.Get("JwtSigningKey"))), SecurityAlgorithms.HmacSha256)));
    }
}
