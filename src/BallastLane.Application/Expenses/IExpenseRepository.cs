using BallastLane.Application.Common;
using BallastLane.Domain.Expenses;

namespace BallastLane.Application.Expenses;

public interface IExpenseRepository
{
    Task AddAsync(Expense expense, CancellationToken cancellationToken);

    Task<Expense?> GetByIdAsync(Guid id, Guid userId, CancellationToken cancellationToken);

    Task<PagedResult<Expense>> ListByUserAsync(
        Guid userId,
        int page,
        int pageSize,
        CancellationToken cancellationToken);

    Task UpdateAsync(Expense expense, CancellationToken cancellationToken);

    Task<bool> DeleteAsync(Guid id, Guid userId, CancellationToken cancellationToken);
}
