using BallastLane.Infrastructure.Security;
using Shouldly;

namespace BallastLane.Infrastructure.Tests.Security;

public class Argon2idPasswordHasherTests
{
    private readonly Argon2idPasswordHasher _sut = new();

    [Fact]
    public void Hash_then_verify_roundtrip_succeeds()
    {
        string hash = _sut.Hash("Demo@123");

        _sut.Verify("Demo@123", hash).ShouldBeTrue();
    }

    [Fact]
    public void Verify_returns_false_for_wrong_password()
    {
        string hash = _sut.Hash("Demo@123");

        _sut.Verify("Wrong@123", hash).ShouldBeFalse();
    }

    [Fact]
    public void Hash_produces_different_output_for_same_password_due_to_random_salt()
    {
        string hash1 = _sut.Hash("Demo@123");
        string hash2 = _sut.Hash("Demo@123");

        hash1.ShouldNotBe(hash2);
        _sut.Verify("Demo@123", hash1).ShouldBeTrue();
        _sut.Verify("Demo@123", hash2).ShouldBeTrue();
    }

    [Fact]
    public void Hash_includes_owasp_parameter_block()
    {
        string hash = _sut.Hash("Demo@123");

        hash.ShouldStartWith("argon2id$m=19456$t=2$p=1$");
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-valid-hash")]
    [InlineData("argon2id$m=19456$t=2$p=1$invalid-base64")]
    public void Verify_returns_false_for_malformed_hash(string malformedHash)
    {
        _sut.Verify("any-password", malformedHash).ShouldBeFalse();
    }

    [Fact]
    public void Verify_known_seed_hash_succeeds()
    {
        // Mirrors the literal in db/scripts/up/0002_seed.sql.
        // If the hashing format ever changes, this test catches the seed
        // drifting before the demo user fails to log in.
        const string SeedHash =
            "argon2id$m=19456$t=2$p=1$/bbweUenC6ZPycMfvsiSLw==$jUvhKxaxhXbTcD1CHOCT7fDbXLtt2wM/8rWdfE632/Y=";

        _sut.Verify("Demo@123", SeedHash).ShouldBeTrue();
    }
}
