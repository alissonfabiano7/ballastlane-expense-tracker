using BallastLane.Application.Common;
using BallastLane.Application.Users;
using BallastLane.Domain.Users;
using FluentValidation;

namespace BallastLane.Application.Auth;

public sealed class LoginUseCase(
    IUserRepository userRepository,
    IPasswordHasher passwordHasher,
    ITokenService tokenService,
    IValidator<LoginCommand> validator)
{
    public async Task<AuthResult> HandleAsync(LoginCommand command, CancellationToken cancellationToken)
    {
        FluentValidation.Results.ValidationResult validation = await validator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
        {
            throw new Common.ValidationException(
                validation.Errors
                    .GroupBy(e => e.PropertyName)
                    .ToDictionary(
                        g => g.Key,
                        g => g.Select(e => e.ErrorMessage).ToArray() as string[]));
        }

        string normalizedEmail = command.Email.Trim().ToLowerInvariant();
        User? user = await userRepository.GetByEmailAsync(normalizedEmail, cancellationToken);
        if (user is null)
        {
            throw new InvalidCredentialsException();
        }

        if (!passwordHasher.Verify(command.Password, user.PasswordHash))
        {
            throw new InvalidCredentialsException();
        }

        GeneratedToken token = tokenService.Generate(user);
        return new AuthResult(user.Id, user.Email, token.Token, token.ExpiresAtUtc);
    }
}
