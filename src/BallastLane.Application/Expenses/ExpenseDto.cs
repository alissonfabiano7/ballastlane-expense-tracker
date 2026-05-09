using BallastLane.Domain.Expenses;

namespace BallastLane.Application.Expenses;

public sealed record ExpenseDto(
    Guid Id,
    decimal Amount,
    string? Description,
    string Category,
    DateTime IncurredAt,
    DateTime CreatedAt)
{
    public static ExpenseDto FromDomain(Expense expense) => new(
        Id: expense.Id,
        Amount: expense.Amount,
        Description: expense.Description,
        Category: expense.Category.ToString(),
        IncurredAt: expense.IncurredAt,
        CreatedAt: expense.CreatedAt);
}
