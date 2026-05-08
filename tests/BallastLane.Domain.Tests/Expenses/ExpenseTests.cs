using BallastLane.Domain.Common;
using BallastLane.Domain.Expenses;
using Shouldly;

namespace BallastLane.Domain.Tests.Expenses;

public class ExpenseTests
{
    private static readonly DateTime FixedUtcNow = new(2026, 5, 8, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid OwnerId = new("3f2504e0-4f89-11d3-9a0c-0305e82c3301");

    [Fact]
    public void Create_assigns_new_id_and_persists_fields()
    {
        Expense expense = Expense.Create(
            userId: OwnerId,
            amount: 12.34m,
            description: "Coffee",
            category: ExpenseCategory.Food,
            incurredAt: FixedUtcNow.AddHours(-2),
            utcNow: FixedUtcNow);

        expense.Id.ShouldNotBe(Guid.Empty);
        expense.UserId.ShouldBe(OwnerId);
        expense.Amount.ShouldBe(12.34m);
        expense.Description.ShouldBe("Coffee");
        expense.Category.ShouldBe(ExpenseCategory.Food);
        expense.IncurredAt.ShouldBe(FixedUtcNow.AddHours(-2));
        expense.CreatedAt.ShouldBe(FixedUtcNow);
    }

    [Theory]
    [InlineData(-0.01)]
    [InlineData(0)]
    public void Create_rejects_non_positive_amount(decimal invalidAmount)
    {
        Should.Throw<DomainValidationException>(() => Expense.Create(
            userId: OwnerId,
            amount: invalidAmount,
            description: null,
            category: ExpenseCategory.Other,
            incurredAt: FixedUtcNow,
            utcNow: FixedUtcNow));
    }

    [Fact]
    public void Create_rejects_empty_user_id()
    {
        Should.Throw<DomainValidationException>(() => Expense.Create(
            userId: Guid.Empty,
            amount: 10m,
            description: null,
            category: ExpenseCategory.Other,
            incurredAt: FixedUtcNow,
            utcNow: FixedUtcNow));
    }

    [Fact]
    public void Create_rejects_future_incurred_date()
    {
        Should.Throw<DomainValidationException>(() => Expense.Create(
            userId: OwnerId,
            amount: 10m,
            description: null,
            category: ExpenseCategory.Other,
            incurredAt: FixedUtcNow.AddDays(1),
            utcNow: FixedUtcNow));
    }

    [Fact]
    public void Create_rejects_description_exceeding_max_length()
    {
        string overlong = new string('x', 501);

        Should.Throw<DomainValidationException>(() => Expense.Create(
            userId: OwnerId,
            amount: 10m,
            description: overlong,
            category: ExpenseCategory.Other,
            incurredAt: FixedUtcNow,
            utcNow: FixedUtcNow));
    }

    [Fact]
    public void Update_revalidates_invariants()
    {
        Expense expense = Expense.Create(
            userId: OwnerId,
            amount: 10m,
            description: "old",
            category: ExpenseCategory.Food,
            incurredAt: FixedUtcNow,
            utcNow: FixedUtcNow);

        expense.Update(
            amount: 25.50m,
            description: "new",
            category: ExpenseCategory.Transport,
            incurredAt: FixedUtcNow.AddHours(-1),
            utcNow: FixedUtcNow);

        expense.Amount.ShouldBe(25.50m);
        expense.Description.ShouldBe("new");
        expense.Category.ShouldBe(ExpenseCategory.Transport);
        expense.IncurredAt.ShouldBe(FixedUtcNow.AddHours(-1));

        Should.Throw<DomainValidationException>(() => expense.Update(
            amount: 0m,
            description: "new",
            category: ExpenseCategory.Transport,
            incurredAt: FixedUtcNow.AddHours(-1),
            utcNow: FixedUtcNow));
    }

    [Fact]
    public void Hydrate_reconstructs_expense_without_validation()
    {
        Guid id = Guid.NewGuid();

        Expense expense = Expense.Hydrate(
            id: id,
            userId: OwnerId,
            amount: -99m,
            description: null,
            category: ExpenseCategory.Other,
            incurredAt: FixedUtcNow,
            createdAt: FixedUtcNow);

        expense.Id.ShouldBe(id);
        expense.Amount.ShouldBe(-99m);
    }
}
