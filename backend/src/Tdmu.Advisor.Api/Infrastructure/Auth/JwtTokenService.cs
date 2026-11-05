using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Tdmu.Advisor.Api.Infrastructure.Auth;

public class JwtTokenService(IOptions<AuthOptions> options)
{
    public (string Token, DateTimeOffset ExpiresAt) Create(IdentityUser user, IEnumerable<string> roles)
    {
        var o = options.Value;
        var expires = DateTimeOffset.UtcNow.AddMinutes(o.AccessTokenMinutes);
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id),
            new(JwtRegisteredClaimNames.Email, user.Email ?? ""),
            new(ClaimTypes.NameIdentifier, user.Id),
        };
        claims.AddRange(roles.Select(r => new Claim(ClaimTypes.Role, r)));
        var creds = new SigningCredentials(SigningKey(o.JwtKey), SecurityAlgorithms.HmacSha256);
        var jwt = new JwtSecurityToken(o.Issuer, o.Audience, claims, expires: expires.UtcDateTime, signingCredentials: creds);
        return (new JwtSecurityTokenHandler().WriteToken(jwt), expires);
    }

    public static SymmetricSecurityKey SigningKey(string key) => new(Encoding.UTF8.GetBytes(key));
}
