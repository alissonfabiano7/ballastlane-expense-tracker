using BallastLane.Application.Common;
using BallastLane.Application.Expenses;
using BallastLane.Domain.Expenses;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Shouldly;

namespace BallastLane.Application.Tests.Expenses;

public class CreateExpenseUseCaseTests
{
    private static readonly Guid OwnerId = new("3f2504e0-4f89-11d3-9a0c-0305e82c3301");
    private static readonly DateTimeOffset FixedNow = new(2026, 5, 8, 12, 0, 0, TimeSpan.Zero);

    private readonly IExpenseRepository _repository = Substitute.For<IExpenseRepository>();
    private readonly FakeTimeProvider _timeProvider = new();
    private readonly CreateExpenseUseCase _sut;

    public CreateExpenseUseCaseTests()
    {
        _timeProvider.SetUtcNow(FixedNow);
        _sut = new CreateExpenseUseCase(
            _repository,
            _timeProvider,
            new CreateExpenseCommandValidator());
    }

    [Fact]
    public async Task HandleAsync_creates_expense_for_user_and_returns_dto()
    {
        CreateExpenseCommand command = new(
            Amount: 12.34m,
            Description: "Coffee",
            Category: "Food",
            IncurredAt: FixedNow.UtcDateTime.AddHours(-1));

        ExpenseDto dto = await _sut.HandleAsync(OwnerId, command, CancellationToken.None);

        dto.Id.ShouldNotBe(Guid.Empty);
        dto.Amount.ShouldBe(12.34m);
        dto.Description.ShouldBe("Coffee");
        dto.Category.ShouldBe("Food");
        dto.IncurredAt.ShouldBe(FixedNow.UtcDateTime.AddHours(-1));
        dto.CreatedAt.ShouldBe(FixedNow.UtcDateTime);

        await _repository.Received(1).AddAsync(
            Arg.Is<Expense>(e =>
                e.UserId == OwnerId
                && e.Amount == 12.34m
                && e.Description == "Coffee"
                && e.Category == ExpenseCategory.Food),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_parses_category_case_insensitively()
    {
        CreateExpenseCommand command = new(
            Amount: 1m,
            Description: null,
            Category: "tRaNsPoRt",
            IncurredAt: FixedNow.UtcDateTime);

        ExpenseDto dto = await _sut.HandleAsync(OwnerId, command, CancellationToken.None);

        dto.Category.ShouldBe("Transport");
    }

    [Fact]
    public async Task HandleAsync_throws_ValidationException_when_amount_zero()
    {
        CreateExpenseCommand command = new(
            Amount: 0m,
            Description: null,
            Category: "Food",
            IncurredAt: FixedNow.UtcDateTime);

        Common.ValidationException exception = await Should.ThrowAsync<Common.ValidationException>(
            () => _sut.HandleAsync(OwnerId, command, CancellationToken.None));

        exception.Errors.ShouldContainKey(nameof(CreateExpenseCommand.Amount));
        await _repository.DidNotReceiveWithAnyArgs().AddAsync(default!, default);
    }

    [Fact]
    public async Task HandleAsync_throws_ValidationException_when_category_unknown()
    {
        CreateExpenseCommand command = new(
            Amount: 10m,
            Description: null,
            Category: "Yacht",
            IncurredAt: FixedNow.UtcDateTime);

        Common.ValidationException exception = await Should.ThrowAsync<Common.ValidationException>(
            () => _sut.HandleAsync(OwnerId, command, CancellationToken.None));

        exception.Errors.ShouldContainKey(nameof(CreateExpenseCommand.Category));
        await _repository.DidNotReceiveWithAnyArgs().AddAsync(default!, default);
    }

    [Fact]
    public async Task HandleAsync_does_not_call_repository_when_validation_fails()
    {
        CreateExpenseCommand command = new(
            Amount: -1m,
            Description: null,
            Category: "",
            IncurredAt: default);

        await Should.ThrowAsync<Common.ValidationException>(
            () => _sut.HandleAsync(OwnerId, command, CancellationToken.None));

        await _repository.DidNotReceiveWithAnyArgs().AddAsync(default!, default);
    }
}
