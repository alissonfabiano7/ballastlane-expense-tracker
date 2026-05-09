using BallastLane.Application.Common;
using BallastLane.Application.Expenses;
using NSubstitute;
using Shouldly;

namespace BallastLane.Application.Tests.Expenses;

public class DeleteExpenseUseCaseTests
{
    private static readonly Guid OwnerId = new("3f2504e0-4f89-11d3-9a0c-0305e82c3301");

    private readonly IExpenseRepository _repository = Substitute.For<IExpenseRepository>();
    private readonly DeleteExpenseUseCase _sut;

    public DeleteExpenseUseCaseTests()
    {
        _sut = new DeleteExpenseUseCase(_repository);
    }

    [Fact]
    public async Task HandleAsync_completes_when_repository_deletes_one_row()
    {
        Guid expenseId = Guid.NewGuid();
        _repository
            .DeleteAsync(expenseId, OwnerId, Arg.Any<CancellationToken>())
            .Returns(true);

        await _sut.HandleAsync(OwnerId, expenseId, CancellationToken.None);

        await _repository.Received(1).DeleteAsync(expenseId, OwnerId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_throws_NotFoundException_when_no_row_was_deleted()
    {
        Guid expenseId = Guid.NewGuid();
        _repository
            .DeleteAsync(expenseId, OwnerId, Arg.Any<CancellationToken>())
            .Returns(false);

        NotFoundException exception = await Should.ThrowAsync<NotFoundException>(
            () => _sut.HandleAsync(OwnerId, expenseId, CancellationToken.None));

        exception.Resource.ShouldBe("Expense");
        exception.Key.ShouldBe(expenseId);
    }
}
