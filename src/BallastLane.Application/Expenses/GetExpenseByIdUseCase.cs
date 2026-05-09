using BallastLane.Application.Common;
using BallastLane.Domain.Expenses;

namespace BallastLane.Application.Expenses;

public sealed class GetExpenseByIdUseCase(IExpenseRepository expenseRepository)
{
    public async Task<ExpenseDto> HandleAsync(
        Guid userId,
        Guid expenseId,
        CancellationToken cancellationToken)
    {
        Expense? expense = await expenseRepository.GetByIdAsync(expenseId, userId, cancellationToken);
        if (expense is null)
        {
            throw new NotFoundException("Expense", expenseId);
        }

        return ExpenseDto.FromDomain(expense);
    }
}
