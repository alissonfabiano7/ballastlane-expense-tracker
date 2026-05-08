using System.Text.RegularExpressions;
using BallastLane.Domain.Common;

namespace BallastLane.Domain.Users;

public sealed partial class User
{
    public Guid Id { get; }
    public string Email { get; }
    public string PasswordHash { get; }
    public DateTime CreatedAt { get; }

    private User(Guid id, string email, string passwordHash, DateTime createdAt)
    {
        Id = id;
        Email = email;
        PasswordHash = passwordHash;
        CreatedAt = createdAt;
    }

    public static User Create(string email, string passwordHash, DateTime utcNow)
    {
        string normalizedEmail = NormalizeEmail(email);

        if (string.IsNullOrWhiteSpace(passwordHash))
        {
            throw new DomainValidationException("Password hash cannot be empty.");
        }

        return new User(Guid.NewGuid(), normalizedEmail, passwordHash, utcNow);
    }

    public static User Hydrate(Guid id, string email, string passwordHash, DateTime createdAt)
        => new(id, email, passwordHash, createdAt);

    private static string NormalizeEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            throw new InvalidEmailException(email ?? string.Empty);
        }

        string trimmed = email.Trim();
        if (!EmailRegex().IsMatch(trimmed))
        {
            throw new InvalidEmailException(trimmed);
        }

        return trimmed.ToLowerInvariant();
    }

    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.CultureInvariant)]
    private static partial Regex EmailRegex();
}
