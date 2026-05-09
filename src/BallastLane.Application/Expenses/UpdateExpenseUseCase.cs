using BallastLane.Application.Common;
using BallastLane.Domain.Expenses;
using FluentValidation;

namespace BallastLane.Application.Expenses;

public sealed class UpdateExpenseUseCase(
    IExpenseRepository expenseRepository,
    TimeProvider timeProvider,
    IValidator<UpdateExpenseCommand> validator)
{
    public async Task<ExpenseDto> HandleAsync(
        Guid userId,
        Guid expenseId,
        UpdateExpenseCommand command,
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

        Expense? expense = await expenseRepository.GetByIdAsync(expenseId, userId, cancellationToken);
        if (expense is null)
        {
            throw new NotFoundException("Expense", expenseId);
        }

        ExpenseCategory category = Enum.Parse<ExpenseCategory>(command.Category, ignoreCase: true);
        DateTime utcNow = timeProvider.GetUtcNow().UtcDateTime;

        expense.Update(
            amount: command.Amount,
            description: command.Description,
            category: category,
            incurredAt: command.IncurredAt,
            utcNow: utcNow);

        await expenseRepository.UpdateAsync(expense, cancellationToken);
        return ExpenseDto.FromDomain(expense);
    }
}
