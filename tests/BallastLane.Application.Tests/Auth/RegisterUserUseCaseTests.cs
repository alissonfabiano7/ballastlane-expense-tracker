using BallastLane.Application.Auth;
using BallastLane.Application.Common;
using BallastLane.Application.Users;
using BallastLane.Domain.Users;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Shouldly;

namespace BallastLane.Application.Tests.Auth;

public class RegisterUserUseCaseTests
{
    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();
    private readonly IPasswordHasher _passwordHasher = Substitute.For<IPasswordHasher>();
    private readonly ITokenService _tokenService = Substitute.For<ITokenService>();
    private readonly FakeTimeProvider _timeProvider = new();
    private readonly RegisterUserUseCase _sut;

    public RegisterUserUseCaseTests()
    {
        _timeProvider.SetUtcNow(new DateTimeOffset(2026, 5, 8, 12, 0, 0, TimeSpan.Zero));
        _sut = new RegisterUserUseCase(
            _userRepository,
            _passwordHasher,
            _tokenService,
            _timeProvider,
            new RegisterUserCommandValidator());
    }

    [Fact]
    public async Task HandleAsync_creates_user_and_returns_auth_result()
    {
        RegisterUserCommand command = new("New@User.Test", "Strong1Password");
        _userRepository
            .EmailExistsAsync("new@user.test", Arg.Any<CancellationToken>())
            .Returns(false);
        _passwordHasher.Hash("Strong1Password").Returns("hashed");
        _tokenService.Generate(Arg.Any<User>())
            .Returns(new GeneratedToken("jwt-token", new DateTime(2026, 5, 8, 13, 0, 0, DateTimeKind.Utc)));

        AuthResult result = await _sut.HandleAsync(command, CancellationToken.None);

        result.UserId.ShouldNotBe(Guid.Empty);
        result.Email.ShouldBe("new@user.test");
        result.Token.ShouldBe("jwt-token");
        result.ExpiresAtUtc.ShouldBe(new DateTime(2026, 5, 8, 13, 0, 0, DateTimeKind.Utc));
        await _userRepository.Received(1).AddAsync(
            Arg.Is<User>(u => u.Email == "new@user.test" && u.PasswordHash == "hashed"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_throws_UserAlreadyExistsException_when_email_already_registered()
    {
        RegisterUserCommand command = new("existing@user.test", "Strong1Password");
        _userRepository
            .EmailExistsAsync("existing@user.test", Arg.Any<CancellationToken>())
            .Returns(true);

        UserAlreadyExistsException exception =
            await Should.ThrowAsync<UserAlreadyExistsException>(
                () => _sut.HandleAsync(command, CancellationToken.None));

        exception.Email.ShouldBe("existing@user.test");
        await _userRepository.DidNotReceive().AddAsync(Arg.Any<User>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_throws_ValidationException_for_invalid_input()
    {
        RegisterUserCommand command = new("not-an-email", "weak");

        ValidationException exception = await Should.ThrowAsync<ValidationException>(
            () => _sut.HandleAsync(command, CancellationToken.None));

        exception.Errors.ShouldContainKey(nameof(RegisterUserCommand.Email));
        exception.Errors.ShouldContainKey(nameof(RegisterUserCommand.Password));
    }

    [Fact]
    public async Task HandleAsync_does_not_call_repository_when_validation_fails()
    {
        RegisterUserCommand command = new("", "");

        await Should.ThrowAsync<ValidationException>(
            () => _sut.HandleAsync(command, CancellationToken.None));

        await _userRepository.DidNotReceiveWithAnyArgs().EmailExistsAsync(default!, default);
        await _userRepository.DidNotReceiveWithAnyArgs().AddAsync(default!, default);
    }
}
