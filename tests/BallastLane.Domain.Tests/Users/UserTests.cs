using BallastLane.Domain.Common;
using BallastLane.Domain.Users;
using Shouldly;

namespace BallastLane.Domain.Tests.Users;

public class UserTests
{
    private static readonly DateTime FixedUtcNow = new(2026, 5, 8, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Create_assigns_new_id_and_normalizes_email()
    {
        User user = User.Create("  Demo@BallastLane.Test ", "hashed-password", FixedUtcNow);

        user.Id.ShouldNotBe(Guid.Empty);
        user.Email.ShouldBe("demo@ballastlane.test");
        user.PasswordHash.ShouldBe("hashed-password");
        user.CreatedAt.ShouldBe(FixedUtcNow);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-an-email")]
    [InlineData("missing@tld")]
    [InlineData("@no-local.test")]
    [InlineData("two@@signs.test")]
    public void Create_rejects_invalid_email(string invalidEmail)
    {
        Should.Throw<InvalidEmailException>(
            () => User.Create(invalidEmail, "hashed-password", FixedUtcNow));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_rejects_empty_password_hash(string invalidHash)
    {
        Should.Throw<DomainValidationException>(
            () => User.Create("demo@ballastlane.test", invalidHash, FixedUtcNow));
    }

    [Fact]
    public void Hydrate_reconstructs_user_without_validation()
    {
        Guid id = Guid.NewGuid();

        User user = User.Hydrate(id, "ANY@email.com", "hash", FixedUtcNow);

        user.Id.ShouldBe(id);
        user.Email.ShouldBe("ANY@email.com");
        user.PasswordHash.ShouldBe("hash");
        user.CreatedAt.ShouldBe(FixedUtcNow);
    }
}
