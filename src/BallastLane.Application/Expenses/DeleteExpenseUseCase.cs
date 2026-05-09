using BallastLane.Application.Common;

namespace BallastLane.Application.Expenses;

public sealed class DeleteExpenseUseCase(IExpenseRepository expenseRepository)
{
    public async Task HandleAsync(
        Guid userId,
        Guid expenseId,
        CancellationToken cancellationToken)
    {
        bool deleted = await expenseRepository.DeleteAsync(expenseId, userId, cancellationToken);
        if (!deleted)
        {
            throw new NotFoundException("Expense", expenseId);
        }
    }
}
