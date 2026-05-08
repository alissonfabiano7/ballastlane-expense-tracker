using BallastLane.Application.Common;

namespace BallastLane.Application.Auth;

public sealed class UserAlreadyExistsException(string email)
    : ConflictException($"A user with the email '{email}' already exists.")
{
    public string Email { get; } = email;
}
