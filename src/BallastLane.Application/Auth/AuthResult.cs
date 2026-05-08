namespace BallastLane.Application.Auth;

public sealed record AuthResult(
    Guid UserId,
    string Email,
    string Token,
    DateTime ExpiresAtUtc);
