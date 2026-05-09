namespace BallastLane.Application.Expenses;

public sealed record ListExpensesQuery(int Page = 1, int PageSize = 20);
