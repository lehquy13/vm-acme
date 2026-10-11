using VmAcme.Common;

namespace VmAcme.Todos;

public interface ITodoRepository
{
    Task AddAsync(TodoItem todo, CancellationToken cancellationToken);
    Task<TodoItem?> GetAsync(Guid id, Guid userId, CancellationToken cancellationToken);
    Task<IReadOnlyList<TodoItem>> ListAsync(Guid userId, CancellationToken cancellationToken);
    Task<Result<Unit>> UpdateAsync(TodoItem todo, CancellationToken cancellationToken);
    Task<Result<Unit>> DeleteAsync(TodoItem todo, CancellationToken cancellationToken);
}