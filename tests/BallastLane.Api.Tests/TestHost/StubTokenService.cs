using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using BallastLane.Application.Auth;
using BallastLane.Domain.Users;
using Microsoft.IdentityModel.Tokens;

namespace BallastLane.Api.Tests.TestHost;

/// <summary>
/// Issues JWTs aligned with the appsettings.Development.json values that
/// Program.cs reads at startup, guaranteeing JwtBearer accepts them in tests.
/// </summary>
public sealed class StubTokenService : ITokenService
{
    public const string Issuer = "BallastLane.Api";
    public const string Audience = "BallastLane.Client";
    public const string Secret = "dev-only-jwt-secret-do-not-use-in-prod-XYZ123-padding-padding-padding";

    public GeneratedToken Generate(User user)
    {
        DateTime utcNow = DateTime.UtcNow;
        DateTime expiresAt = utcNow.AddMinutes(60);

        Claim[] claims =
        [
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email),
        ];

        SymmetricSecurityKey key = new(Encoding.UTF8.GetBytes(Secret));
        SigningCredentials credentials = new(key, SecurityAlgorithms.HmacSha256);

        JwtSecurityToken token = new(
            issuer: Issuer,
            audience: Audience,
            claims: claims,
            notBefore: utcNow,
            expires: expiresAt,
            signingCredentials: credentials);

        string serialized = new JwtSecurityTokenHandler().WriteToken(token);
        return new GeneratedToken(serialized, expiresAt);
    }
}
