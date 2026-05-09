using BallastLane.Application.Expenses;
using FluentValidation.Results;
using Shouldly;

namespace BallastLane.Application.Tests.Expenses;

public class UpdateExpenseCommandValidatorTests
{
    private static readonly DateTime FixedUtcNow = new(2026, 5, 8, 12, 0, 0, DateTimeKind.Utc);

    private readonly UpdateExpenseCommandValidator _sut = new();

    private static UpdateExpenseCommand Valid(
        decimal? amount = null,
        string? description = "Coffee",
        string? category = "Food",
        DateTime? incurredAt = null) => new(
            Amount: amount ?? 10m,
            Description: description,
            Category: category ?? "Food",
            IncurredAt: incurredAt ?? FixedUtcNow);

    [Fact]
    public void Valid_command_passes()
    {
        ValidationResult result = _sut.Validate(Valid());

        result.IsValid.ShouldBeTrue();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-0.01)]
    public void Non_positive_amount_fails(decimal amount)
    {
        ValidationResult result = _sut.Validate(Valid(amount: amount));

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(UpdateExpenseCommand.Amount));
    }

    [Fact]
    public void Description_exceeding_500_chars_fails()
    {
        string overlong = new('x', 501);

        ValidationResult result = _sut.Validate(Valid(description: overlong));

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(UpdateExpenseCommand.Description));
    }

    [Theory]
    [InlineData("")]
    [InlineData("Yacht")]
    public void Invalid_category_fails(string category)
    {
        ValidationResult result = _sut.Validate(Valid(category: category));

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(UpdateExpenseCommand.Category));
    }

    [Fact]
    public void Default_incurred_at_fails()
    {
        ValidationResult result = _sut.Validate(Valid(incurredAt: default(DateTime)));

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(UpdateExpenseCommand.IncurredAt));
    }
}
