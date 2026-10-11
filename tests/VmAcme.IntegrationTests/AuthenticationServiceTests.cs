using VmAcme.Authentication;
using VmAcme.Common;
using VmAcme.Persistence;
using VmAcme.Todos;
using VmAcme.Users;

namespace VmAcme;

public sealed class AuthenticationServiceTests
{
    [Fact]
    public void Password_hashes_are_salted_and_verify_only_the_correct_password()
    {
        var hasher = new PasswordHasher();
        var first = hasher.Hash("Strong-password-123!");
        var second = hasher.Hash("Strong-password-123!");
        Assert.NotEqual(first, second);
        Assert.True(hasher.Verify("Strong-password-123!", first));
        Assert.False(hasher.Verify("Wrong-password-123!", first));
    }

    [Fact]
    public async Task Cancelled_repository_operations_do_not_mutate_storage()
    {
        var repository = new InMemoryTodoRepository();
        using var source = new CancellationTokenSource();
        await source.CancelAsync();
        var todo = TodoItem.Create(Guid.NewGuid(), "Cancelled", Priority.Normal, null, [], DateTimeOffset.UtcNow);
        await Assert.ThrowsAsync<OperationCanceledException>(() => repository.AddAsync(todo, source.Token));
        Assert.Empty(await repository.ListAsync(todo.UserId, CancellationToken.None));
    }

    [Fact]
    public async Task Concurrent_in_memory_registration_keeps_email_unique()
    {
        var repository = new InMemoryUserRepository();
        var results = await Task.WhenAll(Enumerable.Range(0, 10).Select(_ => Task.Run(() =>
            repository.AddAsync(User.Create("same@example.com", "User", "Name", "hash"), CancellationToken.None))));
        Assert.Single(results, x => x.IsSuccess);
        Assert.Equal(9, results.Count(x => x.IsFailure && x.Error!.Type == ErrorType.Conflict));
    }
}