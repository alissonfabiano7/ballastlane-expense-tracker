using BallastLane.Application.Common;
using BallastLane.Application.Expenses;
using BallastLane.Domain.Expenses;
using NSubstitute;
using Shouldly;

namespace BallastLane.Application.Tests.Expenses;

public class ListExpensesUseCaseTests
{
    private static readonly Guid OwnerId = new("3f2504e0-4f89-11d3-9a0c-0305e82c3301");
    private static readonly DateTime FixedUtcNow = new(2026, 5, 8, 12, 0, 0, DateTimeKind.Utc);

    private readonly IExpenseRepository _repository = Substitute.For<IExpenseRepository>();
    private readonly ListExpensesUseCase _sut;

    public ListExpensesUseCaseTests()
    {
        _sut = new ListExpensesUseCase(_repository);
    }

    private static Expense Sample(string description) => Expense.Hydrate(
        id: Guid.NewGuid(),
        userId: OwnerId,
        amount: 10m,
        description: description,
        category: ExpenseCategory.Food,
        incurredAt: FixedUtcNow,
        createdAt: FixedUtcNow);

    [Fact]
    public async Task HandleAsync_returns_paged_dtos_for_user()
    {
        List<Expense> page = [Sample("a"), Sample("b")];
        _repository
            .ListByUserAsync(OwnerId, 1, 20, Arg.Any<CancellationToken>())
            .Returns(new PagedResult<Expense>(Items: page, Page: 1, PageSize: 20, TotalCount: 2));

        PagedResult<ExpenseDto> result = await _sut.HandleAsync(
            OwnerId, new ListExpensesQuery(), CancellationToken.None);

        result.TotalCount.ShouldBe(2);
        result.Page.ShouldBe(1);
        result.PageSize.ShouldBe(20);
        result.Items.Count.ShouldBe(2);
        result.Items.ShouldContain(x => x.Description == "a");
        result.Items.ShouldContain(x => x.Description == "b");
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(-5, 1)]
    [InlineData(3, 3)]
    public async Task HandleAsync_clamps_page_to_minimum_one(int requestedPage, int expectedPage)
    {
        _repository
            .ListByUserAsync(OwnerId, expectedPage, 20, Arg.Any<CancellationToken>())
            .Returns(new PagedResult<Expense>(Items: [], Page: expectedPage, PageSize: 20, TotalCount: 0));

        await _sut.HandleAsync(OwnerId, new ListExpensesQuery(Page: requestedPage), CancellationToken.None);

        await _repository.Received(1).ListByUserAsync(OwnerId, expectedPage, 20, Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(0, ListExpensesUseCase.DefaultPageSize)]
    [InlineData(-1, ListExpensesUseCase.DefaultPageSize)]
    [InlineData(50, 50)]
    [InlineData(101, ListExpensesUseCase.MaxPageSize)]
    [InlineData(500, ListExpensesUseCase.MaxPageSize)]
    public async Task HandleAsync_clamps_page_size(int requested, int expected)
    {
        _repository
            .ListByUserAsync(OwnerId, 1, expected, Arg.Any<CancellationToken>())
            .Returns(new PagedResult<Expense>(Items: [], Page: 1, PageSize: expected, TotalCount: 0));

        await _sut.HandleAsync(OwnerId, new ListExpensesQuery(PageSize: requested), CancellationToken.None);

        await _repository.Received(1).ListByUserAsync(OwnerId, 1, expected, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_returns_empty_paged_result_when_user_has_none()
    {
        _repository
            .ListByUserAsync(OwnerId, 1, 20, Arg.Any<CancellationToken>())
            .Returns(new PagedResult<Expense>(Items: [], Page: 1, PageSize: 20, TotalCount: 0));

        PagedResult<ExpenseDto> result = await _sut.HandleAsync(
            OwnerId, new ListExpensesQuery(), CancellationToken.None);

        result.TotalCount.ShouldBe(0);
        result.Items.ShouldBeEmpty();
        result.TotalPages.ShouldBe(0);
    }
}
