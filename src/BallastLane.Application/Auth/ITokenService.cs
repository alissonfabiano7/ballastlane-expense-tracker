using BallastLane.Domain.Users;

namespace BallastLane.Application.Auth;

public interface ITokenService
{
    GeneratedToken Generate(User user);
}

public readonly record struct GeneratedToken(string Token, DateTime ExpiresAtUtc);
