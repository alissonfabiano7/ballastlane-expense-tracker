using System.Security.Cryptography;
using System.Text;
using BallastLane.Application.Auth;
using Konscious.Security.Cryptography;

namespace BallastLane.Infrastructure.Security;

public sealed class Argon2idPasswordHasher : IPasswordHasher
{
    // OWASP-recommended Argon2id parameters (2024 minimums; tuned to ~250ms on target hardware).
    private const int DegreeOfParallelism = 1;
    private const int MemorySizeKb = 19456;
    private const int Iterations = 2;
    private const int SaltSize = 16;
    private const int HashSize = 32;
    private const string Identifier = "argon2id";

    public string Hash(string plainPassword)
    {
        ArgumentException.ThrowIfNullOrEmpty(plainPassword);

        byte[] salt = RandomNumberGenerator.GetBytes(SaltSize);
        byte[] hash = ComputeHash(plainPassword, salt);

        return string.Create(
            System.Globalization.CultureInfo.InvariantCulture,
            $"{Identifier}$m={MemorySizeKb}$t={Iterations}$p={DegreeOfParallelism}${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}");
    }

    public bool Verify(string plainPassword, string storedHash)
    {
        if (string.IsNullOrEmpty(plainPassword) || string.IsNullOrEmpty(storedHash))
        {
            return false;
        }

        string[] parts = storedHash.Split('$');
        if (parts.Length != 6 || parts[0] != Identifier)
        {
            return false;
        }

        byte[] salt;
        byte[] expectedHash;
        try
        {
            salt = Convert.FromBase64String(parts[4]);
            expectedHash = Convert.FromBase64String(parts[5]);
        }
        catch (FormatException)
        {
            return false;
        }

        byte[] computedHash = ComputeHash(plainPassword, salt);
        return CryptographicOperations.FixedTimeEquals(computedHash, expectedHash);
    }

    private static byte[] ComputeHash(string plainPassword, byte[] salt)
    {
        using Argon2id argon = new(Encoding.UTF8.GetBytes(plainPassword))
        {
            Salt = salt,
            DegreeOfParallelism = DegreeOfParallelism,
            MemorySize = MemorySizeKb,
            Iterations = Iterations,
        };
        return argon.GetBytes(HashSize);
    }
}
