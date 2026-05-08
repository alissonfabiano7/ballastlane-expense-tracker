using BallastLane.Application.Auth;
using FluentValidation.Results;
using Shouldly;

namespace BallastLane.Application.Tests.Auth;

public class RegisterUserCommandValidatorTests
{
    private readonly RegisterUserCommandValidator _sut = new();

    [Fact]
    public void Valid_command_passes()
    {
        ValidationResult result = _sut.Validate(new RegisterUserCommand("demo@ballastlane.test", "Demo@123"));

        result.IsValid.ShouldBeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-an-email")]
    public void Invalid_email_fails(string email)
    {
        ValidationResult result = _sut.Validate(new RegisterUserCommand(email, "Demo@123"));

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(RegisterUserCommand.Email));
    }

    [Theory]
    [InlineData("")]
    [InlineData("short1")]
    [InlineData("noDigitsHere")]
    [InlineData("12345678")]
    public void Invalid_password_fails(string password)
    {
        ValidationResult result = _sut.Validate(new RegisterUserCommand("demo@ballastlane.test", password));

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(RegisterUserCommand.Password));
    }
}
