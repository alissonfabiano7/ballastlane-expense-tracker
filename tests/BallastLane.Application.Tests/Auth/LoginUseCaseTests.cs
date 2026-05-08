using BallastLane.Application.Auth;
using BallastLane.Application.Common;
using BallastLane.Application.Users;
using BallastLane.Domain.Users;
using NSubstitute;
using Shouldly;

namespace BallastLane.Application.Tests.Auth;

public class LoginUseCaseTests
{
    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();
    private readonly IPasswordHasher _passwordHasher = Substitute.For<IPasswordHasher>();
    private readonly ITokenService _tokenService = Substitute.For<ITokenService>();
    private readonly LoginUseCase _sut;

    public LoginUseCaseTests()
    {
        _sut = new LoginUseCase(
            _userRepository,
            _passwordHasher,
            _tokenService,
            new LoginCommandValidator());
    }

    private static User SampleUser(string email = "demo@ballastlane.test", string passwordHash = "hashed-password")
        => User.Hydrate(
            id: Guid.NewGuid(),
            email: email,
            passwordHash: passwordHash,
            createdAt: new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc));

    [Fact]
    public async Task HandleAsync_returns_auth_result_for_valid_credentials()
    {
        User user = SampleUser();
        _userRepository
            .GetByEmailAsync("demo@ballastlane.test", Arg.Any<CancellationToken>())
            .Returns(user);
        _passwordHasher.Verify("Demo@123", "hashed-password").Returns(true);
        _tokenService.Generate(user).Returns(
            new GeneratedToken("jwt-token", new DateTime(2026, 5, 8, 13, 0, 0, DateTimeKind.Utc)));

        AuthResult result = await _sut.HandleAsync(
            new LoginCommand("Demo@BallastLane.Test", "Demo@123"),
            CancellationToken.None);

        result.UserId.ShouldBe(user.Id);
        result.Email.ShouldBe(user.Email);
        result.Token.ShouldBe("jwt-token");
    }

    [Fact]
    public async Task HandleAsync_throws_InvalidCredentialsException_for_unknown_email()
    {
        _userRepository
            .GetByEmailAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((User?)null);

        await Should.ThrowAsync<InvalidCredentialsException>(
            () => _sut.HandleAsync(
                new LoginCommand("missing@user.test", "anything"),
                CancellationToken.None));
    }

    [Fact]
    public async Task HandleAsync_throws_InvalidCredentialsException_for_wrong_password()
    {
        User user = SampleUser();
        _userRepository
            .GetByEmailAsync("demo@ballastlane.test", Arg.Any<CancellationToken>())
            .Returns(user);
        _passwordHasher.Verify(Arg.Any<string>(), user.PasswordHash).Returns(false);

        await Should.ThrowAsync<InvalidCredentialsException>(
            () => _sut.HandleAsync(
                new LoginCommand("demo@ballastlane.test", "WrongPassword"),
                CancellationToken.None));
    }

    [Fact]
    public async Task HandleAsync_throws_ValidationException_for_empty_credentials()
    {
        await Should.ThrowAsync<ValidationException>(
            () => _sut.HandleAsync(new LoginCommand("", ""), CancellationToken.None));

        await _userRepository.DidNotReceiveWithAnyArgs().GetByEmailAsync(default!, default);
    }
}
