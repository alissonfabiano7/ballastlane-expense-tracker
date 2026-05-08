using BallastLane.Domain.Common;

namespace BallastLane.Domain.Expenses;

public sealed class Expense
{
    public const int DescriptionMaxLength = 500;

    public Guid Id { get; }
    public Guid UserId { get; }
    public decimal Amount { get; private set; }
    public string? Description { get; private set; }
    public ExpenseCategory Category { get; private set; }
    public DateTime IncurredAt { get; private set; }
    public DateTime CreatedAt { get; }

    private Expense(
        Guid id,
        Guid userId,
        decimal amount,
        string? description,
        ExpenseCategory category,
        DateTime incurredAt,
        DateTime createdAt)
    {
        Id = id;
        UserId = userId;
        Amount = amount;
        Description = description;
        Category = category;
        IncurredAt = incurredAt;
        CreatedAt = createdAt;
    }

    public static Expense Create(
        Guid userId,
        decimal amount,
        string? description,
        ExpenseCategory category,
        DateTime incurredAt,
        DateTime utcNow)
    {
        if (userId == Guid.Empty)
        {
            throw new DomainValidationException("UserId is required.");
        }
        EnsureValidAmount(amount);
        EnsureValidDescription(description);
        EnsureValidIncurredAt(incurredAt, utcNow);

        return new Expense(
            id: Guid.NewGuid(),
            userId: userId,
            amount: amount,
            description: description,
            category: category,
            incurredAt: incurredAt,
            createdAt: utcNow);
    }

    public void Update(
        decimal amount,
        string? description,
        ExpenseCategory category,
        DateTime incurredAt,
        DateTime utcNow)
    {
        EnsureValidAmount(amount);
        EnsureValidDescription(description);
        EnsureValidIncurredAt(incurredAt, utcNow);

        Amount = amount;
        Description = description;
        Category = category;
        IncurredAt = incurredAt;
    }

    public static Expense Hydrate(
        Guid id,
        Guid userId,
        decimal amount,
        string? description,
        ExpenseCategory category,
        DateTime incurredAt,
        DateTime createdAt)
        => new(id, userId, amount, description, category, incurredAt, createdAt);

    private static void EnsureValidAmount(decimal amount)
    {
        if (amount <= 0m)
        {
            throw new DomainValidationException("Amount must be positive.");
        }
    }

    private static void EnsureValidDescription(string? description)
    {
        if (description is { Length: > DescriptionMaxLength })
        {
            throw new DomainValidationException(
                $"Description cannot exceed {DescriptionMaxLength} characters.");
        }
    }

    private static void EnsureValidIncurredAt(DateTime incurredAt, DateTime utcNow)
    {
        if (incurredAt > utcNow)
        {
            throw new DomainValidationException("IncurredAt cannot be in the future.");
        }
    }
}
