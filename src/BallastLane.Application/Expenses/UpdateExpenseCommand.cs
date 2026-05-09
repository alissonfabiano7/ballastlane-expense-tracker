namespace BallastLane.Application.Expenses;

public sealed record UpdateExpenseCommand(
    decimal Amount,
    string? Description,
    string Category,
    DateTime IncurredAt);
