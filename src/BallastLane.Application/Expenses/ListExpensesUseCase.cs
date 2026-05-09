using BallastLane.Application.Common;
using BallastLane.Domain.Expenses;

namespace BallastLane.Application.Expenses;

public sealed class ListExpensesUseCase(IExpenseRepository expenseRepository)
{
    public const int MaxPageSize = 100;
    public const int DefaultPageSize = 20;

    public async Task<PagedResult<ExpenseDto>> HandleAsync(
        Guid userId,
        ListExpensesQuery query,
        CancellationToken cancellationToken)
    {
        int page = query.Page < 1 ? 1 : query.Page;
        int pageSize = query.PageSize switch
        {
            < 1 => DefaultPageSize,
            > MaxPageSize => MaxPageSize,
            _ => query.PageSize,
        };

        PagedResult<Expense> page1 = await expenseRepository.ListByUserAsync(
            userId, page, pageSize, cancellationToken);

        return new PagedResult<ExpenseDto>(
            Items: page1.Items.Select(ExpenseDto.FromDomain).ToList(),
            Page: page1.Page,
            PageSize: page1.PageSize,
            TotalCount: page1.TotalCount);
    }
}
