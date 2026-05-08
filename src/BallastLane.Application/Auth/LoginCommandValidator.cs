using FluentValidation;

namespace BallastLane.Application.Auth;

public sealed class LoginCommandValidator : AbstractValidator<LoginCommand>
{
    public LoginCommandValidator()
    {
        RuleFor(c => c.Email)
            .NotEmpty().WithMessage("Email is required.");

        RuleFor(c => c.Password)
            .NotEmpty().WithMessage("Password is required.");
    }
}
