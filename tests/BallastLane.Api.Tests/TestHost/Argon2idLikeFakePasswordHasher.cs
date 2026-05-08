using BallastLane.Application.Auth;

namespace BallastLane.Api.Tests.TestHost;

/// <summary>
/// Test-only password hasher: prefixes plain password with a marker so verify
/// is fast and deterministic. Real Argon2id is unit-tested separately in
/// BallastLane.Infrastructure.Tests.
/// </summary>
public sealed class Argon2idLikeFakePasswordHasher : IPasswordHasher
{
    private const string Prefix = "fake-argon2id$";

    public string Hash(string plainPassword) => Prefix + plainPassword;

    public bool Verify(string plainPassword, string hash) => hash == Prefix + plainPassword;
}
