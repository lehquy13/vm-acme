using System.Collections.Concurrent;
using VmAcme.Common;
using VmAcme.Todos;

namespace VmAcme.Persistence;

public sealed class InMemoryTodoRepository : ITodoRepository
{
    private readonly ConcurrentDictionary<Guid, TodoItem> _todos = new();

    public Task AddAsync(TodoItem todo, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!_todos.TryAdd(todo.Id, todo)) throw new InvalidOperationException("Duplicate todo ID.");
        return Task.CompletedTask;
    }

    public Task<TodoItem?> GetAsync(Guid id, Guid userId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(_todos.TryGetValue(id, out var todo) && todo.UserId == userId ? todo : null);
    }

    public Task<IReadOnlyList<TodoItem>> ListAsync(Guid userId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<IReadOnlyList<TodoItem>>(_todos.Values.Where(x => x.UserId == userId)
            .OrderBy(x => x.CreatedAt).ThenBy(x => x.Id).ToArray());
    }

    public Task<Result<Unit>> UpdateAsync(TodoItem todo, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        // Stored aggregates are shared; Complete() serializes its state transition.
        return Task.FromResult(_todos.ContainsKey(todo.Id) ? Result<Unit>.Success(new Unit()) : Missing());
    }

    public Task<Result<Unit>> DeleteAsync(TodoItem todo, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(_todos.TryRemove(todo.Id, out _) ? Result<Unit>.Success(new Unit()) : Missing());
    }

    private static Result<Unit> Missing() =>
        Result<Unit>.Failure(new Error("Todos.NotFound", "Todo was not found.", ErrorType.NotFound));
}