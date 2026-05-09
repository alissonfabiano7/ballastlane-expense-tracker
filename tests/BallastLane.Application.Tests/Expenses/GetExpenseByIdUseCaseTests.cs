using BallastLane.Application.Common;
using BallastLane.Application.Expenses;
using BallastLane.Domain.Expenses;
using NSubstitute;
using Shouldly;

namespace BallastLane.Application.Tests.Expenses;

public class GetExpenseByIdUseCaseTests
{
    private static readonly Guid OwnerId = new("3f2504e0-4f89-11d3-9a0c-0305e82c3301");
    private static readonly DateTime FixedUtcNow = new(2026, 5, 8, 12, 0, 0, DateTimeKind.Utc);

    private readonly IExpenseRepository _repository = Substitute.For<IExpenseRepository>();
    private readonly GetExpenseByIdUseCase _sut;

    public GetExpenseByIdUseCaseTests()
    {
        _sut = new GetExpenseByIdUseCase(_repository);
    }

    [Fact]
    public async Task HandleAsync_returns_dto_when_expense_belongs_to_user()
    {
        Guid expenseId = Guid.NewGuid();
        Expense expense = Expense.Hydrate(
            id: expenseId,
            userId: OwnerId,
            amount: 42m,
            description: "lunch",
            category: ExpenseCategory.Food,
            incurredAt: FixedUtcNow,
            createdAt: FixedUtcNow);
        _repository.GetByIdAsync(expenseId, OwnerId, Arg.Any<CancellationToken>())
            .Returns(expense);

        ExpenseDto dto = await _sut.HandleAsync(OwnerId, expenseId, CancellationToken.None);

        dto.Id.ShouldBe(expenseId);
        dto.Amount.ShouldBe(42m);
        dto.Category.ShouldBe("Food");
    }

    [Fact]
    public async Task HandleAsync_throws_NotFoundException_when_expense_does_not_belong_to_user()
    {
        Guid expenseId = Guid.NewGuid();
        _repository.GetByIdAsync(expenseId, OwnerId, Arg.Any<CancellationToken>())
            .Returns((Expense?)null);

        NotFoundException exception = await Should.ThrowAsync<NotFoundException>(
            () => _sut.HandleAsync(OwnerId, expenseId, CancellationToken.None));

        exception.Resource.ShouldBe("Expense");
        exception.Key.ShouldBe(expenseId);
    }
}
