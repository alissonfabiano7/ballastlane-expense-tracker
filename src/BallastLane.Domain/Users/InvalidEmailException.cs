using BallastLane.Domain.Common;

namespace BallastLane.Domain.Users;

public sealed class InvalidEmailException : DomainValidationException
{
    public InvalidEmailException(string email)
        : base($"'{email}' is not a valid email address.") { }
}
