using BallastLane.Application.Common;
using BallastLane.Domain.Expenses;
using FluentValidation;

namespace BallastLane.Application.Expenses;

public sealed class CreateExpenseUseCase(
    IExpenseRepository expenseRepository,
    TimeProvider timeProvider,
    IValidator<CreateExpenseCommand> validator)
{
    public async Task<ExpenseDto> HandleAsync(
        Guid userId,
        CreateExpenseCommand command,
        CancellationToken cancellationToken)
    {
        FluentValidation.Results.ValidationResult validation = await validator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
        {
            throw new Common.ValidationException(
                validation.Errors
                    .GroupBy(e => e.PropertyName)
                    .ToDictionary(
                        g => g.Key,
                        g => g.Select(e => e.ErrorMessage).ToArray() as string[]));
        }

        ExpenseCategory category = Enum.Parse<ExpenseCategory>(command.Category, ignoreCase: true);
        DateTime utcNow = timeProvider.GetUtcNow().UtcDateTime;

        Expense expense = Expense.Create(
            userId: userId,
            amount: command.Amount,
            description: command.Description,
            category: category,
            incurredAt: command.IncurredAt,
            utcNow: utcNow);

        await expenseRepository.AddAsync(expense, cancellationToken);
        return ExpenseDto.FromDomain(expense);
    }
}
