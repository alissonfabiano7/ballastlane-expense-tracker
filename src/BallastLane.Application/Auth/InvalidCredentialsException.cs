using BallastLane.Application.Common;

namespace BallastLane.Application.Auth;

public sealed class InvalidCredentialsException()
    : AppException("Invalid email or password.");
