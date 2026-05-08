using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using BallastLane.Application.Auth;
using BallastLane.Domain.Users;
using BallastLane.Infrastructure.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace BallastLane.Infrastructure.Security;

public sealed class JwtTokenService(IOptions<JwtSettings> options, TimeProvider timeProvider) : ITokenService
{
    private readonly JwtSettings _settings = options.Value;
    private readonly TimeProvider _timeProvider = timeProvider;

    public GeneratedToken Generate(User user)
    {
        DateTime utcNow = _timeProvider.GetUtcNow().UtcDateTime;
        DateTime expiresAt = utcNow.AddMinutes(_settings.ExpirationMinutes);

        Claim[] claims =
        [
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new(JwtRegisteredClaimNames.Iat,
                new DateTimeOffset(utcNow).ToUnixTimeSeconds().ToString(System.Globalization.CultureInfo.InvariantCulture),
                ClaimValueTypes.Integer64),
        ];

        SymmetricSecurityKey signingKey = new(Encoding.UTF8.GetBytes(_settings.Secret));
        SigningCredentials credentials = new(signingKey, SecurityAlgorithms.HmacSha256);

        JwtSecurityToken token = new(
            issuer: _settings.Issuer,
            audience: _settings.Audience,
            claims: claims,
            notBefore: utcNow,
            expires: expiresAt,
            signingCredentials: credentials);

        string serialized = new JwtSecurityTokenHandler().WriteToken(token);
        return new GeneratedToken(serialized, expiresAt);
    }
}
