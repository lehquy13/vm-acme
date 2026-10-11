using System.Collections.Concurrent;
using VmAcme.Common;
using VmAcme.Users;

namespace VmAcme.Persistence;

public sealed class InMemoryUserRepository : IUserRepository
{
    private readonly ConcurrentDictionary<string, User> _users = new(StringComparer.Ordinal);

    public Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(_users.Values.SingleOrDefault(x => x.Id == id));
    }

    public Task<User?> GetByEmailAsync(string normalizedEmail, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _users.TryGetValue(normalizedEmail, out var user);
        return Task.FromResult(user);
    }

    public Task<Result<Unit>> AddAsync(User user, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(_users.TryAdd(user.NormalizedEmail, user)
            ? Result<Unit>.Success(new Unit())
            : UserPersistenceErrors.DuplicateEmail());
    }
}

internal static class UserPersistenceErrors
{
    public static Result<Unit> DuplicateEmail() =>
        Result<Unit>.Failure(new Error("Users.EmailInUse", "Email is already registered.", ErrorType.Conflict));
}