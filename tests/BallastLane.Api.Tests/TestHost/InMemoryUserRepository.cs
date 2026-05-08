using System.Collections.Concurrent;
using BallastLane.Application.Users;
using BallastLane.Domain.Users;

namespace BallastLane.Api.Tests.TestHost;

public sealed class InMemoryUserRepository : IUserRepository
{
    private readonly ConcurrentDictionary<Guid, User> _byId = new();

    public Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
        => Task.FromResult(_byId.TryGetValue(id, out User? u) ? u : null);

    public Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken)
    {
        User? user = _byId.Values.SingleOrDefault(u =>
            string.Equals(u.Email, email, StringComparison.OrdinalIgnoreCase));
        return Task.FromResult(user);
    }

    public Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken)
        => Task.FromResult(_byId.Values.Any(u =>
            string.Equals(u.Email, email, StringComparison.OrdinalIgnoreCase)));

    public Task AddAsync(User user, CancellationToken cancellationToken)
    {
        _byId[user.Id] = user;
        return Task.CompletedTask;
    }

    public void Reset() => _byId.Clear();
}
