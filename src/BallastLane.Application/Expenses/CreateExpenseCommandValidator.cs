using BallastLane.Domain.Expenses;
using FluentValidation;

namespace BallastLane.Application.Expenses;

public sealed class CreateExpenseCommandValidator : AbstractValidator<CreateExpenseCommand>
{
    public CreateExpenseCommandValidator()
    {
        RuleFor(c => c.Amount)
            .GreaterThan(0m).WithMessage("Amount must be greater than zero.");

        RuleFor(c => c.Description)
            .MaximumLength(Expense.DescriptionMaxLength)
                .WithMessage($"Description must be at most {Expense.DescriptionMaxLength} characters.")
            .When(c => c.Description is not null);

        RuleFor(c => c.Category)
            .NotEmpty().WithMessage("Category is required.")
            .Must(IsKnownCategoryName)
                .WithMessage(c => $"Category '{c.Category}' is not recognized. Allowed values: " +
                    string.Join(", ", Enum.GetNames<ExpenseCategory>()) + ".");

        RuleFor(c => c.IncurredAt)
            .NotEqual(default(DateTime)).WithMessage("IncurredAt is required.");
    }

    internal static bool IsKnownCategoryName(string? value)
        => value is not null && Enum.GetNames<ExpenseCategory>()
            .Any(name => string.Equals(name, value, StringComparison.OrdinalIgnoreCase));
}
