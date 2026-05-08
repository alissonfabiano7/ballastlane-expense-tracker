using BallastLane.Application.Common;
using BallastLane.Application.Users;
using BallastLane.Domain.Users;
using FluentValidation;

namespace BallastLane.Application.Auth;

public sealed class RegisterUserUseCase(
    IUserRepository userRepository,
    IPasswordHasher passwordHasher,
    ITokenService tokenService,
    TimeProvider timeProvider,
    IValidator<RegisterUserCommand> validator)
{
    public async Task<AuthResult> HandleAsync(RegisterUserCommand command, CancellationToken cancellationToken)
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

        if (await userRepository.EmailExistsAsync(normalizedEmail, cancellationToken))
        {
            throw new UserAlreadyExistsException(normalizedEmail);
        }

        DateTime utcNow = timeProvider.GetUtcNow().UtcDateTime;
        string passwordHash = passwordHasher.Hash(command.Password);

        User user = User.Create(command.Email, passwordHash, utcNow);
        await userRepository.AddAsync(user, cancellationToken);

        GeneratedToken token = tokenService.Generate(user);
        return new AuthResult(user.Id, user.Email, token.Token, token.ExpiresAtUtc);
    }
}
