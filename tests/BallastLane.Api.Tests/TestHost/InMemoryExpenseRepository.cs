using System.Collections.Concurrent;
using BallastLane.Application.Common;
using BallastLane.Application.Expenses;
using BallastLane.Domain.Expenses;

namespace BallastLane.Api.Tests.TestHost;

public sealed class InMemoryExpenseRepository : IExpenseRepository
{
    private readonly ConcurrentDictionary<Guid, Expense> _byId = new();

    public Task AddAsync(Expense expense, CancellationToken cancellationToken)
    {
        _byId[expense.Id] = expense;
        return Task.CompletedTask;
    }

    public Task<Expense?> GetByIdAsync(Guid id, Guid userId, CancellationToken cancellationToken)
    {
        if (_byId.TryGetValue(id, out Expense? expense) && expense.UserId == userId)
        {
            return Task.FromResult<Expense?>(expense);
        }
        return Task.FromResult<Expense?>(null);
    }

    public Task<PagedResult<Expense>> ListByUserAsync(
        Guid userId, int page, int pageSize, CancellationToken cancellationToken)
    {
        List<Expense> all = _byId.Values
            .Where(e => e.UserId == userId)
            .OrderByDescending(e => e.IncurredAt)
            .ThenBy(e => e.Id)
            .ToList();

        int totalCount = all.Count;
        List<Expense> items = all
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return Task.FromResult(new PagedResult<Expense>(items, page, pageSize, totalCount));
    }

    public Task UpdateAsync(Expense expense, CancellationToken cancellationToken)
    {
        if (_byId.TryGetValue(expense.Id, out Expense? existing) && existing.UserId == expense.UserId)
        {
            _byId[expense.Id] = expense;
        }
        return Task.CompletedTask;
    }

    public Task<bool> DeleteAsync(Guid id, Guid userId, CancellationToken cancellationToken)
    {
        if (_byId.TryGetValue(id, out Expense? expense) && expense.UserId == userId)
        {
            return Task.FromResult(_byId.TryRemove(id, out _));
        }
        return Task.FromResult(false);
    }

    public void Reset() => _byId.Clear();
}
