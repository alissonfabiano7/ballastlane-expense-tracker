using BallastLane.Application.Common;
using BallastLane.Application.Expenses;
using BallastLane.Domain.Expenses;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Shouldly;

namespace BallastLane.Application.Tests.Expenses;

public class UpdateExpenseUseCaseTests
{
    private static readonly Guid OwnerId = new("3f2504e0-4f89-11d3-9a0c-0305e82c3301");
    private static readonly Guid OtherUserId = new("a8b6c0d1-2222-3333-4444-555555555555");
    private static readonly DateTimeOffset FixedNow = new(2026, 5, 8, 12, 0, 0, TimeSpan.Zero);

    private readonly IExpenseRepository _repository = Substitute.For<IExpenseRepository>();
    private readonly FakeTimeProvider _timeProvider = new();
    private readonly UpdateExpenseUseCase _sut;

    public UpdateExpenseUseCaseTests()
    {
        _timeProvider.SetUtcNow(FixedNow);
        _sut = new UpdateExpenseUseCase(
            _repository,
            _timeProvider,
            new UpdateExpenseCommandValidator());
    }

    private static Expense Existing(Guid userId)
        => Expense.Hydrate(
            id: Guid.NewGuid(),
            userId: userId,
            amount: 10m,
            description: "old",
            category: ExpenseCategory.Food,
            incurredAt: FixedNow.UtcDateTime.AddDays(-2),
            createdAt: FixedNow.UtcDateTime.AddDays(-2));

    [Fact]
    public async Task HandleAsync_updates_expense_and_returns_dto()
    {
        Expense existing = Existing(OwnerId);
        _repository
            .GetByIdAsync(existing.Id, OwnerId, Arg.Any<CancellationToken>())
            .Returns(existing);

        UpdateExpenseCommand command = new(
            Amount: 25.50m,
            Description: "new",
            Category: "Transport",
            IncurredAt: FixedNow.UtcDateTime.AddHours(-1));

        ExpenseDto dto = await _sut.HandleAsync(OwnerId, existing.Id, command, CancellationToken.None);

        dto.Id.ShouldBe(existing.Id);
        dto.Amount.ShouldBe(25.50m);
        dto.Description.ShouldBe("new");
        dto.Category.ShouldBe("Transport");
        dto.IncurredAt.ShouldBe(FixedNow.UtcDateTime.AddHours(-1));

        await _repository.Received(1).UpdateAsync(
            Arg.Is<Expense>(e => e.Id == existing.Id && e.Amount == 25.50m),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_throws_NotFoundException_when_expense_does_not_belong_to_user()
    {
        Guid expenseId = Guid.NewGuid();
        _repository
            .GetByIdAsync(expenseId, OwnerId, Arg.Any<CancellationToken>())
            .Returns((Expense?)null);

        UpdateExpenseCommand command = new(
            Amount: 5m,
            Description: null,
            Category: "Other",
            IncurredAt: FixedNow.UtcDateTime);

        NotFoundException exception = await Should.ThrowAsync<NotFoundException>(
            () => _sut.HandleAsync(OwnerId, expenseId, command, CancellationToken.None));

        exception.Resource.ShouldBe("Expense");
        exception.Key.ShouldBe(expenseId);
        await _repository.DidNotReceiveWithAnyArgs().UpdateAsync(default!, default);
    }

    [Fact]
    public async Task HandleAsync_does_not_call_repository_for_update_when_validation_fails()
    {
        Guid expenseId = Guid.NewGuid();

        UpdateExpenseCommand command = new(
            Amount: -1m,
            Description: null,
            Category: "Bogus",
            IncurredAt: FixedNow.UtcDateTime);

        await Should.ThrowAsync<Common.ValidationException>(
            () => _sut.HandleAsync(OwnerId, expenseId, command, CancellationToken.None));

        await _repository.DidNotReceiveWithAnyArgs().GetByIdAsync(default!, default!, default);
        await _repository.DidNotReceiveWithAnyArgs().UpdateAsync(default!, default);
    }

    [Fact]
    public async Task HandleAsync_uses_user_id_from_caller_for_lookup()
    {
        Expense existing = Existing(OwnerId);
        _repository
            .GetByIdAsync(existing.Id, OwnerId, Arg.Any<CancellationToken>())
            .Returns(existing);

        UpdateExpenseCommand command = new(
            Amount: 1m,
            Description: null,
            Category: "Other",
            IncurredAt: FixedNow.UtcDateTime);

        await _sut.HandleAsync(OwnerId, existing.Id, command, CancellationToken.None);

        await _repository.Received(1).GetByIdAsync(existing.Id, OwnerId, Arg.Any<CancellationToken>());
        await _repository.DidNotReceive().GetByIdAsync(existing.Id, OtherUserId, Arg.Any<CancellationToken>());
    }
}
