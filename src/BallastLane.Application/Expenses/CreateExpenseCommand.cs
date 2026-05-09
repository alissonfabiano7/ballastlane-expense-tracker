namespace BallastLane.Application.Expenses;

public sealed record CreateExpenseCommand(
    decimal Amount,
    string? Description,
    string Category,
    DateTime IncurredAt);
